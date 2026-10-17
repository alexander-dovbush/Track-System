// JsonWebTokenHandler -> reads a token text into an object (same library the API uses)
using Microsoft.IdentityModel.JsonWebTokens;
// our factory
using TrackSystem.Api.Tests.Infrastructure;

namespace TrackSystem.Api.Tests.Api;

// shared API + test DB (collection "Api")
[Collection("Api")]
public class TokenTests
{
    // one valid password for the test employees
    private const string Password = "Test1234!";

    // the shared factory
    private readonly TestApiFactory _factory;

    // xUnit -> hands us the shared factory
    public TokenTests(TestApiFactory factory)
    {
        _factory = factory;
    }

    // runs twice: first-login employee -> "true", normal employee -> "false"
    [Theory]
    [InlineData(true, "true")]
    [InlineData(false, "false")]
    public async Task Login_Token_HasOurClaimsAnd30MinLifetime(bool isFirstLogin, string expectedMustChange)
    {
        // employee with role Manager (not the default) -> proves "role" really comes from the DB
        var eNum = await _factory.CreateEmployeeAsync(Password, isFirstLogin: isFirstLogin, role: "Manager");
        var token = await _factory.LoginAsync(eNum, Password);

        // token text -> readable object (only decodes; does NOT check the signature)
        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(token);

        // our 3 claims -> the employee's real data
        Assert.Equal(eNum.ToString(), jwt.GetClaim("sub").Value);
        Assert.Equal("Manager", jwt.GetClaim("role").Value);
        Assert.Equal(expectedMustChange, jwt.GetClaim("mustChangePassword").Value);

        // who made it / who it's for -> from appsettings.json
        Assert.Equal("TrackSystem.Api", jwt.Issuer);
        Assert.Contains("TrackSystem.Client", jwt.Audiences);

        // exp - iat -> 30 minutes (1800 s); exp is computed a moment before iat, so allow 1798-1800
        var lifetime = jwt.ValidTo - jwt.IssuedAt;
        Assert.InRange(lifetime.TotalSeconds, 1798, 1800);
    }
}