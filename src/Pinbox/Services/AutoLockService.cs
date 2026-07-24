using System;
using System.Runtime.InteropServices;
using Avalonia.Threading;

namespace Pinbox.Services;

// Locks the app after N minutes of *system-wide* idle time (not just idle
// inside Pinbox) via GetLastInputInfo, matching how Windows' own lock screen
// defines "idle."
public class AutoLockService : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("user32.dll")] private static extern bool GetLastInputInfo(ref LastInputInfo info);
    [DllImport("kernel32.dll")] private static extern uint GetTickCount();

    public event Action? IdleTimeoutReached;

    private readonly DispatcherTimer _timer;
    private int _minutes;
    private bool _locked;

    public AutoLockService()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _timer.Tick += (_, _) => CheckIdle();
    }

    public void Configure(int minutes)
    {
        _minutes = minutes;
        _locked = false;
        if (minutes > 0 && OperatingSystem.IsWindows()) _timer.Start();
        else _timer.Stop();
    }

    public void NotifyUnlocked() => _locked = false;

    private void CheckIdle()
    {
        if (_minutes <= 0 || _locked) return;

        try
        {
            var info = new LastInputInfo { cbSize = (uint)Marshal.SizeOf<LastInputInfo>() };
            if (!GetLastInputInfo(ref info)) return;

            var idleMs = GetTickCount() - info.dwTime;
            if (idleMs >= _minutes * 60_000)
            {
                _locked = true;
                IdleTimeoutReached?.Invoke();
            }
        }
        catch
        {
            // If we can't read idle time, just don't auto-lock rather than guess.
        }
    }

    public void Dispose() => _timer.Stop();
}
