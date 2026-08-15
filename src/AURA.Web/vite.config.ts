import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

const apiDevServerUrl = 'http://localhost:5235'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      // Lets the app call relative paths (e.g. "/health", "/api/...") in development
      // without hardcoding VITE_API_BASE_URL.
      '/health': apiDevServerUrl,
      '/api': apiDevServerUrl,
    },
  },
})
