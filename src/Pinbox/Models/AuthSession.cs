namespace Pinbox.Models;

public class AuthSession
{
    public string AccessToken { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public string UserId { get; set; } = "";
    public string Email { get; set; } = "";
    public string Name { get; set; } = "";

    // Set only after a real server confirmation that this account has a
    // valid license, so a dropped connection on a later launch can offer an
    // offline grace period to an actually-licensed user without also
    // letting in someone who saved a session but never activated a key -
    // the server was never wrong to trust here, it just wasn't reachable.
    public bool LicenseVerifiedOk { get; set; }
}
