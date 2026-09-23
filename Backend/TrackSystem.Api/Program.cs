// brings in EF Core tools (UseSqlServer, CanConnectAsync)
using Microsoft.EntityFrameworkCore;
// brings in our AppDbContext class from the Data folder
using TrackSystem.Api.Data;

// input: settings (appsettings.json) → output: builder = the setup area
var builder = WebApplication.CreateBuilder(args);

// input: connection string "DefaultConnection" from appsettings.json
// output: server now knows HOW to create an AppDbContext connected to SQL Server
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// input: the setup → output: app = the ready server
var app = builder.Build();

// input: GET /api/health → output: {"status":"ok"}   (from step 1, unchanged)
app.MapGet("/api/health", () => new { status = "ok" });

// input: GET /api/db-check → the server hands us "db" (a ready AppDbContext)
app.MapGet("/api/db-check", async (AppDbContext db) =>
{
    // input: nothing → asks SQL Server "are you there?"
    // output: true (connected) or false (failed)
    var canConnect = await db.Database.CanConnectAsync();

    // input: true/false → output: {"database":"connected"} or {"database":"failed"}
    return new { database = canConnect ? "connected" : "failed" };
});

// GET /api/employees -> list of all employees as JSON (no passwords)
app.MapGet("/api/employees", async (AppDbContext db) =>
{
    // dbo.Employees rows -> only the safe columns (Password is left out)
    var employees = await db.Employees
        .Select(e => new
        {
            e.ENum,
            e.FirstName,
            e.LastName,
            e.Email,
            e.UserName,
            e.Position,
            e.Role,
            e.TeamNum,
            e.Status,
            e.IsFirstLogin,
            e.FailedLoginAttempts,
            e.LockoutEnd
        })
        // run the query -> List of employees
        .ToListAsync();

    // list -> HTTP 200 with JSON body
    return Results.Ok(employees);
});

// starts the server → waits for requests (stop with Ctrl+C)
app.Run();