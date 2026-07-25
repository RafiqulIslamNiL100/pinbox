using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Pinbox.Models;

namespace Pinbox.Services;

public record LicenseStatus(bool Ok, string Reason);

public static class LicenseService
{
    // Redeeming a key is an explicit action, so it always claims this
    // device as the account's single active device server-side.
    public static async Task<LicenseStatus> ActivateKeyAsync(AuthSession session, string code)
    {
        var payload = new { p_code = code.Trim(), p_device_id = DeviceService.DeviceId, p_device_label = DeviceService.DeviceLabel };
        var req = AuthService.AuthedRequest(HttpMethod.Post, "/rest/v1/rpc/activate_key", session);
        req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var doc = await AuthService.SendAuthedAsync(req, "Could not activate this key.");
        var root = doc.RootElement;
        bool ok = root.TryGetProperty("ok", out var okProp) && okProp.GetBoolean();
        string reason = root.TryGetProperty("reason", out var r) ? (r.GetString() ?? "") : "";
        return new LicenseStatus(ok, reason);
    }

    // claim = true (explicit sign-in/sign-up) always takes over the
    // account's single device slot - that's what signs any other device
    // out. claim = false (silent resume on launch, or the periodic
    // background poll) only verifies this is still the claimed device,
    // and comes back with reason "device_mismatch" if another device has
    // since taken over.
    public static async Task<LicenseStatus> CheckLicenseAsync(AuthSession session, bool claim = false)
    {
        var payload = new { p_device_id = DeviceService.DeviceId, p_claim = claim, p_device_label = DeviceService.DeviceLabel };
        var req = AuthService.AuthedRequest(HttpMethod.Post, "/rest/v1/rpc/check_license", session);
        req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

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
        "device_mismatch" => zh ? "此账户已在另一台设备上登录。" : "This account is signed in on another device.",
        _ => zh ? "无法验证您的许可证。" : "Couldn't verify your license.",
    };
}
