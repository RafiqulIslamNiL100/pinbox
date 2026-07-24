using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Pinbox.Models;

namespace Pinbox.Services;

public record LicenseStatus(bool Ok, string Reason);

public static class LicenseService
{
    public static async Task<LicenseStatus> ActivateKeyAsync(AuthSession session, string code)
    {
        var payload = new { p_code = code.Trim() };
        var req = AuthService.AuthedRequest(HttpMethod.Post, "/rest/v1/rpc/activate_key", session);
        req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var doc = await AuthService.SendAuthedAsync(req, "Could not activate this key.");
        var root = doc.RootElement;
        bool ok = root.TryGetProperty("ok", out var okProp) && okProp.GetBoolean();
        string reason = root.TryGetProperty("reason", out var r) ? (r.GetString() ?? "") : "";
        return new LicenseStatus(ok, reason);
    }

    public static async Task<LicenseStatus> CheckLicenseAsync(AuthSession session)
    {
        var req = AuthService.AuthedRequest(HttpMethod.Post, "/rest/v1/rpc/check_license", session);
        req.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        var doc = await AuthService.SendAuthedAsync(req, "Could not verify your license.");
        var root = doc.RootElement;
        bool ok = root.TryGetProperty("ok", out var okProp) && okProp.GetBoolean();
        string reason = root.TryGetProperty("reason", out var r) ? (r.GetString() ?? "") : "";
        return new LicenseStatus(ok, reason);
    }

    public static string DescribeReason(string reason, bool zh) => reason switch
    {
        "no_key" => zh ? "此账户尚未激活。" : "This account hasn't been activated yet.",
        "expired" => zh ? "您的激活密钥已过期。" : "Your activation key has expired.",
        "banned" => zh ? "此账户已被封禁。" : "This account has been banned.",
        "restricted" => zh ? "此账户已被限制。" : "This account has been restricted.",
        "already_used" => zh ? "此密钥已被使用。" : "That key has already been used.",
        "revoked" => zh ? "此密钥已被撤销。" : "That key has been revoked.",
        "not_found" => zh ? "未找到该密钥。" : "That key wasn't found.",
        _ => zh ? "无法验证您的许可证。" : "Couldn't verify your license.",
    };
}
