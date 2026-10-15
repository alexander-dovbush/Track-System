import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import "./index.css";
import App from "./App.jsx";
// session keeper -> refreshes the token while the user is active
import { startSession } from "./api/session";

// one call at app start -> activity listeners on
startSession();

createRoot(document.getElementById("root")).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
