// UseEnvironment / UseSetting -> change the API's setup before it starts
using Microsoft.AspNetCore.Hosting;
// WebApplicationFactory<T> -> starts our API in memory
using Microsoft.AspNetCore.Mvc.Testing;

namespace TrackSystem.Api.Tests.Infrastructure;

// a SECOND in-memory API with limit 3 -> its own rate-limit counters, the shared API is not affected
public class RateLimitApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // "RateLimitTesting" -> appsettings.RateLimitTesting.json (FinalProject_Test, limit 3)
        builder.UseEnvironment("RateLimitTesting");

        // same random test key as the shared factory
        builder.UseSetting("Jwt:Key", TestApiFactory.JwtKey);
    }
}