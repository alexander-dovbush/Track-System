import { useState } from "react";
import styles from "./ChangePasswordPage.module.css";
// the 4 password rules (same as the API)
import { validatePassword } from "../../utils/passwordRules";
// our shared axios (adds the token, handles 401) + the sessionStorage key name
import api, { TOKEN_KEY } from "../../api/client";

function ChangePasswordPage() {
  // what the user types in "Current Password" -> currentPassword
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [error, setError] = useState("");

  // form submit -> checks the fields -> calls the API
  const handleSubmit = async (e) => {
    e.preventDefault();

    // any of the 3 fields empty -> message, no server call
    if (!currentPassword || !newPassword || !confirmPassword) {
      setError("Please fill in all fields");
      return;
    }

    // new password -> first broken rule as text, or null
    const ruleError = validatePassword(newPassword);
    // a rule is broken -> show it, no server call
    if (ruleError) {
      setError(ruleError);
      return;
    }

    if (newPassword !== confirmPassword) {
      setError("Passwords do not match");
      return;
    }

    // new = current -> message (the API refuses it too)
    if (newPassword === currentPassword) {
      setError("New password must be different from the current password");
      return;
    }

    // clear an old message
    setError("");
    try {
      // POST {currentPassword, newPassword} (+ token added by the interceptor) -> 200 {message, mustChangePassword, token}
      const response = await api.post("/api/auth/change-password", {
        currentPassword,
        newPassword,
      });
      // new token ("false") replaces the old one ("true") -> the rest of the system opens
      sessionStorage.setItem(TOKEN_KEY, response.data.token);
      // TODO: navigate to the main page once it exists
      console.log(response.data.message);
    } catch (err) {
      // no answer at all -> API stopped or unreachable
      if (!err.response) {
        setError("Cannot reach the server. Try again later.");
      } else {
        // 400 -> the API's text (wrong current / same / rule); a 401 never gets here visibly: the interceptor sends to Login
        setError(
          err.response.data?.message ?? "Something went wrong. Try again.",
        );
      }
    }
  };

  return (
    <div className={styles.changeContainer}>
      <div className={styles.changeCard}>
        <h1>Change Password</h1>
        <p className={styles.changeDescription}>
          First login - please set a new password
        </p>
        <form onSubmit={handleSubmit}>
          <div className={styles.formGroup}>
            {/* typed text -> currentPassword */}
            <label>Current Password</label>
            <input
              type="password"
              value={currentPassword}
              onChange={(e) => setCurrentPassword(e.target.value)}
            />
          </div>

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
          <button type="submit" className={styles.submitButton}>
            Update Password
          </button>
        </form>
      </div>
    </div>
  );
}

export default ChangePasswordPage;
