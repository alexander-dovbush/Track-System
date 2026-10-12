// the "address" of this class -> Program.cs finds it with: using TrackSystem.Api.Security;
namespace TrackSystem.Api.Security;

// our password rules in one place -> used by the change-password endpoint (and later the tests)
public static class PasswordRules
{
    // password text -> null if it passes all rules, or the first broken rule as a message
    public static string? Validate(string? password)
    {
        // missing or shorter than 8 -> error
        if (string.IsNullOrEmpty(password) || password.Length < 8)
            return "Password must be at least 8 characters";

        // longer than 64 -> error (BCrypt ignores everything after 72 bytes)
        if (password.Length > 64)
            return "Password must be at most 64 characters";

        // no A-Z anywhere -> error
        if (!password.Any(char.IsAsciiLetterUpper))
            return "Password must contain at least one capital letter";

        // no 0-9 anywhere -> error
        if (!password.Any(char.IsAsciiDigit))
            return "Password must contain at least one number";

        // no character that is not a letter, digit or space -> error
        if (!password.Any(c => !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c)))
            return "Password must contain at least one special character";

        // all rules passed
        return null;
    }
}