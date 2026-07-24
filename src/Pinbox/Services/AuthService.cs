using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Pinbox.Models;

namespace Pinbox.Services;

public class AuthException : Exception
{
    public AuthException(string message) : base(message) { }
}

// Thin HTTP client over Supabase Auth (GoTrue). Passwords are never touched
// or stored by this app - Supabase hashes and stores them server-side. This
// class only exchanges credentials for a session (access + refresh token).
public static class AuthService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    private static string SessionPath =>
        Path.Combine(AppPaths.DataDirectory, "session.json");

    private static void EnsureConfigured()
    {
        if (!SupabaseConfig.IsConfigured)
            throw new AuthException("Pinbox isn't connected to a server yet. Contact the developer.");
    }

    private static HttpRequestMessage NewRequest(HttpMethod method, string path)
    {
        var req = new HttpRequestMessage(method, $"{SupabaseConfig.Url}{path}");
        req.Headers.Add("apikey", SupabaseConfig.AnonKey);
        return req;
    }

    private static async Task<JsonDocument> SendAsync(HttpRequestMessage req, string genericErrorMessage)
    {
        HttpResponseMessage res;
        try
        {
            res = await Http.SendAsync(req);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new AuthException("Couldn't reach the server. Check your internet connection and try again.");
        }

        var body = await res.Content.ReadAsStringAsync();
        JsonDocument doc;
        try { doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body); }
        catch { doc = JsonDocument.Parse("{}"); }

        if (!res.IsSuccessStatusCode)
        {
            string message = genericErrorMessage;
            var root = doc.RootElement;
            if (root.TryGetProperty("error_description", out var ed)) message = ed.GetString() ?? message;
            else if (root.TryGetProperty("msg", out var msg)) message = msg.GetString() ?? message;
            else if (root.TryGetProperty("message", out var m2)) message = m2.GetString() ?? message;
            throw new AuthException(message);
        }

        return doc;
    }

    private static AuthSession ParseSession(JsonDocument doc)
    {
        var root = doc.RootElement;
        var user = root.GetProperty("user");
        string name = "";
        if (user.TryGetProperty("user_metadata", out var meta) && meta.TryGetProperty("name", out var n))
            name = n.GetString() ?? "";

        return new AuthSession
        {
            AccessToken = root.GetProperty("access_token").GetString() ?? "",
            RefreshToken = root.GetProperty("refresh_token").GetString() ?? "",
            UserId = user.GetProperty("id").GetString() ?? "",
            Email = user.GetProperty("email").GetString() ?? "",
            Name = name,
        };
    }

    public static async Task<AuthSession> SignUpAsync(string name, string email, string password)
    {
        EnsureConfigured();
        var cleanName = (name ?? "").Trim();
        var cleanEmail = (email ?? "").Trim().ToLowerInvariant();

        if (string.IsNullOrEmpty(cleanName) || string.IsNullOrEmpty(cleanEmail) || string.IsNullOrEmpty(password))
            throw new AuthException("Please fill in every field.");
        if (password.Length < 8)
            throw new AuthException("Password must be at least 8 characters.");

        var payload = new
        {
            email = cleanEmail,
            password,
            data = new { name = cleanName, signup_path = "self" },
        };

        var req = NewRequest(HttpMethod.Post, "/auth/v1/signup");
        req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var doc = await SendAsync(req, "Could not create your account.");
        var session = ParseSession(doc);
        if (string.IsNullOrEmpty(session.Name)) session.Name = cleanName;
        return session;
    }

    public static async Task<AuthSession> SignInAsync(string email, string password)
    {
        EnsureConfigured();
        var payload = new { email = (email ?? "").Trim().ToLowerInvariant(), password };

        var req = NewRequest(HttpMethod.Post, "/auth/v1/token?grant_type=password");
        req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var doc = await SendAsync(req, "Incorrect email or password.");
        return ParseSession(doc);
    }

    public static async Task<AuthSession> RefreshAsync(string refreshToken)
    {
        EnsureConfigured();
        var payload = new { refresh_token = refreshToken };

        var req = NewRequest(HttpMethod.Post, "/auth/v1/token?grant_type=refresh_token");
        req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var doc = await SendAsync(req, "Session expired, please sign in again.");
        return ParseSession(doc);
    }

    public static HttpRequestMessage AuthedRequest(HttpMethod method, string path, AuthSession session)
    {
        var req = NewRequest(method, path);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        return req;
    }

    public static async Task<JsonDocument> SendAuthedAsync(HttpRequestMessage req, string genericErrorMessage)
        => await SendAsync(req, genericErrorMessage);

    public static void SaveSession(AuthSession session)
    {
        Directory.CreateDirectory(AppPaths.DataDirectory);
        File.WriteAllText(SessionPath, JsonSerializer.Serialize(session));
    }

    public static AuthSession? LoadSession()
    {
        try
        {
            if (!File.Exists(SessionPath)) return null;
            return JsonSerializer.Deserialize<AuthSession>(File.ReadAllText(SessionPath));
        }
        catch { return null; }
    }

    public static void ClearSession()
    {
        try { if (File.Exists(SessionPath)) File.Delete(SessionPath); } catch { /* non-fatal */ }
    }
}
