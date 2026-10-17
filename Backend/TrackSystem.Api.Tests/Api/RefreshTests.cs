// HttpStatusCode -> named status codes
using System.Net;
// AuthenticationHeaderValue -> "Authorization: Bearer <token>"
using System.Net.Http.Headers;
// ReadFromJsonAsync -> read the JSON answer
using System.Net.Http.Json;
// JsonElement -> read any JSON answer
using System.Text.Json;
// JsonWebTokenHandler -> read the new token's claims
using Microsoft.IdentityModel.JsonWebTokens;
// our factory
using TrackSystem.Api.Tests.Infrastructure;

namespace TrackSystem.Api.Tests.Api;

// shared API + test DB (collection "Api")
[Collection("Api")]
public class RefreshTests
{
    // one valid password for the test employees
    private const string Password = "Test1234!";

    // the shared factory
    private readonly TestApiFactory _factory;

    // xUnit -> hands us the shared factory
    public RefreshTests(TestApiFactory factory)
    {
        _factory = factory;
    }

    // token (or null = no header) -> POST /api/auth/refresh with no body -> response
    private async Task<HttpResponseMessage> PostRefreshAsync(string? token)
    {
        var client = _factory.CreateClient();
        if (token is not null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.PostAsync("/api/auth/refresh", null);
    }

        // runs twice: first-login token and normal token -> both may refresh ("PasswordChange" rule)
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Refresh_ActiveEmployee_Returns200WithFreshToken(bool isFirstLogin)
    {
        var eNum = await _factory.CreateEmployeeAsync(Password, isFirstLogin: isFirstLogin);
        var token = await _factory.LoginAsync(eNum, Password);

        // valid token, employee still active -> 200
        var response = await PostRefreshAsync(token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // body -> mustChangePassword from the DB + a new token
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(isFirstLogin, body.GetProperty("mustChangePassword").GetBoolean());
        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(body.GetProperty("token").GetString()!);

        // new token -> same person, a full 30 minutes from now
        Assert.Equal(eNum.ToString(), jwt.GetClaim("sub").Value);
        Assert.InRange((jwt.ValidTo - DateTime.UtcNow).TotalMinutes, 29, 30);
    }

    [Fact]
    public async Task Refresh_NoToken_Returns401()
    {
        // no Authorization header -> 401
        var response = await PostRefreshAsync(null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_EmployeeDeactivatedAfterLogin_Returns401()
    {
        // logged in while active -> valid token
        var eNum = await _factory.CreateEmployeeAsync(Password);
        var token = await _factory.LoginAsync(eNum, Password);

        // admin deactivates the employee (token itself is still valid)
        await _factory.SetStatusAsync(eNum, "inactive");

        // refresh reads the DB -> not active -> 401 (React sends the user to Login)
        var response = await PostRefreshAsync(token);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}