// HttpStatusCode -> named status codes
using System.Net;
// PostAsJsonAsync / ReadFromJsonAsync -> send and read JSON
using System.Net.Http.Json;
// JsonElement -> read any JSON answer
using System.Text.Json;
// our factory
using TrackSystem.Api.Tests.Infrastructure;

namespace TrackSystem.Api.Tests.Api;

// shared API + test DB (collection "Api")
[Collection("Api")]
public class ResetPasswordTests
{
    private const string OldPassword = "Test1234!";
    private const string NewPassword = "NewPass1!";
    // the one answer for every bad-link case
    private const string InvalidLink = "This link is invalid or has expired. Please request a new one.";

    private readonly TestApiFactory _factory;

    public ResetPasswordTests(TestApiFactory factory)
    {
        _factory = factory;
    }

    // eNum -> forgot-password -> the token from the newest email
    private async Task<string> RequestTokenAsync(int eNum)
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/forgot-password", new { eNum });
        var email = (await _factory.GetEmployeeAsync(eNum))!.Email;
        return _factory.LastResetTokenFor(email)!;
    }

    // token + new password -> POST /api/auth/reset-password -> response
    private async Task<HttpResponseMessage> PostResetAsync(string token, string newPassword)
    {
        var client = _factory.CreateClient();
        return await client.PostAsJsonAsync("/api/auth/reset-password", new { token, newPassword });
    }

    // eNum + password -> login status code (200 / 401)
    private async Task<HttpStatusCode> LoginStatusAsync(int eNum, string password)
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { eNum, password });
        return response.StatusCode;
    }

    // response -> asserts 400 + the given message
    private static async Task AssertBadRequestAsync(HttpResponseMessage response, string message)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(message, body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Reset_ValidLink_ChangesPasswordUnlocksAndUsesLink()
    {
        // first-login employee, then locked by 5 wrong passwords
        var eNum = await _factory.CreateEmployeeAsync(OldPassword, isFirstLogin: true);
        for (var i = 0; i < 5; i++)
            await LoginStatusAsync(eNum, "Wrong123!");
        Assert.Equal(HttpStatusCode.Unauthorized, await LoginStatusAsync(eNum, OldPassword));

        // reset with a valid link -> 200
        var token = await RequestTokenAsync(eNum);
        var response = await PostResetAsync(token, NewPassword);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // DB: unlocked, no forced change, link used
        var employee = (await _factory.GetEmployeeAsync(eNum))!;
        Assert.False(employee.IsFirstLogin);
        Assert.Equal(0, employee.FailedLoginAttempts);
        Assert.Null(employee.LockoutEnd);
        Assert.NotNull(Assert.Single(await _factory.GetResetTokensAsync(eNum)).UsedAt);

        // new password works, old one doesn't
        Assert.Equal(HttpStatusCode.OK, await LoginStatusAsync(eNum, NewPassword));
        Assert.Equal(HttpStatusCode.Unauthorized, await LoginStatusAsync(eNum, OldPassword));
    }

    [Fact]
    public async Task Reset_SameLinkTwice_SecondIs400()
    {
        var eNum = await _factory.CreateEmployeeAsync(OldPassword);
        var token = await RequestTokenAsync(eNum);

        // first use -> 200; second use of the same link -> 400 invalid
        Assert.Equal(HttpStatusCode.OK, (await PostResetAsync(token, NewPassword)).StatusCode);
        await AssertBadRequestAsync(await PostResetAsync(token, "Other123!"), InvalidLink);

        // the second try changed nothing -> the first new password still works
        Assert.Equal(HttpStatusCode.OK, await LoginStatusAsync(eNum, NewPassword));
    }

    // empty / made-up / right shape but never issued -> 400 invalid
    [Theory]
    [InlineData("")]
    [InlineData("abc123")]
    [InlineData("0000000000000000000000000000000000000000000000000000000000000000")]
    public async Task Reset_BadToken_Returns400(string token)
    {
        await AssertBadRequestAsync(await PostResetAsync(token, NewPassword), InvalidLink);
    }
    
    [Fact]
    public async Task Reset_ReplacedLink_Returns400_NewestWorks()
    {
        var eNum = await _factory.CreateEmployeeAsync(OldPassword);
        // two requests -> the first link is replaced by the second
        var first = await RequestTokenAsync(eNum);
        var second = await RequestTokenAsync(eNum);

        // old link -> 400 invalid; newest link -> 200
        await AssertBadRequestAsync(await PostResetAsync(first, NewPassword), InvalidLink);
        Assert.Equal(HttpStatusCode.OK, (await PostResetAsync(second, NewPassword)).StatusCode);
    }

    [Fact]
    public async Task Reset_ExpiredLink_Returns400_PasswordUnchanged()
    {
        var eNum = await _factory.CreateEmployeeAsync(OldPassword);
        var token = await RequestTokenAsync(eNum);
        // "30 minutes later"
        await _factory.ExpireResetTokensAsync(eNum);

        // expired -> 400 invalid; the old password still works
        await AssertBadRequestAsync(await PostResetAsync(token, NewPassword), InvalidLink);
        Assert.Equal(HttpStatusCode.OK, await LoginStatusAsync(eNum, OldPassword));
    }

    [Fact]
    public async Task Reset_WeakPassword_Returns400_LinkStillUsable()
    {
        var eNum = await _factory.CreateEmployeeAsync(OldPassword);
        var token = await RequestTokenAsync(eNum);

        // weak password -> 400 with a RULE message (not the invalid-link one)
        var response = await PostResetAsync(token, "abc");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotEqual(InvalidLink, body.GetProperty("message").GetString());

        // link not burned -> still unused, and a good password now works
        Assert.Null(Assert.Single(await _factory.GetResetTokensAsync(eNum)).UsedAt);
        Assert.Equal(HttpStatusCode.OK, (await PostResetAsync(token, NewPassword)).StatusCode);
    }

    [Fact]
    public async Task Reset_EmployeeDeactivatedAfterRequest_Returns400()
    {
        var eNum = await _factory.CreateEmployeeAsync(OldPassword);
        var token = await RequestTokenAsync(eNum);
        // admin deactivates the employee after the email was sent
        await _factory.SetStatusAsync(eNum, "inactive");

        // link no longer works for a non-active employee
        await AssertBadRequestAsync(await PostResetAsync(token, NewPassword), InvalidLink);
    }
}