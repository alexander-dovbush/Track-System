// axios library -> sends HTTP requests to our API
import axios from "axios";
// the name under which the token is kept in sessionStorage (one place -> no typos)
export const TOKEN_KEY = "token";

// one shared axios with our API address -> calls only need "/api/..."
const api = axios.create({
  // "http://localhost:5163" from .env.development
  baseURL: import.meta.env.VITE_API_URL,
});

// before every request -> if a token is saved, add "Authorization: Bearer <token>"
api.interceptors.request.use((config) => {
  // sessionStorage "token" -> token text, or null if not logged in
  const token = sessionStorage.getItem(TOKEN_KEY);
  // token found -> add the header
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  // the (changed) request -> continues to the API
  return config;
});

// after every answer -> errors pass through here first
api.interceptors.response.use(
  // success (2xx) -> answer goes on unchanged
  (response) => response,
  // error (4xx/5xx, or server down) -> checked here
  (error) => {
    // was this the login call? -> its 401 means "wrong password", not "bad token"
    const isLoginCall = error.config?.url === "/api/auth/login";
    // 401 from any other call -> token missing / expired / fake
    if (error.response?.status === 401 && !isLoginCall) {
      // bad token -> removed from sessionStorage
      sessionStorage.removeItem(TOKEN_KEY);
      // not already on Login -> go to "/" (full page reload)
      if (window.location.pathname !== "/") {
        window.location.href = "/";
      }
    }
    // error -> still handed to the page that made the call (it can show a message)
    return Promise.reject(error);
  },
);

// other files: import api from "../../api/client" -> use api.get / api.post
export default api;
