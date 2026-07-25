namespace Pinbox.Models;

public class AppSettings
{
    public string GlobalHotkey { get; set; } = "Ctrl+Shift+V";
    public bool StartWithWindows { get; set; }
    public bool CompactModeDefault { get; set; }
    public bool NotificationsEnabled { get; set; } = true;
    public string Theme { get; set; } = "System"; // Light, Dark, System
    public int AutoLockMinutes { get; set; } // 0 = off
    public string? PinHash { get; set; }
    public string? DeviceId { get; set; }
}
