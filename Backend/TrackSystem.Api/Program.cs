// brings in EF Core tools (UseSqlServer, CanConnectAsync)
using Microsoft.EntityFrameworkCore;
// brings in our AppDbContext class from the Data folder
using TrackSystem.Api.Data;
// lets Program.cs see LoginRequest from the Dtos folder
using TrackSystem.Api.Dtos;
// lets Program.cs see LoginLog from the Models folder
using TrackSystem.Api.Models;
// Claim, ClaimsIdentity -> the data pieces inside a token
using System.Security.Claims;
// JsonWebTokenHandler -> creates the token text
using Microsoft.IdentityModel.JsonWebTokens;
// SymmetricSecurityKey, SigningCredentials, SecurityTokenDescriptor -> key + signing tools
using Microsoft.IdentityModel.Tokens;

// input: settings (appsettings.json + User Secrets) → output: builder = the setup area
var builder = WebApplication.CreateBuilder(args);

// "Jwt:Key" from User Secrets -> text; missing -> stop the server with a clear message
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is missing. Run: dotnet user-secrets set \"Jwt:Key\" \"<key>\"");

// base64 text -> the 64 random bytes -> a key object that can sign tokens
var signingKey = new SymmetricSecurityKey(Convert.FromBase64String(jwtKey));

// "Jwt:Issuer" / "Jwt:Audience" from appsettings.json -> who made the token / who it's for
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];

// "Jwt:ExpiresMinutes" from appsettings.json -> 60 (as a number)
var jwtExpiresMinutes = builder.Configuration.GetValue<int>("Jwt:ExpiresMinutes");

// input: connection string "DefaultConnection" from appsettings.json
// output: server now knows HOW to create an AppDbContext connected to SQL Server
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// registers the JWT guard, named "Bearer" -> checks tokens sent as "Authorization: Bearer <token>"
builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        // keep claim names as we wrote them ("sub", "role") -> no renaming to long .NET names
        options.MapInboundClaims = false;

        // the rules the guard uses to check every token
        options.TokenValidationParameters = new TokenValidationParameters
        {
            // signature must be valid -> token was made by us and not changed
            ValidateIssuerSigningKey = true,
            // the same key that signs tokens at login -> also checks them
            IssuerSigningKey = signingKey,
            // "iss" in the token must be "TrackSystem.Api" -> made by our API
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,

            // "aud" in the token must be "TrackSystem.Client" -> meant for our React app
            ValidateAudience = true,
            ValidAudience = jwtAudience,

            // "exp" must be in the future -> expired token = 401
            ValidateLifetime = true,
            // no 5-minute grace period -> 60 minutes means exactly 60
            ClockSkew = TimeSpan.Zero,

            // which claim is the user's id / role (the short names we kept with MapInboundClaims = false)
            NameClaimType = "sub",
            RoleClaimType = "role"
        };
    });

// registers the rules system -> lets endpoints say "a valid token is required"
builder.Services.AddAuthorization();

// input: the setup → output: app = the ready server
var app = builder.Build();

// every request -> reads "Authorization: Bearer <token>" (if any), checks it, remembers who is calling
app.UseAuthentication();
// every request -> applies the rules (e.g. RequireAuthorization) using what UseAuthentication found
app.UseAuthorization();

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
})
// no valid token -> 401, the code above never runs
.RequireAuthorization();

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

    // the "recipe" for the token: what's inside, who made it, who it's for, until when, how to sign
    var tokenDescriptor = new SecurityTokenDescriptor
    {
        // claims = the data inside the token (readable by anyone -> no secrets here!)
        Subject = new ClaimsIdentity(new[]
        {
            // "sub" (subject) = who this token belongs to -> the eNum
            new Claim("sub", employee.ENum.ToString()),
            // "role" -> Employee / Manager / Admin (used later to allow or deny endpoints)
            new Claim("role", employee.Role),
            // "mustChangePassword" -> "true" on first login, "false" after
            new Claim("mustChangePassword", employee.IsFirstLogin.ToString().ToLower())
        }),
        // "TrackSystem.Api" -> written into the token as "iss"
        Issuer = jwtIssuer,
        // "TrackSystem.Client" -> written into the token as "aud"
        Audience = jwtAudience,
        // now (UTC) + 60 min -> written into the token as "exp"
        Expires = DateTime.UtcNow.AddMinutes(jwtExpiresMinutes),
        // our secret key + HMAC-SHA256 -> how the signature is made
        SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256)
    };

    // recipe -> the token text "xxxxx.yyyyy.zzzzz"
    var token = new JsonWebTokenHandler().CreateToken(tokenDescriptor);

    // -> 200 with the token (React will save it) + whether the user must change the password
    return Results.Ok(new
    {
        message = "Login successful",
        employee.ENum,
        mustChangePassword = employee.IsFirstLogin,
        token
    });
});

// starts the server → waits for requests (stop with Ctrl+C)
app.Run();