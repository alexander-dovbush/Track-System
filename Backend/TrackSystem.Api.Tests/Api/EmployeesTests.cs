// HttpStatusCode -> named status codes
using System.Net;
// AuthenticationHeaderValue -> builds "Authorization: Bearer <token>"
using System.Net.Http.Headers;
// Claim, ClaimsIdentity -> the data pieces inside a token
using System.Security.Claims;
// RandomNumberGenerator -> random bytes for an attacker's key
using System.Security.Cryptography;
// JsonWebTokenHandler -> creates token text (same class as CreateToken in Program.cs)
using Microsoft.IdentityModel.JsonWebTokens;
// SymmetricSecurityKey, SigningCredentials, Base64UrlEncoder -> key, signing, token-part encoding
using Microsoft.IdentityModel.Tokens;
// our factory
using TrackSystem.Api.Tests.Infrastructure;

namespace TrackSystem.Api.Tests.Api;

// shared API + test DB (collection "Api")
[Collection("Api")]
public class EmployeesTests
{
    // one valid password for the test employees
    private const string Password = "Test1234!";

    // the shared factory
    private readonly TestApiFactory _factory;

    // xUnit -> hands us the shared factory
    public EmployeesTests(TestApiFactory factory)
    {
        _factory = factory;
    }

    // token (or null = no header) -> GET /api/employees -> response
    private async Task<HttpResponseMessage> GetEmployeesAsync(string? token)
    {
        var client = _factory.CreateClient();

        // token given -> header "Authorization: Bearer <token>" on this client's requests
        if (token is not null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return await client.GetAsync("/api/employees");
    }

        // eNum + key (base64) -> token with our claims, iss, aud, 30 min -> signed with THAT key
    private static string BuildToken(int eNum, string keyBase64)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            // same 3 claims as a real token, password already changed
            Subject = new ClaimsIdentity(new[]
            {
                new Claim("sub", eNum.ToString()),
                new Claim("role", "Employee"),
                new Claim("mustChangePassword", "false")
            }),
            // same iss / aud as appsettings.json -> only the key can be wrong
            Issuer = "TrackSystem.Api",
            Audience = "TrackSystem.Client",
            Expires = DateTime.UtcNow.AddMinutes(30),
            // base64 key -> bytes -> HMAC-SHA256 signature
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Convert.FromBase64String(keyBase64)), SecurityAlgorithms.HmacSha256)
        };

        // recipe -> token text "xxxxx.yyyyy.zzzzz"
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

        [Fact]
    public async Task Employees_NoToken_Returns401()
    {
        // no Authorization header -> the guard can't tell who calls -> 401
        var response = await GetEmployeesAsync(null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Employees_ValidToken_Returns200WithoutPasswords()
    {
        // normal employee (password already changed) -> real token
        var eNum = await _factory.CreateEmployeeAsync(Password);
        var token = await _factory.LoginAsync(eNum, Password);

        // valid token + mustChangePassword "false" -> 200
        var response = await GetEmployeesAsync(token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // body as plain text -> must not contain a "password" field or a BCrypt hash ($2...)
        var text = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("\"password\"", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("$2", text);
    }

    [Fact]
    public async Task Employees_MustChangePasswordToken_Returns403()
    {
        // first-login employee -> token with mustChangePassword "true"
        var eNum = await _factory.CreateEmployeeAsync(Password, isFirstLogin: true);
        var token = await _factory.LoginAsync(eNum, Password);

        // valid token, but the default rule wants "false" -> 403 (not 401: React must NOT log out)
        var response = await GetEmployeesAsync(token);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

        [Fact]
    public async Task Employees_TokenSignedWithOurKey_Returns200()
    {
        // control: hand-built token + the test key the API uses -> accepted
        var response = await GetEmployeesAsync(BuildToken(1, TestApiFactory.JwtKey));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Employees_TokenSignedWithOtherKey_Returns401()
    {
        // attacker's own random key -> same content, wrong signature
        var otherKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

        // signature check fails -> 401
        var response = await GetEmployeesAsync(BuildToken(1, otherKey));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Employees_EditedPayload_Returns401()
    {
        // real token from a real login
        var eNum = await _factory.CreateEmployeeAsync(Password);
        var token = await _factory.LoginAsync(eNum, Password);

        // "xxxxx.yyyyy.zzzzz" -> [header, payload, signature]
        var parts = token.Split('.');

        // payload part -> JSON text -> role Employee changed to Admin
        var payload = Base64UrlEncoder.Decode(parts[1]);
        var edited = payload.Replace("\"role\":\"Employee\"", "\"role\":\"Admin\"");

        // guard: the replace really changed something (else this test would prove nothing)
        Assert.NotEqual(payload, edited);

        // same header + edited payload + OLD signature -> tampered token
        var tampered = $"{parts[0]}.{Base64UrlEncoder.Encode(edited)}.{parts[2]}";

        // server recomputes the signature over the new payload -> no match -> 401
        var response = await GetEmployeesAsync(tampered);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}