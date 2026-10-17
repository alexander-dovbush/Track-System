// RandomNumberGenerator (secure random), SHA256
using System.Security.Cryptography;
// Encoding.UTF8 -> text to bytes
using System.Text;

namespace TrackSystem.Api.Security;

// Reset-link tokens: make one, hash one (no state -> static)
public static class ResetTokens
{
    // nothing -> (Token for the email link, Hash for the DB)
    public static (string Token, string Hash) Create()
    {
        // 32 bytes from the OS's secure random generator (unguessable)
        var bytes = RandomNumberGenerator.GetBytes(32);
        // bytes -> 64 hex chars "a3f0..." (safe inside a URL, no escaping needed)
        var token = Convert.ToHexStringLower(bytes);
        // token -> its hash; both returned together
        return (token, Hash(token));
    }

    // token text -> SHA-256 -> 64 lowercase hex chars (fits tokenHash char(64))
    public static string Hash(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}