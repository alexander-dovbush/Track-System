import { useState } from "react";
import { Link } from "react-router-dom";
import styles from "./ForgotPasswordPage.module.css";

function ForgotPasswordPage() {
  const [email, setEmail] = useState("");
  const [message, setMessage] = useState("");
  const [error, setError] = useState("");

  const handleSubmit = (e) => {
    e.preventDefault();
    if (!email) {
      setError("Please enter your email");
      setMessage(""); // clear success message
      return;
    }
    setError(""); // clear error message
    setMessage("Password reset link sent to your email");
  };

  return (
    <div className={styles.forgotContainer}>
      <div className={styles.forgotCard}>
        <h1>Forgot Password</h1>
        <p className={styles.forgotDescription}>
          Enter your email and we will send you a reset link
        </p>
        <form onSubmit={handleSubmit}>
          <div className={styles.formGroup}>
            <label>Email</label>
            <input
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
            />
          </div>
          {error && <p className={styles.errorMessage}>{error}</p>}
          {message && <p className={styles.successMessage}>{message}</p>}
          <button type="submit" className={styles.submitButton}>
            Send Reset Link
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
