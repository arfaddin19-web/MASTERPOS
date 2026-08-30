import react from '@vitejs/plugin-react';
import { defineConfig } from 'vite';

// Dev-time proxy to the local backend — avoids needing CORS middleware in
// the API at all, since the browser sees every request as same-origin.
// A production build is meant to be served from the same origin as the API
// (or behind the same reverse proxy) per the backend's local-install
// deployment model, so this proxy is dev-only; VITE_API_BASE_URL overrides
// api/client.ts's baseURL if a different setup is ever needed.
export default defineConfig({
  plugins: [react()],
  server: {
    host: true,
    proxy: {
      '/api': {
        target: process.env.VITE_BACKEND_URL ?? 'http://localhost:5080',
        changeOrigin: true,
      },
    },
  },
});
