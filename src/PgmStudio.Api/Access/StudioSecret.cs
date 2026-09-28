using System.Security.Cryptography;
using System.Text;

namespace PgmStudio.Api.Access;

/// <summary>
/// A secret the studio hands out once and keeps only the hash of — an invitation's code and a token alike:
/// 32 random bytes as base64url, stored as their SHA-256 in hex, so the table holding them holds nothing a
/// request could be made with.
/// </summary>
public static class StudioSecret
{
    /// <summary>A fresh secret, with <paramref name="prefix"/> in front of it, and the hash it is stored under.</summary>
    public static (string Secret, string Hash) New(string prefix = "")
    {
        var secret = prefix + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return (secret, HashOf(secret));
    }

    public static string HashOf(string secret) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));
}
