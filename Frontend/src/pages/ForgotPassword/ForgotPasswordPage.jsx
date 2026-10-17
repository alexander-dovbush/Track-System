import { useState } from "react";
import { Link } from "react-router-dom";
import styles from "./ForgotPasswordPage.module.css";
// our shared axios (baseURL from .env, error handling)
import api from "../../api/client";

function ForgotPasswordPage() {
  // what the user typed in "Employee Number" (text; checked to be digits before sending)
  const [eNum, setENum] = useState("");
  const [message, setMessage] = useState("");
  const [error, setError] = useState("");
  // true while waiting for the server (~1.5 s) -> button disabled, shows "Sending..."
  const [sending, setSending] = useState(false);

  const handleSubmit = async (e) => {
    e.preventDefault();
    // empty field -> message, no server call
    if (!eNum) {
      setError("Please enter your employee number");
      setMessage("");
      return;
    }
    // not only digits -> message (same check as Login)
    if (!/^\d+$/.test(eNum)) {
      setError("Employee number must contain digits only");
      setMessage("");
      return;
    }
    // clear old messages, start "sending"
    setError("");
    setMessage("");
    setSending(true);
    try {
      // POST {eNum} -> always 200 {message} (same text whether the eNum exists or not)
      const response = await api.post("/api/auth/forgot-password", {
        eNum: Number(eNum),
      });
      // the server's text -> green message
      setMessage(response.data.message);
    } catch (err) {
      // no answer at all -> API stopped or unreachable
      if (!err.response) {
        setError("Cannot reach the server. Try again later.");
      } else {
        // 429 (too many) or other -> the API's own text, or a general one
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
    <div className={styles.forgotContainer}>
      <div className={styles.forgotCard}>
        <h1>Forgot Password</h1>
        <p className={styles.forgotDescription}>
          Enter your employee number and we will send a reset link to your email
        </p>
        <form onSubmit={handleSubmit}>
          <div className={styles.formGroup}>
            <label>Employee Number</label>
            <input
              type="text"
              value={eNum}
              onChange={(e) => setENum(e.target.value)}
            />
          </div>
          {error && <p className={styles.errorMessage}>{error}</p>}
          {message && <p className={styles.successMessage}>{message}</p>}
          {/* disabled while sending -> no double clicks (each click = a new email + rate-limit count) */}
          <button
            type="submit"
            className={styles.submitButton}
            disabled={sending}
          >
            {sending ? "Sending..." : "Send Reset Link"}
          </button>
        </form>
        <Link to="/" className={styles.backLink}>
          Back to Login
        </Link>
      </div>
    </div>
  );
}

export default ForgotPasswordPage;
