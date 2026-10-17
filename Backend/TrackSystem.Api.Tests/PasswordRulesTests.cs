// the class we test -> PasswordRules
using TrackSystem.Api.Security;

// test "address" -> mirrors the API's Security folder
namespace TrackSystem.Api.Tests.Security;

// all PasswordRules tests in one class
public class PasswordRulesTests
{
    // [Fact] -> xUnit runs this method as one test
    [Fact]
    // name = Method_Situation_Expected -> readable in the test report
    public void Validate_ValidPassword_ReturnsNull()
    {
        // password that passes every rule -> Validate gives back null
        var result = PasswordRules.Validate("Test1234!");

        // null -> test passes; anything else -> test fails and shows the value
        Assert.Null(result);
    }

        // [Theory] -> one method, run once per [InlineData] row
    [Theory]
    // 4 chars (has capital, number, special) -> only "too short" is broken
    [InlineData("Te1!", "Password must be at least 8 characters")]
    // no A-Z -> capital message
    [InlineData("test1234!", "Password must contain at least one capital letter")]
    // no 0-9 -> number message
    [InlineData("Testtest!", "Password must contain at least one number")]
    // only letters + digits -> special message
    [InlineData("Test1234", "Password must contain at least one special character")]
    // a space does NOT count as special -> still the special message
    [InlineData("Test 1234", "Password must contain at least one special character")]
    // row values -> arrive as password + expected
    public void Validate_BrokenRule_ReturnsThatRulesMessage(string password, string expected)
    {
        // password -> error message
        var result = PasswordRules.Validate(password);

        // exact same text -> pass; different text or null -> fail
        Assert.Equal(expected, result);
    }

        // runs twice: length 8, then length 64
    [Theory]
    [InlineData(8)]
    [InlineData(64)]
    public void Validate_LengthAtLimit_ReturnsNull(int length)
    {
        // "Ab1!" (capital, number, special) + 'a' repeated -> exactly `length` chars
        var password = "Ab1!" + new string('a', length - 4);

        // guard: our built password really has that length
        Assert.Equal(length, password.Length);

        // all rules OK at the limit -> null
        Assert.Null(PasswordRules.Validate(password));
    }

        [Fact]
    public void Validate_65Chars_ReturnsTooLong()
    {
        // "Ab1!" + 61 x 'a' -> 65 chars, every other rule OK
        var password = "Ab1!" + new string('a', 61);

        // one over the limit -> max-length message
        Assert.Equal("Password must be at most 64 characters", PasswordRules.Validate(password));
    }

        // 3 rows -> all must get the "too short" message
    [Theory]
    // no password sent at all
    [InlineData(null)]
    // empty text
    [InlineData("")]
    // 7 chars, capital + number + special OK -> one under the limit
    [InlineData("Ab1!aaa")]
    // string? -> this parameter is allowed to be null
    public void Validate_MissingOrSevenChars_ReturnsTooShort(string? password)
    {
        // each row -> min-length message
        Assert.Equal("Password must be at least 8 characters", PasswordRules.Validate(password));
    }

    
}