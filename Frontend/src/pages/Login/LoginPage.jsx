import { useState } from "react";
// Link -> clickable links; useNavigate -> move to another page from code
import { Link, useNavigate } from "react-router-dom";
import styles from "./LoginPage.module.css";
// our shared axios (adds the token, handles 401) + the sessionStorage key name
import api, { TOKEN_KEY } from "../../api/client";

function LoginPage() {
  const [eNum, setENum] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  // navigate("/some-path") -> switch page without reloading
  const navigate = useNavigate();

  // form submit -> checks the fields -> calls the API
  const handleSubmit = async (e) => {
    // stop the browser's own submit (it would reload the page)
    e.preventDefault();
    // an empty field -> message, no server call
    if (!eNum || !password) {
      setError("Please enter employee number and password");
      return;
    }
    // not only digits -> message (eNum is a number in the DB)
    if (!/^\d+$/.test(eNum)) {
      setError("Employee number must contain digits only");
      return;
    }
    // clear an old message
    setError("");
    try {
      // POST {eNum, password} -> 200 {message, eNum, mustChangePassword, token}
      const response = await api.post("/api/auth/login", {
        eNum: Number(eNum),
        password,
      });
      // token -> sessionStorage (the interceptor sends it from now on)
      sessionStorage.setItem(TOKEN_KEY, response.data.token);
      // first login -> must change password first
      if (response.data.mustChangePassword) {
        navigate("/change-password");
      } else {
        // TODO: navigate to the main page once it exists
        console.log("Logged in as", response.data.eNum);
      }
    } catch (err) {
      // no answer at all -> API stopped or unreachable
      if (!err.response) {
        setError("Cannot reach the server. Try again later.");
      } else {
        // 401 / 423 -> the API's own text; an answer without text -> a general message
        setError(
          err.response.data?.message ?? "Something went wrong. Try again.",
        );
      }
    }
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
