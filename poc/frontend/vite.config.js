import { defineConfig } from 'vite';
import { resolve } from 'node:path';

export default defineConfig({
  base: '/POC/',
  publicDir: resolve(import.meta.dirname, 'public'),
  build: { outDir: 'dist', emptyOutDir: true },
  optimizeDeps: { exclude: ['@sqlite.org/sqlite-wasm'] },
  worker: { format: 'es' }
});
