import { useState } from "react";
// Link -> clickable links; useSearchParams -> reads "?token=..." from the address bar
import { Link, useSearchParams } from "react-router-dom";
import styles from "./ResetPasswordPage.module.css";
// the 4 password rules (same texts as the API)
import { validatePassword } from "../../utils/passwordRules";
// our shared axios (baseURL from .env, error handling)
import api from "../../api/client";

function ResetPasswordPage() {
  // address "/reset-password?token=a3f0..." -> searchParams
  const [searchParams] = useSearchParams();
  // "a3f0..." -> token; no "?token=" in the link -> null
  const token = searchParams.get("token");

  // typed new password / its repeat / red message
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [error, setError] = useState("");
  // green text after success (form hides when set)
  const [message, setMessage] = useState("");
  // true while waiting for the server -> button disabled
  const [sending, setSending] = useState(false);

  // form submit -> local checks (server call comes in 6.8c)
  const handleSubmit = async (e) => {
    // stop the browser's own submit (it would reload the page)
    e.preventDefault();
    // a field empty -> message
    if (!newPassword || !confirmPassword) {
      setError("Please fill in all fields");
      return;
    }
    // new password -> first broken rule as text, or null
    const ruleError = validatePassword(newPassword);
    if (ruleError) {
      setError(ruleError);
      return;
    }
    // the two fields differ -> message
    if (newPassword !== confirmPassword) {
      setError("Passwords do not match");
      return;
    }
    setError("");
    // TEMPORARY -> replaced by the API call in 6.8c
    setSending(true);
    try {
      // POST {token, newPassword} -> 200 {message}
      const response = await api.post("/api/auth/reset-password", {
        token,
        newPassword,
      });
      // success -> green text, form disappears
      setMessage(response.data.message);
    } catch (err) {
      // no answer at all -> API stopped or unreachable
      if (!err.response) {
        setError("Cannot reach the server. Try again later.");
      } else {
        // 400 (bad/used/expired link, or a rule) / 429 -> the API's text
        setError(
          err.response.data?.message ?? "Something went wrong. Try again.",
        );
      }
    } finally {
      // success or error -> button usable again
      setSending(false);
    }
  };

  return (
    <div className={styles.resetContainer}>
      <div className={styles.resetCard}>
        <h1>Reset Password</h1>
        {/* no token -> the link is broken; the server checks the real validity later */}
        {!token && (
          <p className={styles.errorMessage}>
            This link is invalid or has expired. Please request a new one.
          </p>
        )}
        {/* token present and not finished yet -> description + form */}
        {token && !message && (
          <>
            <p className={styles.resetDescription}>Choose a new password</p>
            <form onSubmit={handleSubmit}>
              <div className={styles.formGroup}>
                <label>New Password</label>
                <input
                  type="password"
                  value={newPassword}
                  onChange={(e) => setNewPassword(e.target.value)}
                />
              </div>
              <div className={styles.formGroup}>
                <label>Confirm Password</label>
                <input
                  type="password"
                  value={confirmPassword}
                  onChange={(e) => setConfirmPassword(e.target.value)}
                />
              </div>
              {error && <p className={styles.errorMessage}>{error}</p>}
              {/* disabled while sending -> no double clicks */}
              <button
                type="submit"
                className={styles.submitButton}
                disabled={sending}
              >
                {sending ? "Saving..." : "Reset Password"}
              </button>
            </form>
          </>
        )}
        {/* success -> green text */}
        {message && <p className={styles.successMessage}>{message}</p>}
        {/* success -> "Go to Login"; otherwise -> "Request a new link" */}
        {message ? (
          <Link to="/" className={styles.backLink}>
            Go to Login
          </Link>
        ) : (
          <Link to="/forgot-password" className={styles.backLink}>
            Request a new link
          </Link>
        )}
      </div>
    </div>
  );
}

export default ResetPasswordPage;
