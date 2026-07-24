using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Pinbox.Services;

// Registers Windows global hotkeys (work even when Pinbox isn't focused).
// Uses its own tiny hidden native window rather than hooking into
// Avalonia's window internals - RegisterHotKey delivers WM_HOTKEY to
// whichever window registered it, and since this window is created on the
// same UI thread Avalonia already pumps messages on, its WndProc gets
// called back correctly without needing access to Avalonia's own message
// loop at all.
public class HotkeyService : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const int WsExNoActivate = 0x08000000;
    private static readonly IntPtr HwndMessage = new(-3);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowEx(int exStyle, string className, string windowName, int style,
        int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);

    [DllImport("user32.dll")] private static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern ushort RegisterClassEx(ref WndClassEx lpwcx);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(IntPtr hWnd);
    [DllImport("kernel32.dll")] private static extern IntPtr GetModuleHandle(string? lpModuleName);

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WndClassEx
    {
        public int cbSize;
        public int style;
        public WndProcDelegate lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
        public IntPtr hIconSm;
    }

    private const uint ModAlt = 0x1, ModControl = 0x2, ModShift = 0x4;

    private readonly Dictionary<int, Action> _handlers = new();
    private readonly WndProcDelegate _wndProcDelegate;
    private readonly IntPtr _hwnd = IntPtr.Zero;
    private int _nextId = 1;

    public bool IsAvailable => _hwnd != IntPtr.Zero;

    public HotkeyService()
    {
        _wndProcDelegate = WndProc;

        if (!OperatingSystem.IsWindows()) return;

        try
        {
            var className = "PinboxHotkeyWindow_" + Guid.NewGuid().ToString("N");
            var wc = new WndClassEx
            {
                cbSize = Marshal.SizeOf<WndClassEx>(),
                lpfnWndProc = _wndProcDelegate,
                hInstance = GetModuleHandle(null),
                lpszClassName = className,
            };
            RegisterClassEx(ref wc);

            _hwnd = CreateWindowEx(WsExNoActivate, className, "PinboxHotkeys", 0, 0, 0, 0, 0,
                HwndMessage, IntPtr.Zero, wc.hInstance, IntPtr.Zero);
        }
        catch
        {
            _hwnd = IntPtr.Zero;
        }
    }

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WmHotkey && _handlers.TryGetValue(wParam.ToInt32(), out var action))
        {
            try { action(); } catch { /* never let a hotkey handler crash the app */ }
            return IntPtr.Zero;
        }
        return DefWindowProc(hWnd, msg, wParam, lParam);
    }

    /// Parses combos like "Ctrl+Shift+V" and registers them. Returns false if
    /// the combo couldn't be parsed or is already taken by another app.
    public bool Register(string combo, Action onPressed)
    {
        if (_hwnd == IntPtr.Zero || !TryParse(combo, out var mods, out var vk)) return false;

        var id = _nextId++;
        if (!RegisterHotKey(_hwnd, id, mods, vk)) return false;

        _handlers[id] = onPressed;
        return true;
    }

    public void UnregisterAll()
    {
        foreach (var id in _handlers.Keys)
        {
            try { UnregisterHotKey(_hwnd, id); } catch { /* best effort */ }
        }
        _handlers.Clear();
    }

    private static bool TryParse(string combo, out uint mods, out uint vk)
    {
        mods = 0; vk = 0;
        if (string.IsNullOrWhiteSpace(combo)) return false;

        var parts = combo.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        string? keyPart = null;
        foreach (var part in parts)
        {
            switch (part.ToLowerInvariant())
            {
                case "ctrl": case "control": mods |= ModControl; break;
                case "alt": mods |= ModAlt; break;
                case "shift": mods |= ModShift; break;
                default: keyPart = part; break;
            }
        }
        if (keyPart == null) return false;

        if (keyPart.Length == 1 && char.IsLetterOrDigit(keyPart[0]))
        {
            vk = char.ToUpperInvariant(keyPart[0]);
            return true;
        }
        if (keyPart.Length == 2 && keyPart[0] is 'F' or 'f' && char.IsDigit(keyPart[1]))
        {
            vk = (uint)(0x70 + (keyPart[1] - '1')); // F1..F9
            return true;
        }
        return false;
    }

    public void Dispose()
    {
        UnregisterAll();
        if (_hwnd != IntPtr.Zero)
        {
            try { DestroyWindow(_hwnd); } catch { /* best effort */ }
        }
    }
}
