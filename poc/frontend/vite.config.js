import { defineConfig } from 'vite';
import { resolve } from 'node:path';

export default defineConfig({
  // Same frontend: /POC/ in the existing browser POC, relative URLs in native Tauri.
  base: process.env.KDZJ_TAURI === '1' ? './' : (process.env.KDZJ_PAGES_BASE || '/POC/'),
  publicDir: resolve(import.meta.dirname, 'public'),
  build: { outDir: 'dist', emptyOutDir: true },
  optimizeDeps: { exclude: ['@sqlite.org/sqlite-wasm'] },
  worker: { format: 'es' }
});
