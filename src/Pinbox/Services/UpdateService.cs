using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace Pinbox.Services;

public record UpdateInfo(bool Available, string? Version, string? DownloadUrl);

public static class UpdateService
{
    private const string VersionUrl =
        "https://raw.githubusercontent.com/RafiqulIslamNiL100/pinbox/main/version.json";
    private const string DownloadUrlTemplate =
        "https://raw.githubusercontent.com/RafiqulIslamNiL100/pinbox/main/Pinbox-for-Windows.zip";

    public static string CurrentVersion =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

    private static bool IsNewer(string a, string b)
    {
        int[] Parts(string v) => v.TrimStart('v', 'V').Split('.').Select(s => int.TryParse(s, out var n) ? n : 0).ToArray();
        var pa = Parts(a);
        var pb = Parts(b);
        for (int i = 0; i < Math.Max(pa.Length, pb.Length); i++)
        {
            int va = i < pa.Length ? pa[i] : 0;
            int vb = i < pb.Length ? pb[i] : 0;
            if (va != vb) return va > vb;
        }
        return false;
    }

    public static async Task<UpdateInfo> CheckForUpdateAsync()
    {
        if (!OperatingSystem.IsWindows()) return new UpdateInfo(false, null, null);

        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Pinbox-App-Updater");
            http.Timeout = TimeSpan.FromSeconds(10);

            var json = await http.GetStringAsync(VersionUrl);
            using var doc = JsonDocument.Parse(json);
            var latest = doc.RootElement.GetProperty("version").GetString() ?? "";

            if (string.IsNullOrEmpty(latest) || !IsNewer(latest, CurrentVersion))
                return new UpdateInfo(false, null, null);

            return new UpdateInfo(true, latest, DownloadUrlTemplate);
        }
        catch
        {
            return new UpdateInfo(false, null, null);
        }
    }

    /// Downloads the new build, extracts it, and hands off to a small script
    /// that waits for Pinbox.exe to exit, swaps the installed files, and
    /// relaunches it. Call Environment.Exit after this to let the swap happen.
    public static async Task DownloadAndApplyAsync(string downloadUrl)
    {
        var tempDir = Path.GetTempPath();
        var zipPath = Path.Combine(tempDir, "pinbox-update.zip");
        var extractDir = Path.Combine(tempDir, $"pinbox-update-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}");
        var scriptPath = Path.Combine(tempDir, $"pinbox-apply-update-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.bat");

        // Update whichever folder Pinbox is actually running from - not a
        // hardcoded install path. That hardcoded assumption was the actual
        // bug: anyone running the portable zip (or any copy not installed to
        // the default location) would have their real running folder left
        // untouched while a second copy silently appeared elsewhere, making
        // the update look like it did nothing.
        var installDir = Path.GetDirectoryName(Environment.ProcessPath)
            ?? throw new AuthException("Couldn't determine where Pinbox is running from.");

        using (var http = new HttpClient())
        {
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Pinbox-App-Updater");
            http.Timeout = TimeSpan.FromMinutes(3);
            var bytes = await http.GetByteArrayAsync(downloadUrl);
            await File.WriteAllBytesAsync(zipPath, bytes);
        }

        if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);
        ZipFile.ExtractToDirectory(zipPath, extractDir);

        if (!File.Exists(Path.Combine(extractDir, "Pinbox.exe")))
            throw new AuthException("The downloaded update looked incomplete or corrupt.");

        var script = string.Join("\r\n", new[]
        {
            "@echo off",
            "setlocal",
            $"set \"DEST={installDir}\"",
            $"set \"NEW={extractDir}\"",
            ":waitloop",
            "tasklist /FI \"IMAGENAME eq Pinbox.exe\" 2>NUL | find /I \"Pinbox.exe\" >NUL",
            "if \"%ERRORLEVEL%\"==\"0\" (",
            "  timeout /t 1 >nul",
            "  goto waitloop",
            ")",
            "if exist \"%DEST%\" rmdir /s /q \"%DEST%\"",
            "mkdir \"%DEST%\" >nul 2>nul",
            "xcopy \"%NEW%\\*\" \"%DEST%\\\" /E /I /Y /Q >nul",
            "start \"\" \"%DEST%\\Pinbox.exe\"",
            "rmdir /s /q \"%NEW%\"",
            $"del \"{zipPath}\"",
            "del \"%~f0\"",
            "",
        });
        await File.WriteAllTextAsync(scriptPath, script);

        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c \"{scriptPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        Process.Start(psi);
    }
}
