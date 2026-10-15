// our shared axios (adds the token, handles 401) + the sessionStorage key name
import api, { TOKEN_KEY } from "./client";

// token text -> its claims object {sub, role, mustChangePassword, iat, exp, ...}; broken/missing token -> null
export function readClaims(token) {
  try {
    // "xxx.yyy.zzz" -> middle part -> base64url to base64 -> JSON text -> object
    return JSON.parse(
      atob(token.split(".")[1].replace(/-/g, "+").replace(/_/g, "/")),
    );
  } catch {
    // anything goes wrong (null token, bad text) -> no claims
    return null;
  }
}

// true while a refresh request is on its way -> never two at the same time
let refreshing = false;

// user did something -> token older than 1 minute? -> get a fresh 30-min token
async function refreshIfNeeded() {
  // saved token -> claims; not logged in, or a refresh already running -> nothing to do
  const claims = readClaims(sessionStorage.getItem(TOKEN_KEY));
  if (!claims || refreshing) return;

  // token made less than 60 s ago -> fresh enough (max 1 refresh per minute)
  if (Date.now() - claims.iat * 1000 < 60_000) return;

  refreshing = true;
  try {
    // POST /api/auth/refresh (token added by the interceptor) -> {token, mustChangePassword}
    const response = await api.post("/api/auth/refresh");
    // fresh token -> replaces the old one
    sessionStorage.setItem(TOKEN_KEY, response.data.token);
  } catch {
    // 401 -> the interceptor already removed the token and went to Login; other errors -> retried on the next activity
  } finally {
    // request finished (ok or not) -> the next refresh is allowed
    refreshing = false;
  }
}

// called once at app start -> activity listeners on + expiry check every 15 s
export function startSession() {
  // every click / key / mouse move / scroll -> may trigger a refresh
  ["click", "keydown", "mousemove", "scroll"].forEach((eventName) =>
    window.addEventListener(eventName, refreshIfNeeded, { passive: true }),
  );

  // every 15 s -> saved token expired? -> remove it and go to Login (idle logout without any click)
  setInterval(() => {
    // saved token -> claims; no token -> nothing to do
    const claims = readClaims(sessionStorage.getItem(TOKEN_KEY));
    // no token, or "exp" still in the future -> nothing to do
    if (!claims || claims.exp * 1000 > Date.now()) return;
    // expired -> token removed
    sessionStorage.removeItem(TOKEN_KEY);
    // not already on Login -> go to "/" (full reload, like the interceptor)
    if (window.location.pathname !== "/") window.location.href = "/";
  }, 15_000);
}
