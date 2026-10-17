// RandomNumberGenerator -> random bytes for a test-only JWT key
using System.Security.Cryptography;
// UseEnvironment / UseSetting -> change the API's setup before it starts
using Microsoft.AspNetCore.Hosting;
// WebApplicationFactory<T> -> starts our API in memory
using Microsoft.AspNetCore.Mvc.Testing;
// GetDbConnection -> read the DB name from the connection
using Microsoft.EntityFrameworkCore;
// CreateScope, GetRequiredService -> get an AppDbContext from the API's services
using Microsoft.Extensions.DependencyInjection;
// our AppDbContext
using TrackSystem.Api.Data;
// Employee model -> to build a test employee row
using TrackSystem.Api.Models;
// PostAsJsonAsync / ReadFromJsonAsync -> send and read JSON
using System.Net.Http.Json;
// JsonElement -> read the login answer
using System.Text.Json;
// ConfigureTestServices -> replace services after Program.cs registered them
using Microsoft.AspNetCore.TestHost;
// RemoveAll -> delete a registration
using Microsoft.Extensions.DependencyInjection.Extensions;
// IEmailSender -> the registration we replace
using TrackSystem.Api.Services;
// Regex -> pull the token out of the email's link
using System.Text.RegularExpressions;

// test helpers live here (not tests themselves)
namespace TrackSystem.Api.Tests.Infrastructure;

// our API (Program) -> started in memory, with test settings
// IAsyncLifetime -> xUnit calls InitializeAsync once before the tests
public class TestApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // 64 random bytes -> base64 text: a new key every run, never saved in any file
    public static readonly string JwtKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    
    // the fake mailbox for this API -> tests read sent emails from here
    public FakeEmailSender Emails { get; } = new();

    // runs once before the API starts -> our chance to change its settings
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // environment "Testing" -> appsettings.Testing.json loads (FinalProject_Test, limit 1000), User Secrets don't
        builder.UseEnvironment("Testing");

        // random key -> "Jwt:Key", the same name Program.cs reads
        builder.UseSetting("Jwt:Key", JwtKey);
        
        // Program.cs registered SmtpEmailSender -> removed; our Emails fake used instead (no real mail in tests)
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Emails);
        });
    }

        // before the first test -> a fresh, empty FinalProject_Test with our tables
    public async Task InitializeAsync()
    {
        // new scope -> a fresh AppDbContext from the API (same setup as in Program.cs)
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // connection -> DB name; anything but FinalProject_Test -> stop, nothing is dropped
        var dbName = db.Database.GetDbConnection().Database;
        if (dbName != "FinalProject_Test")
            throw new InvalidOperationException($"Tests may only use FinalProject_Test, not '{dbName}'");

        // old test DB (if any) -> dropped
        await db.Database.EnsureDeletedAsync();
        // new test DB -> created with Employees + LoginLogs from our models
        await db.Database.EnsureCreatedAsync();
    }

    // after the tests -> nothing extra (the factory shuts the API down by itself)
    Task IAsyncLifetime.DisposeAsync() => Task.CompletedTask;

        // password (+ optional status / first-login / role) -> new Employee row in FinalProject_Test -> its eNum
    public async Task<int> CreateEmployeeAsync(
        string password, string status = "active", bool isFirstLogin = false, string role = "Employee")
    {
        // fresh AppDbContext from the API's services
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // random 8 chars -> unique email / user name for every test employee
        var id = Guid.NewGuid().ToString("N")[..8];

        var employee = new Employee
        {
            FirstName = "Test",
            LastName = "User",
            Email = $"{id}@test.local",
            UserName = $"user_{id}",
            // plain password -> real BCrypt hash, exactly how the API stores it
            Password = BCrypt.Net.BCrypt.HashPassword(password),
            Role = role,
            Status = status,
            IsFirstLogin = isFirstLogin
        };

        // row -> saved; SQL Server gives the new eNum, EF writes it back into employee.ENum
        db.Employees.Add(employee);
        await db.SaveChangesAsync();
        return employee.ENum;
    }

    // eNum -> all LoginLogs rows for that number, oldest first
    public async Task<List<LoginLog>> GetLoginLogsAsync(int eNum)
    {
        // fresh AppDbContext from the API's services
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // dbo.LoginLogs WHERE eNum = ... ORDER BY LogID -> list
        return await db.LoginLogs.Where(l => l.ENum == eNum).OrderBy(l => l.LogID).ToListAsync();
    }

        // eNum -> the Employee row as it is NOW in the test DB (null if missing)
    public async Task<Employee?> GetEmployeeAsync(int eNum)
    {
        // fresh AppDbContext -> reads the real DB state, nothing cached from earlier calls
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Employees.FirstOrDefaultAsync(e => e.ENum == eNum);
    }

    // eNum + password -> POST /api/auth/login -> the token text (not 200 -> the test fails here)
    public async Task<string> LoginAsync(int eNum, string password)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { eNum, password });

        // 4xx / 5xx -> exception -> the test stops with the status in the message
        response.EnsureSuccessStatusCode();

        // body -> "token" field
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("token").GetString()!;
    }

    // eNum + status -> that employee's Status changed in the test DB (e.g. an admin deactivates them)
    public async Task SetStatusAsync(int eNum, string status)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // row must exist -> FirstAsync throws if not (the test fails clearly)
        var employee = await db.Employees.FirstAsync(e => e.ENum == eNum);
        employee.Status = status;
        await db.SaveChangesAsync();
    }
    
    // email address -> token from the NEWEST reset email sent to it (null if none)
    public string? LastResetTokenFor(string email)
    {
        // all emails to this address -> the newest one's HTML body (null if none)
        var body = Emails.Sent.Where(m => m.To == email).Select(m => m.HtmlBody).LastOrDefault();
        if (body is null)
            return null;
        // "...?token=a3f0..." -> the 64 hex chars after "token="
        var match = Regex.Match(body, "token=([0-9a-f]{64})");
        return match.Success ? match.Groups[1].Value : null;
    }
    
    // eNum -> all PasswordResetTokens rows for that employee, oldest first
    public async Task<List<PasswordResetToken>> GetResetTokensAsync(int eNum)
    {
        // fresh AppDbContext -> real DB state
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.PasswordResetTokens.Where(t => t.ENum == eNum).OrderBy(t => t.TokenID).ToListAsync();
    }
    
    // eNum -> all that employee's reset links expire 1 minute ago (simulates "30 minutes passed")
    public async Task ExpireResetTokensAsync(int eNum)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rows = await db.PasswordResetTokens.Where(t => t.ENum == eNum).ToListAsync();
        foreach (var row in rows)
            row.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();
    }
}