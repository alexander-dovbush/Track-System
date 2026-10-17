// Regex -> check the "64 lowercase hex chars" shape
using System.Text.RegularExpressions;
// ResetTokens -> the class under test
using TrackSystem.Api.Security;

namespace TrackSystem.Api.Tests.Security;

// unit tests: only the ResetTokens class, no API, no DB
public class ResetTokensTests
{
    // token -> exactly 64 chars of 0-9 a-f (32 random bytes as lowercase hex)
    [Fact]
    public void Create_TokenIs64LowercaseHex()
    {
        var (token, _) = ResetTokens.Create();
        Assert.Matches(new Regex("^[0-9a-f]{64}$"), token);
    }

    // Create's hash -> equals Hash(token) (so the DB row can be found from the link)
    [Fact]
    public void Create_HashMatchesHashOfToken()
    {
        var (token, hash) = ResetTokens.Create();
        Assert.Equal(ResetTokens.Hash(token), hash);
    }

    // two calls -> two different tokens (random, never repeated)
    [Fact]
    public void Create_TwoCallsGiveDifferentTokens()
    {
        var (first, _) = ResetTokens.Create();
        var (second, _) = ResetTokens.Create();
        Assert.NotEqual(first, second);
    }

    // "abc" -> the official SHA-256 test value (proves it's real SHA-256, lowercase hex)
    [Fact]
    public void Hash_KnownValue()
    {
        Assert.Equal(
            "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad",
            ResetTokens.Hash("abc"));
    }
}