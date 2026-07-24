using System;
using System.Diagnostics;
using System.IO;
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
        "https://raw.githubusercontent.com/RafiqulIslamNiL100/pinbox/main/Pinbox-Setup.exe";

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

    /// Downloads the real installer (the same Pinbox-Setup.exe you'd run by
    /// hand) and runs it silently, then relaunches Pinbox once it's done.
    ///
    /// This replaced an earlier version that manually extracted a zip and
    /// xcopy'd files into place itself. That hand-rolled approach had two
    /// real bugs: it assumed a hardcoded install folder (breaking portable
    /// installs), and even once that was fixed, a plain file copy has no way
    /// to deal with locked DLLs, partial antivirus scans, or any of the other
    /// things a real installer already handles correctly - which is exactly
    /// why "download Setup.exe and run it yourself" always worked while the
    /// in-app button didn't. Running that same installer silently gives the
    /// in-app button the same reliability.
    public static async Task DownloadAndApplyAsync(string downloadUrl)
    {
        var tempDir = Path.GetTempPath();
        var installerPath = Path.Combine(tempDir, $"Pinbox-Setup-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.exe");

        using (var http = new HttpClient())
        {
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Pinbox-App-Updater");
            http.Timeout = TimeSpan.FromMinutes(3);
            var bytes = await http.GetByteArrayAsync(downloadUrl);
            await File.WriteAllBytesAsync(installerPath, bytes);
        }

        // A real installer is tens of MB; anything drastically smaller means
        // the download got cut short or GitHub served an error page instead.
        if (new FileInfo(installerPath).Length < 5_000_000)
            throw new AuthException("The downloaded update looked incomplete - check your internet connection and try again.");

        // RequestExecutionLevel in the installer is "user", so /S runs with
        // no UAC prompt and no visible window. taskkill inside the installer
        // itself closes the currently-running Pinbox.exe for us.
        var installerProcess = Process.Start(new ProcessStartInfo
        {
            FileName = installerPath,
            Arguments = "/S",
            UseShellExecute = true,
        }) ?? throw new AuthException("Couldn't start the installer.");

        // A silent install skips the finish page, so nothing relaunches
        // Pinbox automatically - a small detached script waits for the
        // installer (by its exact PID, not by name) to finish, then starts
        // the freshly-installed Pinbox.exe itself and cleans up after itself.
        var installDir = AppPaths.InstallDirectory;
        var scriptPath = Path.Combine(tempDir, $"pinbox-relaunch-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.bat");
        var script = string.Join("\r\n", new[]
        {
            "@echo off",
            ":waitloop",
            $"tasklist /FI \"PID eq {installerProcess.Id}\" 2>NUL | find \"{installerProcess.Id}\" >NUL",
            "if \"%ERRORLEVEL%\"==\"0\" (",
            "  timeout /t 1 >nul",
            "  goto waitloop",
            ")",
            $"start \"\" \"{installDir}\\Pinbox.exe\"",
            $"del \"{installerPath}\"",
            "del \"%~f0\"",
            "",
        });
        await File.WriteAllTextAsync(scriptPath, script);

        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c \"{scriptPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
        });
    }
}
