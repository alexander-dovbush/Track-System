import { useState } from "react";
import { Link } from "react-router-dom";
import styles from "./LoginPage.module.css";

function LoginPage() {
  const [eNum, setENum] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");

  const handleSubmit = (e) => {
    e.preventDefault();
    if (!eNum || !password) {
      setError("Please enter employee number and password");
      return;
    }
    setError("");
    console.log("Login attempt:", { eNum, password });
  };

  return (
    <div className={styles.loginContainer}>
      <div className={styles.loginCard}>
        <h1>Login</h1>
        <form onSubmit={handleSubmit}>
          <div className={styles.formGroup}>
            <label>Employee Number</label>
            <input
              type="text"
              value={eNum}
              onChange={(e) => setENum(e.target.value)}
            />
          </div>
          <div className={styles.formGroup}>
            <label>Password</label>
            <input
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
            />
          </div>
          {error && <p className={styles.errorMessage}>{error}</p>}
          <button type="submit" className={styles.loginButton}>
            Login
          </button>
        </form>
        <Link to="/forgot-password" className={styles.forgotLink}>
          Forgot Password?
        </Link>
      </div>
    </div>
  );
}

export default LoginPage;
