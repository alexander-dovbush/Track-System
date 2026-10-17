// HttpStatusCode -> named status codes
using System.Net;
// PostAsJsonAsync / ReadFromJsonAsync -> send and read JSON
using System.Net.Http.Json;
// JsonElement -> read the 429 answer
using System.Text.Json;
// both factories
using TrackSystem.Api.Tests.Infrastructure;

namespace TrackSystem.Api.Tests.Api;

// in collection "Api" -> the shared factory has created FinalProject_Test before this runs
// IClassFixture<RateLimitApiFactory> -> plus our own low-limit API for this class only
[Collection("Api")]
public class RateLimitTests : IClassFixture<RateLimitApiFactory>
{
    // the low-limit API
    private readonly RateLimitApiFactory _limitedApi;

    // shared factory -> used only to read LoginLogs; low-limit factory -> sends the requests
    private readonly TestApiFactory _shared;

    public RateLimitTests(RateLimitApiFactory limitedApi, TestApiFactory shared)
    {
        _limitedApi = limitedApi;
        _shared = shared;
    }

    [Fact]
    public async Task Login_FourthAttemptInOneMinute_Returns429()
    {
        var client = _limitedApi.CreateClient();

        // unknown random eNum + empty password -> fast 401 (no BCrypt), still counted by the limiter
        var eNum = 900_000 + Random.Shared.Next(99_999);
        var request = new { eNum, password = "" };

        // attempts 1-3 -> under the limit -> the endpoint runs -> 401
        for (var i = 0; i < 3; i++)
        {
            var allowed = await client.PostAsJsonAsync("/api/auth/login", request);
            Assert.Equal(HttpStatusCode.Unauthorized, allowed.StatusCode);
        }

        // attempt 4 -> over the limit -> rejected before the endpoint -> 429 + our message
        var blocked = await client.PostAsJsonAsync("/api/auth/login", request);
        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        var body = await blocked.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Too many attempts. Please wait a minute and try again.", body.GetProperty("message").GetString());

        // LoginLogs -> only 3 rows: the 4th request never reached the endpoint
        Assert.Equal(3, (await _shared.GetLoginLogsAsync(eNum)).Count);
    }
}