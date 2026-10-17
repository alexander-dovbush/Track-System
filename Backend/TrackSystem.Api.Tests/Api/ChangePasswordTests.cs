// HttpStatusCode -> named status codes
using System.Net;
// AuthenticationHeaderValue -> "Authorization: Bearer <token>"
using System.Net.Http.Headers;
// PostAsJsonAsync / ReadFromJsonAsync -> send and read JSON
using System.Net.Http.Json;
// JsonElement -> read the JSON answer
using System.Text.Json;
// JsonWebTokenHandler -> read the claims of the new token
using Microsoft.IdentityModel.JsonWebTokens;
// our factory
using TrackSystem.Api.Tests.Infrastructure;

namespace TrackSystem.Api.Tests.Api;

// shared API + test DB (collection "Api")
[Collection("Api")]
public class ChangePasswordTests
{
    // the employee's real (current) password
    private const string Password = "Test1234!";

    // the shared factory
    private readonly TestApiFactory _factory;

    // xUnit -> hands us the shared factory
    public ChangePasswordTests(TestApiFactory factory)
    {
        _factory = factory;
    }

    // token + current + new -> POST /api/auth/change-password -> response
    private async Task<HttpResponseMessage> PostChangeAsync(string token, string currentPassword, string newPassword)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.PostAsJsonAsync("/api/auth/change-password", new { currentPassword, newPassword });
    }

        // each row: what the user types -> the 400 message we expect
    [Theory]
    // wrong current password
    [InlineData("Wrong1234!", "NewPass1!", "Current password is incorrect")]
    // empty current password
    [InlineData("", "NewPass1!", "Current password is incorrect")]
    // new = current
    [InlineData(Password, Password, "New password must be different from the current password")]
    // new password breaks a rule (too short) -> PasswordRules' message
    [InlineData(Password, "Ab1!", "Password must be at least 8 characters")]
    public async Task ChangePassword_BadInput_Returns400AndSavesNothing(
        string currentPassword, string newPassword, string expectedMessage)
    {
        // first-login employee -> token with mustChangePassword "true"
        var eNum = await _factory.CreateEmployeeAsync(Password, isFirstLogin: true);
        var token = await _factory.LoginAsync(eNum, Password);

        // bad input -> 400 (not 401: React would log the user out; not 403: this endpoint allows "true")
        var response = await PostChangeAsync(token, currentPassword, newPassword);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // body -> exactly the expected message (React shows it to the user)
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(expectedMessage, body.GetProperty("message").GetString());

        // DB -> nothing changed: old password still matches the hash, still first login
        var employee = await _factory.GetEmployeeAsync(eNum);
        Assert.NotNull(employee);
        Assert.True(BCrypt.Net.BCrypt.Verify(Password, employee.Password));
        Assert.True(employee.IsFirstLogin);
    }

        [Fact]
    public async Task ChangePassword_Valid_Returns200NewTokenAndUpdatesDb()
    {
        // new password that passes all rules
        const string newPassword = "NewPass1!";

        // first-login employee -> old token (mustChangePassword "true")
        var eNum = await _factory.CreateEmployeeAsync(Password, isFirstLogin: true);
        var oldToken = await _factory.LoginAsync(eNum, Password);

        // correct current + valid new -> 200
        var response = await PostChangeAsync(oldToken, Password, newPassword);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // body -> message, mustChangePassword false, a new token
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Password changed", body.GetProperty("message").GetString());
        Assert.False(body.GetProperty("mustChangePassword").GetBoolean());
        var newToken = body.GetProperty("token").GetString()!;

        // new token's claim -> "false" (React may now open normal pages)
        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(newToken);
        Assert.Equal("false", jwt.GetClaim("mustChangePassword").Value);

        // DB -> new hash matches the new password, not the old one; first-login flag off
        var employee = await _factory.GetEmployeeAsync(eNum);
        Assert.NotNull(employee);
        Assert.True(BCrypt.Net.BCrypt.Verify(newPassword, employee.Password));
        Assert.False(BCrypt.Net.BCrypt.Verify(Password, employee.Password));
        Assert.False(employee.IsFirstLogin);

        // login with the NEW password -> works (LoginAsync fails the test if not 200)
        await _factory.LoginAsync(eNum, newPassword);

        // login with the OLD password -> 401
        var client = _factory.CreateClient();
        var oldLogin = await client.PostAsJsonAsync("/api/auth/login", new { eNum, password = Password });
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);

        // NEW token -> /api/employees opens (200)
        var employeesClient = _factory.CreateClient();
        employeesClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", newToken);
        Assert.Equal(HttpStatusCode.OK, (await employeesClient.GetAsync("/api/employees")).StatusCode);

        // OLD token -> still "true" inside -> 403 (that's why React must swap to the new token)
        var oldClient = _factory.CreateClient();
        oldClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", oldToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await oldClient.GetAsync("/api/employees")).StatusCode);
    }
}