import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import path from 'node:path'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, 'src'),
    },
  },
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: process.env.VITE_API_PROXY_TARGET ?? 'http://localhost:8085',
        changeOrigin: true,
      },
      '/hubs': {
        target: process.env.VITE_API_PROXY_TARGET ?? 'http://localhost:8085',
        changeOrigin: true,
        ws: true,
      },
    },
  },
})
