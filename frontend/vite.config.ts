/// <reference types="vitest/config" />
import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'
import { VitePWA } from 'vite-plugin-pwa'

// https://vite.dev/config/
export default defineConfig({
  plugins: [
    react(),
    tailwindcss(),
    VitePWA({
      registerType: 'autoUpdate',
      // generateSW with an empty precache list and no runtimeCaching: this satisfies
      // installability (valid manifest + a registered service worker) without adding
      // any offline caching - the brief is explicit that offline support is out of
      // scope for v1. Do not add globPatterns/runtimeCaching here without revisiting
      // that decision first (see docs/roadmap.md, Lot 10).
      strategies: 'generateSW',
      workbox: {
        globPatterns: [],
      },
      includeAssets: ['favicon.png', 'apple-touch-icon.png'],
      manifest: {
        id: '/',
        name: 'Budget Prévisionnel',
        short_name: 'Budget',
        description: 'Suivi de budget et prévisionnel financier personnel/familial',
        lang: 'fr',
        theme_color: '#C6A15B',
        background_color: '#191714',
        display: 'standalone',
        start_url: '/',
        icons: [
          { src: 'pwa-192x192.png', sizes: '192x192', type: 'image/png' },
          { src: 'pwa-512x512.png', sizes: '512x512', type: 'image/png' },
          { src: 'maskable-icon-512x512.png', sizes: '512x512', type: 'image/png', purpose: 'maskable' },
        ],
      },
    }),
  ],
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    globals: true,
  },
})
