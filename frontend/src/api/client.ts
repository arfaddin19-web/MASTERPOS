import axios from 'axios';

// The backend is a local-install-per-client server (see backend/README.md's
// deployment model) — same machine, same network, as documented there.
// VITE_API_BASE_URL overrides this for dev; defaults to same-origin /api,
// which is how a production build served by (or reverse-proxied alongside)
// the API itself would resolve it.
const baseURL = import.meta.env.VITE_API_BASE_URL ?? '/api';

export const apiClient = axios.create({ baseURL });

const TOKEN_KEY = 'masterpos.token';

export function getStoredToken(): string | null {
  return localStorage.getItem(TOKEN_KEY);
}

export function setStoredToken(token: string | null) {
  if (token) localStorage.setItem(TOKEN_KEY, token);
  else localStorage.removeItem(TOKEN_KEY);
}

apiClient.interceptors.request.use((config) => {
  const token = getStoredToken();
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

// A 401 means the token is missing/expired/invalid — never something a retry
// fixes. Clear the stale session and bounce to /login rather than leaving
// the app stuck showing a half-loaded authenticated screen. Login itself
// is exempt: a wrong-password 401 there must surface as a form error, not
// a redirect loop.
apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    if (axios.isAxiosError(error) && error.response?.status === 401 && !error.config?.url?.includes('/auth/login')) {
      setStoredToken(null);
      localStorage.removeItem('masterpos.session');
      if (!window.location.pathname.startsWith('/login')) {
        window.location.assign('/login');
      }
    }
    return Promise.reject(error);
  },
);

/** The shape every controller in the backend returns on a rejected AppException. */
export interface ApiErrorBody {
  message?: string;
  title?: string; // ASP.NET's default ValidationProblemDetails shape, for 400s the model binder itself rejects
}

/** Pulls the backend's own error message out of an axios error, falling back
 * to something generic — every controller in the backend maps AppException
 * to `{ message }`, so this is almost always what the user should see.
 * Mutation `mutationFn`s across the app also throw plain client-side
 * `Error`s for validation done before ever calling the API ("Select a
 * table first.", "Product name is required.", …) — those aren't axios
 * errors, so they need their own branch or every one of those messages
 * gets thrown away in favor of the generic fallback, right when the user
 * most needs the specific guidance. */
export function apiErrorMessage(err: unknown): string {
  if (axios.isAxiosError(err)) {
    const body = err.response?.data as ApiErrorBody | undefined;
    if (body?.message) return body.message;
    if (body?.title) return body.title;
    if (err.response?.status === 401) return 'Your session has expired — please sign in again.';
    if (err.message) return err.message;
  }
  if (err instanceof Error && err.message) return err.message;
  return 'Something went wrong. Please try again.';
}
