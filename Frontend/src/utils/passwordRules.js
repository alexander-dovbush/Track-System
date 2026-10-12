// new password text -> null if all rules pass, or the first broken rule as a message
// (same rules and texts as Backend/TrackSystem.Api/Security/PasswordRules.cs)
export function validatePassword(password) {
  // missing or shorter than 8 -> error
  if (!password || password.length < 8)
    return "Password must be at least 8 characters";
  // longer than 64 -> error
  if (password.length > 64) return "Password must be at most 64 characters";
  // no A-Z anywhere -> error
  if (!/[A-Z]/.test(password))
    return "Password must contain at least one capital letter";
  // no 0-9 anywhere -> error
  if (!/[0-9]/.test(password))
    return "Password must contain at least one number";
  // no character that is not a letter, digit or space -> error
  if (!/[^\p{L}\p{Nd}\s]/u.test(password))
    return "Password must contain at least one special character";
  // all rules passed
  return null;
}
