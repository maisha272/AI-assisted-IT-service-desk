/// <reference types="vitest/config" />
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    // In development the React app (port 5173) forwards /api calls to the ASP.NET Core API (port 5080),
    // so the browser sees a single origin and we don't need CORS. In production the API serves the
    // built React files itself, which gives the same single-origin setup.
    proxy: {
      '/api': 'http://localhost:5080',
    },
  },
  test: {
    environment: 'jsdom',
    setupFiles: './src/test/setup.ts',
    globals: true,
  },
})
