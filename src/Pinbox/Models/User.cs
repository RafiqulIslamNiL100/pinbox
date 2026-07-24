namespace Pinbox.Models;

public class User
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string PasswordSalt { get; set; } = "";
    public long CreatedAt { get; set; }
}

public record PublicUser(string Id, string Name, string Email);
