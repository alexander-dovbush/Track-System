// brings in EF Core tools (UseSqlServer, CanConnectAsync)
using Microsoft.EntityFrameworkCore;
// brings in our AppDbContext class from the Data folder
using TrackSystem.Api.Data;
// lets Program.cs see LoginRequest, ChangePasswordRequest from the Dtos folder
using TrackSystem.Api.Dtos;
// lets Program.cs see LoginLog from the Models folder
using TrackSystem.Api.Models;
// lets Program.cs see PasswordRules from the Security folder
using TrackSystem.Api.Security;
// lets Program.cs see IEmailSender / SmtpEmailSender from the Services folder
using TrackSystem.Api.Services;
// Claim, ClaimsIdentity -> the data pieces inside a token
using System.Security.Claims;
// JsonWebTokenHandler -> creates the token text
using Microsoft.IdentityModel.JsonWebTokens;
// SymmetricSecurityKey, SigningCredentials, SecurityTokenDescriptor -> key + signing tools
using Microsoft.IdentityModel.Tokens;
// AuthorizationPolicyBuilder -> builds the access rules (policies)
using Microsoft.AspNetCore.Authorization;
// RateLimitPartition, FixedWindowRateLimiterOptions -> the counting tools
using System.Threading.RateLimiting;
// AddPolicy / RequireRateLimiting -> rate-limit rules for endpoints
using Microsoft.AspNetCore.RateLimiting;

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

// "Jwt:ExpiresMinutes" from appsettings.json -> 30 (as a number)
var jwtExpiresMinutes = builder.Configuration.GetValue<int>("Jwt:ExpiresMinutes");

// "PasswordReset:TokenMinutes" from appsettings.json -> 30 (missing -> 30)
var resetTokenMinutes = builder.Configuration.GetValue("PasswordReset:TokenMinutes", 30);

// "PasswordReset:ResetPageUrl" from appsettings.json -> the React page the email links to; missing -> stop with a clear message
var resetPageUrl = builder.Configuration["PasswordReset:ResetPageUrl"]
    ?? throw new InvalidOperationException("PasswordReset:ResetPageUrl is missing in appsettings.json");

// "PasswordReset:MinResponseMs" from appsettings.json -> 1500 (tests will set 0); missing -> 1500
var resetMinResponseMs = builder.Configuration.GetValue("PasswordReset:MinResponseMs", 1500);

// made once at startup: a real BCrypt hash of a throwaway text -> used only to waste the same time for unknown eNums
var dummyHash = BCrypt.Net.BCrypt.HashPassword("dummy-password-for-equal-timing");

// input: connection string "DefaultConnection" from appsettings.json
// output: server now knows HOW to create an AppDbContext connected to SQL Server
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// someone asks for IEmailSender -> ASP.NET gives a SmtpEmailSender (tests swap it for a fake)
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();

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
            // no 5-minute grace period -> 30 minutes means exactly 30
            ClockSkew = TimeSpan.Zero,

            // which claim is the user's id / role (the short names we kept with MapInboundClaims = false)
            NameClaimType = "sub",
            RoleClaimType = "role"
        };
    });

// registers the rules system + our two rules (default and "PasswordChange")
builder.Services.AddAuthorization(options =>
{
    // default rule for every .RequireAuthorization() -> valid token AND password already changed (else 403)
    options.DefaultPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .RequireClaim("mustChangePassword", "false")
        .Build();

    // rule "PasswordChange" -> a valid token is enough (mustChangePassword may be "true")
    options.AddPolicy("PasswordChange", policy => policy.RequireAuthenticatedUser());
});

// "Cors:AllowedOrigins" from appsettings.json -> ["http://localhost:5173"] (empty list if missing)
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

// registers a CORS policy named "ReactApp" -> the rules for browser calls from other origins
builder.Services.AddCors(options =>
    options.AddPolicy("ReactApp", policy =>
        // only these origins are allowed -> any other site's browser call is blocked
        policy.WithOrigins(allowedOrigins)
            // any header -> lets React send "Content-Type: application/json" and "Authorization: Bearer ..."
            .AllowAnyHeader()
            // any method -> GET, POST, PUT, DELETE
            .AllowAnyMethod()));

// "RateLimit:AuthPermitLimit" from appsettings.json -> 10 (tests set a big number); missing -> 10
var authPermitLimit = builder.Configuration.GetValue("RateLimit:AuthPermitLimit", 10);
// registers the rate limiter + one rule "auth" (used by login and change-password)
builder.Services.AddRateLimiter(options =>
{
    // over the limit -> 429 Too Many Requests (the default would be 503)
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // rejected request (status already set to 429) -> body {"message": "..."} so React can show it
    options.OnRejected = async (context, cancellationToken) =>
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { message = "Too many attempts. Please wait a minute and try again." },
            cancellationToken);

    // rule "auth" -> every IP gets its own counter
    options.AddPolicy("auth", http =>
        RateLimitPartition.GetFixedWindowLimiter(
            // caller's IP -> the counter's name (no IP -> one shared "unknown" counter)
            partitionKey: http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            // authPermitLimit requests per 1-minute window, extra requests rejected at once (no waiting line)
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = authPermitLimit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

// input: the setup → output: app = the ready server
var app = builder.Build();

// every request -> if it comes from an allowed origin, add the "Access-Control-Allow-Origin" header to the answer
app.UseCors("ReactApp");

// every request -> if its endpoint has a rate-limit rule, count it; over the limit -> 429
app.UseRateLimiter();

// every request -> reads "Authorization: Bearer <token>" (if any), checks it, remembers who is calling
app.UseAuthentication();

// every request -> applies the rules (e.g. RequireAuthorization) using what UseAuthentication found
app.UseAuthorization();

// employee -> signed JWT text with sub, role, mustChangePassword (used by login and change-password)
string CreateToken(Employee employee)
{
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
        // now (UTC) + 30 min -> written into the token as "exp"
        Expires = DateTime.UtcNow.AddMinutes(jwtExpiresMinutes),
        // our secret key + HMAC-SHA256 -> how the signature is made
        SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256)
    };

    // recipe -> the token text "xxxxx.yyyyy.zzzzz"
    return new JsonWebTokenHandler().CreateToken(tokenDescriptor);
}

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
// default rule: valid token AND password already changed -> else 401 (no/bad token) or 403 (must change first)
.RequireAuthorization();

// POST /api/auth/login, body {"eNum":1,"password":"..."} -> 200 (ok) or 401 (generic error, also when locked / not active)
// every attempt -> one row in dbo.LoginLogs
app.MapPost("/api/auth/login", async (LoginRequest request, AppDbContext db, HttpContext http) =>
{
    // one fixed error answer -> same text whether the eNum or the password is wrong
    var loginFailed = Results.Json(
        new { message = "Invalid employee number or password" },
        statusCode: StatusCodes.Status401Unauthorized);

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
        // unknown eNum -> same slow check as a real one (result ignored) -> response time doesn't reveal it
        BCrypt.Net.BCrypt.Verify(request.Password, dummyHash);
        AddLog(false);
        await db.SaveChangesAsync();
        return loginFailed;
    }

    // lockoutEnd later than now -> log failure, save, generic error (the lock stays silent, per the spec)
    if (employee.LockoutEnd > DateTime.UtcNow)
    {
        // same slow check as a normal attempt (result ignored) -> timing doesn't reveal the lock
        BCrypt.Net.BCrypt.Verify(request.Password, dummyHash);
        AddLog(false);
        await db.SaveChangesAsync();
        return loginFailed;
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
            // -> generic 401 (account is now locked, but we don't say so)
            return loginFailed;
        }

        // new count + log row -> saved together
        await db.SaveChangesAsync();
        // -> generic 401
        return loginFailed;
    }

    // correct password, but status is not "active" (pending / inactive) -> log failure, save, generic error
    if (!string.Equals(employee.Status, "active", StringComparison.OrdinalIgnoreCase))
    {
        AddLog(false);
        await db.SaveChangesAsync();
        return loginFailed;
    }

    // correct password -> counter 0, no lock
    employee.FailedLoginAttempts = 0;
    employee.LockoutEnd = null;
    // log success -> "to save" list
    AddLog(true);
    // employee changes + log row -> saved together
    await db.SaveChangesAsync();

    // employee -> token text (sub, role, mustChangePassword, 30 min)
    var token = CreateToken(employee);

    // -> 200 with the token (React will save it) + whether the user must change the password
    return Results.Ok(new
    {
        message = "Login successful",
        employee.ENum,
        mustChangePassword = employee.IsFirstLogin,
        token
    });
})
// rule "auth" -> max 10 requests per minute per IP, then 429
.RequireRateLimiting("auth");


// POST /api/auth/change-password (valid token required), body {"currentPassword":"...","newPassword":"..."}
// -> 200 {message, mustChangePassword, token} or 400 {message}
app.MapPost("/api/auth/change-password", async (ChangePasswordRequest request, AppDbContext db, ClaimsPrincipal user) =>
{
    // "sub" claim from the token -> eNum as a number; missing or not a number -> 401
    if (!int.TryParse(user.FindFirst("sub")?.Value, out var eNum))
        return Results.Unauthorized();

    // eNum -> the Employee row, or null if it was deleted after the token was made
    var employee = await db.Employees.FirstOrDefaultAsync(e => e.ENum == eNum);
    // no such employee -> 401 (token belongs to nobody now)
    if (employee is null)
        return Results.Unauthorized();

        // current password empty or not matching the stored hash -> 400 (not 401: in React 401 means "bad token" -> logout)
    if (string.IsNullOrEmpty(request.CurrentPassword) || !BCrypt.Net.BCrypt.Verify(request.CurrentPassword, employee.Password))
        return Results.BadRequest(new { message = "Current password is incorrect" });

    // new = current -> 400 (a "change" that changes nothing)
    if (request.NewPassword == request.CurrentPassword)
        return Results.BadRequest(new { message = "New password must be different from the current password" });

    // new password -> first broken rule as text, or null if all rules pass
    var ruleError = PasswordRules.Validate(request.NewPassword);
    // a rule is broken -> 400 with that rule's message
    if (ruleError is not null)
        return Results.BadRequest(new { message = ruleError });

    // new password -> BCrypt hash (with a fresh random salt) -> replaces the old hash
    employee.Password = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
    // first-login flag -> off (no more forced change)
    employee.IsFirstLogin = false;
    // both changes -> saved to dbo.Employees
    await db.SaveChangesAsync();

    // updated employee (IsFirstLogin = false) -> new token with mustChangePassword "false"
    var token = CreateToken(employee);

    // -> 200 with the new token (React will replace the old one with it)
    return Results.Ok(new
    {
        message = "Password changed",
        mustChangePassword = employee.IsFirstLogin,
        token
    });
})
// valid token required, even one with mustChangePassword = "true"
.RequireAuthorization("PasswordChange")
// rule "auth" -> max 10 requests per minute per IP (stops guessing the current password)
.RequireRateLimiting("auth");

// POST /api/auth/refresh (valid token required, no body) -> 200 {token, mustChangePassword} or 401
// React calls it while the user is active -> a fresh 30-min token
app.MapPost("/api/auth/refresh", async (AppDbContext db, ClaimsPrincipal user) =>
{
    // "sub" claim from the token -> eNum as a number; missing or not a number -> 401
    if (!int.TryParse(user.FindFirst("sub")?.Value, out var eNum))
        return Results.Unauthorized();

    // eNum -> the Employee row as it is NOW in the DB
    var employee = await db.Employees.FirstOrDefaultAsync(e => e.ENum == eNum);

    // deleted or no longer "active" -> 401 (React sends the user to Login)
    if (employee is null || !string.Equals(employee.Status, "active", StringComparison.OrdinalIgnoreCase))
        return Results.Unauthorized();

    // current DB state -> new token (same claims, new 30-min expiry)
    return Results.Ok(new
    {
        token = CreateToken(employee),
        mustChangePassword = employee.IsFirstLogin
    });
})
// valid token required, also during first login (the change-password page must not expire while the user types)
.RequireAuthorization("PasswordChange");

// POST /api/auth/forgot-password (no token), body {"eNum":5} -> ALWAYS 200 with the same message
// known + active employee -> reset link by email (part 2); anyone else -> nothing happens
app.MapPost("/api/auth/forgot-password", async (ForgotPasswordRequest request, AppDbContext db, IEmailSender email, ILogger<Program> logger) =>
{
    // timer starts now -> measures how long this request has taken so far
    var stopwatch = System.Diagnostics.Stopwatch.StartNew();

    // wait until MinResponseMs has passed -> then the one fixed answer (same text and same time for every case)
    async Task<IResult> SameAnswer()
    {
        // e.g. 1500 - 40 = 1460 ms still to wait; already slower than 1500 -> no wait
        var waitMs = resetMinResponseMs - (int)stopwatch.ElapsedMilliseconds;
        if (waitMs > 0)
            await Task.Delay(waitMs);
        return Results.Ok(new { message = "If the account exists, a reset link was sent to its email." });
    }

    // eNum -> the Employee row, or null if no such employee
    var employee = await db.Employees.FirstOrDefaultAsync(e => e.ENum == request.ENum);

    // unknown, or not "active" (pending / inactive can't log in anyway) -> no email, same answer
    if (employee is null || !string.Equals(employee.Status, "active", StringComparison.OrdinalIgnoreCase))
        return await SameAnswer();

    // one "now" (UTC) for every time written below -> all consistent
    var now = DateTime.UtcNow;

    // this employee's links that were never used -> list (usually 0 or 1)
    var oldTokens = await db.PasswordResetTokens
        .Where(t => t.ENum == employee.ENum && t.UsedAt == null)
        .ToListAsync();
    // each old link -> usedAt = now (replaced) -> only the newest link will work
    foreach (var old in oldTokens)
        old.UsedAt = now;

    // nothing -> token (goes in the email only) + its SHA-256 hash (goes in the DB)
    var (token, tokenHash) = ResetTokens.Create();

    // new row -> EF's "to save" list
    db.PasswordResetTokens.Add(new PasswordResetToken
    {
        ENum = employee.ENum,
        TokenHash = tokenHash,
        CreatedAt = now,
        // now + 30 min -> after this the link is dead
        ExpiresAt = now.AddMinutes(resetTokenMinutes)
    });
    // old links' usedAt + the new row -> saved together (all or nothing)
    await db.SaveChangesAsync();

    // page URL + token -> "http://localhost:5173/reset-password?token=a3f0..."
    var link = $"{resetPageUrl}?token={token}";
    // first name from the DB -> HTML-safe text (a name like "<b>" can't break the email)
    var firstName = System.Net.WebUtility.HtmlEncode(employee.FirstName);
    // the email body (HTML) -> greeting, clickable link, expiry, "ignore if not you"
    var body = $"""
        <p>Hello {firstName},</p>
        <p>We received a request to reset your Track-System password.</p>
        <p><a href="{link}">Reset my password</a></p>
        <p>This link works once, for {resetTokenMinutes} minutes. If you did not ask for this, ignore this email.</p>
        """;

    try
    {
        // employee's email from the DB -> email sent (Mailpit now, Gmail later)
        await email.SendAsync(employee.Email, "Reset your Track-System password", body);
    }
    catch (Exception ex)
    {
        // mail server down / bad settings -> written to the API console, user still gets the same 200
        logger.LogError(ex, "Reset email for eNum {ENum} could not be sent", employee.ENum);
    }

    // -> same answer as above
    return await SameAnswer();
})
// rule "auth" -> max 10 per minute per IP (stops spamming someone's inbox and eNum scanning)
.RequireRateLimiting("auth");

// POST /api/auth/reset-password (no JWT), body {"token":"...","newPassword":"..."} -> 200 or 400 {message}
app.MapPost("/api/auth/reset-password", async (ResetPasswordRequest request, AppDbContext db) =>
{
    // one answer for every bad-link case (unknown / used / replaced / expired / employee not active)
    var invalidLink = Results.BadRequest(new { message = "This link is invalid or has expired. Please request a new one." });

    // no token in the body -> bad link
    if (string.IsNullOrEmpty(request.Token))
        return invalidLink;

    // token from the link -> the same SHA-256 hex as stored at forgot-password
    var tokenHash = ResetTokens.Hash(request.Token);
    // hash -> the matching row + its Employee (Include = load the employee in the same query), or null
    var resetToken = await db.PasswordResetTokens
        .Include(t => t.Employee)
        .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

    // not found / already used or replaced / expired / employee gone or not active -> bad link
    if (resetToken is null
        || resetToken.UsedAt != null
        || resetToken.ExpiresAt <= DateTime.UtcNow
        || resetToken.Employee is null
        || !string.Equals(resetToken.Employee.Status, "active", StringComparison.OrdinalIgnoreCase))
        return invalidLink;

    // new password (null -> "") -> first broken rule as text, or null if all rules pass
    var ruleError = PasswordRules.Validate(request.NewPassword ?? "");
    // a rule is broken -> 400 with that rule's message (the link stays usable, the user can try again)
    if (ruleError is not null)
        return Results.BadRequest(new { message = ruleError });

    // the employee this link belongs to (loaded by Include above)
    var employee = resetToken.Employee;
    // new password -> BCrypt hash (fresh salt) -> replaces the old hash
    employee.Password = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
    // the user chose this password himself -> no forced change at next login
    employee.IsFirstLogin = false;
    // reset also unlocks -> counter 0, no lock
    employee.FailedLoginAttempts = 0;
    employee.LockoutEnd = null;
    // link -> used now (a second click = invalid)
    resetToken.UsedAt = DateTime.UtcNow;
    // employee changes + usedAt -> saved together (all or nothing)
    await db.SaveChangesAsync();

    // -> 200; React sends the user to Login (no automatic login)
    return Results.Ok(new { message = "Your password has been reset. You can log in now." });
})

// rule "auth" -> max 10 per minute per IP
.RequireRateLimiting("auth");

// starts the server → waits for requests (stop with Ctrl+C)
app.Run();