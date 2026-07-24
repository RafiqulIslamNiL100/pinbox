using System;
using System.IO;
using System.Text.Json;

namespace Pinbox.Services;

public static class SupabaseConfig
{
    private static string ConfigPath =>
        Path.Combine(AppContext.BaseDirectory, "supabase-config.json");

    public static string Url { get; private set; } = "";
    public static string AnonKey { get; private set; } = "";
    public static bool IsConfigured { get; private set; }

    static SupabaseConfig()
    {
        try
        {
            var json = File.ReadAllText(ConfigPath);
            using var doc = JsonDocument.Parse(json);
            Url = doc.RootElement.GetProperty("url").GetString() ?? "";
            AnonKey = doc.RootElement.GetProperty("anonKey").GetString() ?? "";
            IsConfigured = !string.IsNullOrWhiteSpace(Url)
                && !Url.Contains("YOUR-PROJECT")
                && !string.IsNullOrWhiteSpace(AnonKey)
                && !AnonKey.Contains("YOUR-ANON-KEY");
        }
        catch
        {
            IsConfigured = false;
        }
    }
}
