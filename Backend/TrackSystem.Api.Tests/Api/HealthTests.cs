// HttpStatusCode -> named status codes (OK = 200)
using System.Net;
// our factory
using TrackSystem.Api.Tests.Infrastructure;

namespace TrackSystem.Api.Tests.Api;

// member of collection "Api" -> uses the shared factory (one API, one test DB)
[Collection("Api")]
public class HealthTests
{
    // the shared factory -> kept for the tests below
    private readonly TestApiFactory _factory;

    // xUnit creates the factory once -> hands it to us here
    public HealthTests(TestApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Health_ReturnsOk()
    {
        // factory -> HttpClient whose requests go straight into the in-memory API
        var client = _factory.CreateClient();

        // GET /api/health -> response (status + body)
        var response = await client.GetAsync("/api/health");

        // 200 expected
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}