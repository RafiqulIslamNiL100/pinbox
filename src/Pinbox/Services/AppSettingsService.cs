using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;
using Pinbox.Models;

namespace Pinbox.Services;

public static class AppSettingsService
{
    private static string SettingsPath =>
        Path.Combine(AppPaths.DataDirectory, "app-settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return new AppSettings();
            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        Directory.CreateDirectory(AppPaths.DataDirectory);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static void SetPin(AppSettings settings, string pin)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(pin));
        settings.PinHash = Convert.ToBase64String(hash);
    }

    public static bool VerifyPin(AppSettings settings, string pin)
    {
        if (string.IsNullOrEmpty(settings.PinHash)) return false;
        using var sha = SHA256.Create();
        var hash = Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(pin)));
        return hash == settings.PinHash;
    }

    // Toggles a "run at Windows login" entry in the current user's registry
    // Run key - no admin rights needed, matches the per-user install.
    public static void SetStartWithWindows(bool enabled)
    {
        if (!OperatingSystem.IsWindows()) return;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
            if (key == null) return;

            if (enabled)
            {
                var exePath = Environment.ProcessPath ?? "";
                if (!string.IsNullOrEmpty(exePath))
                    key.SetValue("Pinbox", $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue("Pinbox", throwOnMissingValue: false);
            }
        }
        catch
        {
            // Non-fatal - the toggle just won't take effect this time.
        }
    }
}
