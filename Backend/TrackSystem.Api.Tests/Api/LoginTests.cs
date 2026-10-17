// HttpStatusCode -> named status codes
using System.Net;
// PostAsJsonAsync / ReadFromJsonAsync -> send and read JSON
using System.Net.Http.Json;
// JsonElement -> read any JSON answer without a C# class for it
using System.Text.Json;
// our factory
using TrackSystem.Api.Tests.Infrastructure;

namespace TrackSystem.Api.Tests.Api;

// shared API + test DB (collection "Api")
[Collection("Api")]
public class LoginTests
{
    // one valid password for the test employees (passes PasswordRules)
    private const string Password = "Test1234!";

    // the shared factory
    private readonly TestApiFactory _factory;

    // xUnit -> hands us the shared factory
    public LoginTests(TestApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Login_CorrectPassword_Returns200WithToken()
    {
        // new active employee -> its eNum
        var eNum = await _factory.CreateEmployeeAsync(Password);
        var client = _factory.CreateClient();

        // POST /api/auth/login {"eNum":..., "password":"Test1234!"} -> response
        var response = await client.PostAsJsonAsync("/api/auth/login", new { eNum, password = Password });

        // 200 expected
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // body JSON -> same eNum back + a non-empty token
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(eNum, body.GetProperty("eNum").GetInt32());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("token").GetString()));
        // LoginLogs -> exactly 1 row for this eNum (Single fails if 0 or 2+), and it's a success
        var log = Assert.Single(await _factory.GetLoginLogsAsync(eNum));
        Assert.True(log.Success);
    }

    // eNum + password -> POST /api/auth/login -> response
    private async Task<HttpResponseMessage> PostLoginAsync(int eNum, string password)
    {
        var client = _factory.CreateClient();
        return await client.PostAsJsonAsync("/api/auth/login", new { eNum, password });
    }

    // response -> must be 401 with the ONE generic message (else the test fails)
    private static async Task AssertGenericFailureAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Invalid employee number or password", body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401Generic()
    {
        // real employee + wrong password -> generic 401
        var eNum = await _factory.CreateEmployeeAsync(Password);
        var response = await PostLoginAsync(eNum, "Wrong1234!");
        await AssertGenericFailureAsync(response);
        // LoginLogs -> exactly 1 row for this eNum, and it's a failure
        var log = Assert.Single(await _factory.GetLoginLogsAsync(eNum));
        Assert.False(log.Success);
    }

    [Fact]
    public async Task Login_UnknownENum_Returns401Generic()
    {
        // random number far above the test employees' numbers -> no such employee
        // (random, not 999999: the DB is shared, other tests may log unknown numbers too)
        var unknownENum = 900_000 + Random.Shared.Next(99_999);

        // unknown eNum -> same generic 401
        var response = await PostLoginAsync(unknownENum, Password);
        await AssertGenericFailureAsync(response);

        // option B: the unknown number is still logged -> exactly 1 failure row
        var log = Assert.Single(await _factory.GetLoginLogsAsync(unknownENum));
        Assert.False(log.Success);
    }

    [Fact]
    public async Task Login_EmptyPassword_Returns401Generic()
    {
        // real employee + "" -> generic 401 (BCrypt is never reached)
        var eNum = await _factory.CreateEmployeeAsync(Password);
        var response = await PostLoginAsync(eNum, "");
        await AssertGenericFailureAsync(response);
        // LoginLogs -> exactly 1 row for this eNum, and it's a failure
        var log = Assert.Single(await _factory.GetLoginLogsAsync(eNum));
        Assert.False(log.Success);
    }

        [Fact]
    public async Task Login_FiveWrongPasswords_LocksAccountSilently()
    {
        var eNum = await _factory.CreateEmployeeAsync(Password);

        // 5 wrong passwords -> each one the same generic 401
        for (var i = 0; i < 5; i++)
            await AssertGenericFailureAsync(await PostLoginAsync(eNum, "Wrong1234!"));

        // DB now -> employee row exists, LockoutEnd is set
        var employee = await _factory.GetEmployeeAsync(eNum);
        Assert.NotNull(employee);
        Assert.NotNull(employee.LockoutEnd);

        // LockoutEnd (UTC) - now (UTC) -> about 15 minutes left
        var minutesLeft = (employee.LockoutEnd.GetValueOrDefault() - DateTime.UtcNow).TotalMinutes;
        Assert.InRange(minutesLeft, 14, 15);

        // counter -> back to 0 when the lock starts
        Assert.Equal(0, employee.FailedLoginAttempts);

        // correct password while locked -> still the generic 401 (the lock stays silent)
        await AssertGenericFailureAsync(await PostLoginAsync(eNum, Password));

        // LoginLogs -> 6 rows (5 wrong + 1 while locked), all failures
        var logs = await _factory.GetLoginLogsAsync(eNum);
        Assert.Equal(6, logs.Count);
        Assert.All(logs, l => Assert.False(l.Success));
    }

        // runs twice: once "pending", once "inactive"
    [Theory]
    [InlineData("pending")]
    [InlineData("inactive")]
    public async Task Login_NotActiveStatus_Returns401Generic(string status)
    {
        // employee with this status -> its eNum
        var eNum = await _factory.CreateEmployeeAsync(Password, status: status);

        // the CORRECT password, but not active -> generic 401 (no hint about the status)
        await AssertGenericFailureAsync(await PostLoginAsync(eNum, Password));

        // counter -> still 0 (a right password is not a wrong-password attempt)
        var employee = await _factory.GetEmployeeAsync(eNum);
        Assert.NotNull(employee);
        Assert.Equal(0, employee.FailedLoginAttempts);

        // LoginLogs -> exactly 1 row, a failure
        var log = Assert.Single(await _factory.GetLoginLogsAsync(eNum));
        Assert.False(log.Success);
    }
}