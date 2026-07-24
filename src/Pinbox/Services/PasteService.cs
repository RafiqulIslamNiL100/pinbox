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

    /// Copies the text to the clipboard, hides Pinbox (which hands focus back
    /// to whatever window was active before - the same Hide()/Show() pair the
    /// rest of the app already uses for its tray icon and global hotkey, so
    /// bringing Pinbox back afterward is guaranteed to work), pastes with
    /// Ctrl+V, then presses Enter to submit it - the same thing pressing Enter
    /// does in Messenger, Telegram, Facebook comments, and WhatsApp Web.
    ///
    /// Using WindowState.Minimized here previously caused the bug where
    /// Pinbox seemed to vanish after sending: a minimized window doesn't
    /// reliably come back via Show()+Activate() on Windows, so the hotkey and
    /// tray icon's "restore" path stopped working. Hide() doesn't have that
    /// problem because it's the exact mechanism already proven to work for
    /// bringing the window back.
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

        window.Hide();
        await Task.Delay(300);

        SendCtrlV();
        await Task.Delay(150);
        SendKeyStroke(VkReturn);

        if (clipboard is not null && previous is not null)
        {
            _ = RestoreClipboardLaterAsync(clipboard, previous);
        }
    }

    /// Puts the image file itself on the clipboard (as a copied file, the
    /// same as copying it in Explorer), hides Pinbox, pastes, then presses
    /// Enter to submit it - see SendTextAsync for why Hide() replaced Minimize.
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

        window.Hide();
        await Task.Delay(300);

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
