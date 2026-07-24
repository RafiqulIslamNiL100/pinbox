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

    [StructLayout(LayoutKind.Sequential)]
    private struct KeybdInput
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Explicit)]
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

    /// Copies the text to the clipboard, minimizes Pinbox (which hands focus
    /// back to whatever window was active before), then sends Ctrl+V so it
    /// pastes into the field the user was previously focused on.
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

        window.WindowState = WindowState.Minimized;
        await Task.Delay(300);

        SendCtrlV();

        if (clipboard is not null && previous is not null)
        {
            _ = Task.Delay(600).ContinueWith(async _ =>
            {
                try { await clipboard.SetTextAsync(previous); } catch { /* best effort */ }
            });
        }
    }

    /// Puts the image file itself on the clipboard (as a copied file, the
    /// same as copying it in Explorer), minimizes Pinbox, then pastes. Most
    /// chat/email/document apps accept a pasted file this way and attach or
    /// inline it as an image.
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

        window.WindowState = WindowState.Minimized;
        await Task.Delay(300);

        SendCtrlV();
    }
}
