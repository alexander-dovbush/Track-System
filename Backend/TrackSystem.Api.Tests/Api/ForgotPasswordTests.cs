// HttpStatusCode -> named status codes
using System.Net;
// PostAsJsonAsync / ReadFromJsonAsync -> send and read JSON
using System.Net.Http.Json;
// JsonElement -> read any JSON answer
using System.Text.Json;
// ResetTokens.Hash -> compare the emailed token with the stored hash
using TrackSystem.Api.Security;
// our factory
using TrackSystem.Api.Tests.Infrastructure;

namespace TrackSystem.Api.Tests.Api;

// shared API + test DB (collection "Api")
[Collection("Api")]
public class ForgotPasswordTests
{
    private const string Password = "Test1234!";
    // the one answer the endpoint always gives
    private const string SameMessage = "If the account exists, a reset link was sent to its email.";

    private readonly TestApiFactory _factory;

    public ForgotPasswordTests(TestApiFactory factory)
    {
        _factory = factory;
    }

    // eNum -> POST /api/auth/forgot-password -> response
    private async Task<HttpResponseMessage> PostForgotAsync(int eNum)
    {
        var client = _factory.CreateClient();
        return await client.PostAsJsonAsync("/api/auth/forgot-password", new { eNum });
    }

    // response -> asserts 200 + the same message
    private static async Task AssertSameAnswerAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(SameMessage, body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Forgot_ActiveEmployee_SendsEmailAndSavesOnlyTheHash()
    {
        var eNum = await _factory.CreateEmployeeAsync(Password);
        var email = (await _factory.GetEmployeeAsync(eNum))!.Email;

        await AssertSameAnswerAsync(await PostForgotAsync(eNum));

        // an email went to the employee's address, with a token in the link
        var token = _factory.LastResetTokenFor(email);
        Assert.NotNull(token);

        // one row: stores the token's HASH (not the token), unused, valid 30 min
        var row = Assert.Single(await _factory.GetResetTokensAsync(eNum));
        Assert.Equal(ResetTokens.Hash(token!), row.TokenHash);
        Assert.NotEqual(token, row.TokenHash);
        Assert.Null(row.UsedAt);
        Assert.Equal(30, (row.ExpiresAt - row.CreatedAt).TotalMinutes, precision: 1);
    }

    [Fact]
    public async Task Forgot_UnknownENum_SameAnswerNoEmail()
    {
        // emails "sent" so far (shared fake) -> must not grow
        var emailsBefore = _factory.Emails.Sent.Count;

        // number that doesn't exist in the test DB
        await AssertSameAnswerAsync(await PostForgotAsync(Random.Shared.Next(900000, 999999)));

        Assert.Equal(emailsBefore, _factory.Emails.Sent.Count);
    }

    // pending / inactive -> same answer, but no email and no row
    [Theory]
    [InlineData("pending")]
    [InlineData("inactive")]
    public async Task Forgot_NotActive_SameAnswerNoEmailNoRow(string status)
    {
        var eNum = await _factory.CreateEmployeeAsync(Password, status: status);
        var email = (await _factory.GetEmployeeAsync(eNum))!.Email;

        await AssertSameAnswerAsync(await PostForgotAsync(eNum));

        Assert.Null(_factory.LastResetTokenFor(email));
        Assert.Empty(await _factory.GetResetTokensAsync(eNum));
    }

    [Fact]
    public async Task Forgot_SecondRequest_OldLinkMarkedUsed()
    {
        var eNum = await _factory.CreateEmployeeAsync(Password);
        var email = (await _factory.GetEmployeeAsync(eNum))!.Email;

        // two requests -> two rows
        await PostForgotAsync(eNum);
        await PostForgotAsync(eNum);
        var rows = await _factory.GetResetTokensAsync(eNum);
        Assert.Equal(2, rows.Count);

        // first -> replaced (usedAt set); second -> still usable
        Assert.NotNull(rows[0].UsedAt);
        Assert.Null(rows[1].UsedAt);

        // the newest email's token -> belongs to the second (usable) row
        Assert.Equal(rows[1].TokenHash, ResetTokens.Hash(_factory.LastResetTokenFor(email)!));
    }
}