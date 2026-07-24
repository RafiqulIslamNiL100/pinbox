using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Pinbox.Models;

namespace Pinbox.Services;

public class AuthException : Exception
{
    public AuthException(string message) : base(message) { }
}

public static class AuthService
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 100_000;

    private static string UsersFilePath =>
        Path.Combine(AppPaths.DataDirectory, "users.json");

    private static System.Collections.Generic.List<User> LoadUsers()
    {
        if (!File.Exists(UsersFilePath)) return new();
        var json = File.ReadAllText(UsersFilePath);
        return JsonSerializer.Deserialize<System.Collections.Generic.List<User>>(json) ?? new();
    }

    private static void SaveUsers(System.Collections.Generic.List<User> users)
    {
        Directory.CreateDirectory(AppPaths.DataDirectory);
        var json = JsonSerializer.Serialize(users, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(UsersFilePath, json);
    }

    // Manual PBKDF2-HMAC-SHA256 (RFC 8018), implemented directly on top of
    // HMACSHA256 rather than Rfc2898DeriveBytes.Pbkdf2. The .NET built-in
    // routes through the OS's native BCryptDeriveKeyPBKDF2 API, which is not
    // fully implemented on some non-Windows-native environments (e.g. Wine);
    // this manual version only depends on HMACSHA256, which is universally
    // supported, so it works identically everywhere.
    private static byte[] Pbkdf2Sha256(string password, byte[] salt, int iterations, int outputLength)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(password));
        int hashLength = hmac.HashSize / 8;
        int blockCount = (int)Math.Ceiling((double)outputLength / hashLength);
        var output = new byte[outputLength];
        var block = new byte[salt.Length + 4];
        Buffer.BlockCopy(salt, 0, block, 0, salt.Length);

        int offset = 0;
        for (int i = 1; i <= blockCount; i++)
        {
            block[salt.Length] = (byte)(i >> 24);
            block[salt.Length + 1] = (byte)(i >> 16);
            block[salt.Length + 2] = (byte)(i >> 8);
            block[salt.Length + 3] = (byte)i;

            var u = hmac.ComputeHash(block);
            var t = (byte[])u.Clone();

            for (int j = 1; j < iterations; j++)
            {
                u = hmac.ComputeHash(u);
                for (int k = 0; k < t.Length; k++) t[k] ^= u[k];
            }

            int bytesToCopy = Math.Min(hashLength, outputLength - offset);
            Buffer.BlockCopy(t, 0, output, offset, bytesToCopy);
            offset += bytesToCopy;
        }

        return output;
    }

    private static (string hash, string salt) HashPassword(string password)
    {
        var saltBytes = RandomNumberGenerator.GetBytes(SaltSize);
        var hashBytes = Pbkdf2Sha256(password, saltBytes, Iterations, HashSize);
        return (Convert.ToBase64String(hashBytes), Convert.ToBase64String(saltBytes));
    }

    private static bool VerifyPassword(string password, string hash, string salt)
    {
        var saltBytes = Convert.FromBase64String(salt);
        var expected = Convert.FromBase64String(hash);
        var actual = Pbkdf2Sha256(password, saltBytes, Iterations, HashSize);
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    public static PublicUser SignUp(string name, string email, string password)
    {
        var cleanName = (name ?? "").Trim();
        var cleanEmail = (email ?? "").Trim().ToLowerInvariant();

        if (string.IsNullOrEmpty(cleanName) || string.IsNullOrEmpty(cleanEmail) || string.IsNullOrEmpty(password))
            throw new AuthException("Please fill in every field.");
        if (password.Length < 8)
            throw new AuthException("Password must be at least 8 characters.");

        var users = LoadUsers();
        if (users.Any(u => u.Email == cleanEmail))
            throw new AuthException("An account with this email already exists.");

        var (hash, salt) = HashPassword(password);
        var user = new User
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = cleanName,
            Email = cleanEmail,
            PasswordHash = hash,
            PasswordSalt = salt,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };

        users.Add(user);
        SaveUsers(users);
        return new PublicUser(user.Id, user.Name, user.Email);
    }

    public static PublicUser SignIn(string email, string password)
    {
        var cleanEmail = (email ?? "").Trim().ToLowerInvariant();
        var users = LoadUsers();
        var user = users.FirstOrDefault(u => u.Email == cleanEmail);

        if (user is null || !VerifyPassword(password ?? "", user.PasswordHash, user.PasswordSalt))
            throw new AuthException("Incorrect email or password.");

        return new PublicUser(user.Id, user.Name, user.Email);
    }
}
