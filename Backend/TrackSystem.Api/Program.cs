// brings in EF Core tools (UseSqlServer, CanConnectAsync)
using Microsoft.EntityFrameworkCore;
// brings in our AppDbContext class from the Data folder
using TrackSystem.Api.Data;
// lets Program.cs see LoginRequest from the Dtos folder
using TrackSystem.Api.Dtos;
// lets Program.cs see LoginLog from the Models folder
using TrackSystem.Api.Models;

// input: settings (appsettings.json) → output: builder = the setup area
var builder = WebApplication.CreateBuilder(args);

// TEMPORARY: config -> prints whether the JWT settings were found (never print the key itself)
Console.WriteLine($"Jwt:Key found: {!string.IsNullOrEmpty(builder.Configuration["Jwt:Key"])}, Issuer: {builder.Configuration["Jwt:Issuer"]}");

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

// POST /api/auth/login, body {"eNum":1,"password":"..."} -> 200 (ok), 401 (generic error) or 423 (locked)
// every attempt -> one row in dbo.LoginLogs
app.MapPost("/api/auth/login", async (LoginRequest request, AppDbContext db, HttpContext http) =>
{
    // one fixed error answer -> same text whether the eNum or the password is wrong
    var loginFailed = Results.Json(
        new { message = "Invalid employee number or password" },
        statusCode: StatusCodes.Status401Unauthorized);

    // locked answer -> status 423 so React can tell it apart from a wrong password
    var accountLocked = Results.Json(
        new { message = "Account is locked. Try again in 15 minutes." },
        statusCode: StatusCodes.Status423Locked);

    // "Asia/Jerusalem" -> Israel time zone rules (summer/winter switch is automatic)
    var israelZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Jerusalem");

    // true/false -> one LoginLog row added to EF's "to save" list (written on the next SaveChangesAsync)
    void AddLog(bool success) => db.LoginLogs.Add(new LoginLog
    {
        // the eNum that was typed -> may not exist in Employees (option B)
        ENum = request.ENum,
        // UTC now -> Israel time now
        LoginDate = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, israelZone),
        // caller's IP -> text like "::1" or "192.168.1.5" (null if unknown)
        IpAddress = http.Connection.RemoteIpAddress?.ToString(),
        // true = logged in, false = any failure
        Success = success
    });

    // empty password -> log failure, save, generic error
    if (string.IsNullOrEmpty(request.Password))
    {
        AddLog(false);
        await db.SaveChangesAsync();
        return loginFailed;
    }

    // eNum -> the matching Employee row, or null if no such employee
    var employee = await db.Employees.FirstOrDefaultAsync(e => e.ENum == request.ENum);

    // no such employee -> log failure (eNum still recorded), save, generic error
    if (employee is null)
    {
        AddLog(false);
        await db.SaveChangesAsync();
        return loginFailed;
    }

    // lockoutEnd later than now -> log failure, save, locked (password not checked)
    if (employee.LockoutEnd > DateTime.UtcNow)
    {
        AddLog(false);
        await db.SaveChangesAsync();
        return accountLocked;
    }

    // typed password doesn't match the stored hash -> count this failure
    if (!BCrypt.Net.BCrypt.Verify(request.Password, employee.Password))
    {
        // failedLoginAttempts -> +1
        employee.FailedLoginAttempts++;
        // log row -> "to save" list
        AddLog(false);

        // 5th failure -> lock for 15 minutes, counter back to 0
        if (employee.FailedLoginAttempts >= 5)
        {
            // now + 15 minutes (UTC) -> lockoutEnd
            employee.LockoutEnd = DateTime.UtcNow.AddMinutes(15);
            // counter -> 0
            employee.FailedLoginAttempts = 0;
            // employee changes + log row -> saved together
            await db.SaveChangesAsync();
            // -> 423 locked
            return accountLocked;
        }

        // new count + log row -> saved together
        await db.SaveChangesAsync();
        // -> generic 401
        return loginFailed;
    }

    // correct password -> counter 0, no lock
    employee.FailedLoginAttempts = 0;
    employee.LockoutEnd = null;
    // log success -> "to save" list
    AddLog(true);
    // employee changes + log row -> saved together
    await db.SaveChangesAsync();

    // -> 200 with basic info (no password, no hash)
    return Results.Ok(new { message = "Login successful", employee.ENum, employee.IsFirstLogin });
});

// starts the server → waits for requests (stop with Ctrl+C)
app.Run();