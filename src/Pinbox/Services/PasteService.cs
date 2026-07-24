using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;

namespace Pinbox.Services;

public static class PasteService
{
    private const int InputKeyboard = 1;
    private const uint KeyEventFKeyUp = 0x0002;
    private const ushort VkControl = 0x11;
    private const ushort VkV = 0x56;
    private const ushort VkReturn = 0x0D;

    [StructLayout(LayoutKind.Sequential)]
    private struct KeybdInput
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    // Win32's actual INPUT union is sized to fit its largest member,
    // MOUSEINPUT (32 bytes on 64-bit Windows) - not KEYBDINPUT (24 bytes).
    // Without the explicit Size below, Marshal.SizeOf<Input> comes out
    // smaller than the real native INPUT struct, so SendInput reads/writes
    // past the end of each element when striding through the array - memory
    // corruption that surfaces as a hard, uncatchable process crash (an
    // access violation, not a .NET exception - no try/catch can stop it).
    // This was the real cause of Pinbox crashing on send: every call here
    // sends 2-4 elements, and the corruption became more likely once Enter
    // was added as a second SendInput call after Ctrl+V.
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    private struct InputUnion
    {
        [FieldOffset(0)] public KeybdInput ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public int type;
        public InputUnion u;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, Input[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(IntPtr hWnd);

    /// The last window that had focus that WASN'T Pinbox itself. A timer in
    /// MainWindow keeps this current. On send we switch focus back to it and
    /// paste there - instead of hiding Pinbox - so Pinbox stays visible.
    public static IntPtr LastExternalWindow { get; set; }

    public static IntPtr CurrentForegroundWindow() => GetForegroundWindow();

    private static void SendKeyStroke(ushort vk)
    {
        var inputs = new Input[]
        {
            new() { type = InputKeyboard, u = new InputUnion { ki = new KeybdInput { wVk = vk } } },
            new() { type = InputKeyboard, u = new InputUnion { ki = new KeybdInput { wVk = vk, dwFlags = KeyEventFKeyUp } } },
        };
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
    }

    private static void SendCtrlV()
    {
        var inputs = new Input[]
        {
            new() { type = InputKeyboard, u = new InputUnion { ki = new KeybdInput { wVk = VkControl } } },
            new() { type = InputKeyboard, u = new InputUnion { ki = new KeybdInput { wVk = VkV } } },
            new() { type = InputKeyboard, u = new InputUnion { ki = new KeybdInput { wVk = VkV, dwFlags = KeyEventFKeyUp } } },
            new() { type = InputKeyboard, u = new InputUnion { ki = new KeybdInput { wVk = VkControl, dwFlags = KeyEventFKeyUp } } },
        };
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
    }

    /// Copies the text to the clipboard, switches focus to the app the user
    /// was last in (via SetForegroundWindow - allowed because Pinbox itself
    /// owns the foreground at click time), pastes with Ctrl+V, then presses
    /// Enter to submit it - the same thing pressing Enter does in Messenger,
    /// Telegram, Facebook comments, and WhatsApp Web.
    ///
    /// Crucially, Pinbox is NOT hidden or minimized here anymore. Earlier
    /// versions called window.Hide() (and before that Minimize()), which is
    /// what made Pinbox appear to "close automatically" after every send.
    /// Because Pinbox owns the foreground when you click an item, it's allowed
    /// to hand focus to another window with SetForegroundWindow, which is all
    /// that's needed for the paste to land in the right place - the Pinbox
    /// window just stays put, unfocused, exactly where it was.
    public static async Task SendTextAsync(Window window, string text)
    {
        if (!OperatingSystem.IsWindows())
            throw new AuthException("Sending into other apps is only supported on Windows.");

        IClipboard? clipboard = window.Clipboard;
        string? previous = null;
        if (clipboard is not null)
        {
            try { previous = await clipboard.GetTextAsync(); } catch { /* clipboard may be empty or non-text */ }
            await clipboard.SetTextAsync(text);
        }

        await FocusTargetAndPasteAsync();

        if (clipboard is not null && previous is not null)
        {
            _ = RestoreClipboardLaterAsync(clipboard, previous);
        }
    }

    /// Puts the image file itself on the clipboard (as a copied file, the
    /// same as copying it in Explorer), switches focus to the last app,
    /// pastes, then presses Enter - Pinbox stays visible, see SendTextAsync.
    public static async Task SendImageAsync(Window window, string imagePath)
    {
        if (!OperatingSystem.IsWindows())
            throw new AuthException("Sending into other apps is only supported on Windows.");

        IClipboard? clipboard = window.Clipboard;
        if (clipboard is not null)
        {
            var topLevel = TopLevel.GetTopLevel(window);
            var file = topLevel is not null
                ? await topLevel.StorageProvider.TryGetFileFromPathAsync(imagePath)
                : null;

            var data = new DataObject();
            if (file is not null)
            {
                data.Set(DataFormats.Files, new List<IStorageItem> { file });
            }
            await clipboard.SetDataObjectAsync(data);
        }

        await FocusTargetAndPasteAsync();
    }

    private static async Task FocusTargetAndPasteAsync()
    {
        var target = LastExternalWindow;
        if (target != IntPtr.Zero && IsWindow(target))
        {
            SetForegroundWindow(target);
            // Give Windows a moment to actually complete the focus switch
            // before the keystrokes go out, otherwise they can land on Pinbox.
            await Task.Delay(120);
        }

        SendCtrlV();
        await Task.Delay(150);
        SendKeyStroke(VkReturn);
    }

    // A plain async method awaited fire-and-forget (rather than
    // Task.Delay(...).ContinueWith(async _ => ...)) so any failure is
    // contained entirely within its own try/catch - a ContinueWith callback
    // that itself returns a Task creates a nested, never-awaited inner task,
    // which is exactly the kind of thing that can end up as an unobserved
    // task exception.
    private static async Task RestoreClipboardLaterAsync(IClipboard clipboard, string previous)
    {
        await Task.Delay(600);
        try { await clipboard.SetTextAsync(previous); } catch { /* best effort */ }
    }
}
