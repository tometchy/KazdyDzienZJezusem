import { defineConfig, devices } from '@playwright/test';
import path from 'node:path';
import { existsSync } from 'node:fs';

const externalBaseUrl = process.env.KDJ_VISUAL_BASE_URL;
process.env.PLAYWRIGHT_BROWSERS_PATH ??= path.resolve(__dirname, '../artifacts/playwright');
if (process.platform !== 'linux') {
  throw new Error('Shared screenshot baselines require Linux (use WSL or the Ubuntu CI runner).');
}
if (!existsSync(path.resolve(__dirname, '../artifacts/visual-tools/liberation-fonts-ttf-2.1.5/LiberationSans-Regular.ttf'))) {
  throw new Error('Screenshot fonts are missing. Run bash scripts/setup-visual-tests.sh first.');
}
process.env.FONTCONFIG_FILE = path.resolve(__dirname, 'fonts.conf');

export default defineConfig({
  testDir: './tests',
  outputDir: '../screenshots/results',
  snapshotPathTemplate: '../screenshots/baseline/{projectName}/{arg}{ext}',
  // A regular run must fail on missing baselines, including in Codex.
  updateSnapshots: 'none',
  globalSetup: externalBaseUrl ? undefined : require.resolve('./global-setup'),
  fullyParallel: true,
  forbidOnly: true,
  retries: 1,
  workers: 2,
  reporter: [
    ['list'],
    ['html', { outputFolder: '../screenshots/report', open: 'never' }],
  ],
  expect: {
    timeout: 15_000,
    toHaveScreenshot: {
      animations: 'disabled',
      caret: 'hide',
      scale: 'css',
      maxDiffPixelRatio: 0.001,
    },
  },
  use: {
    baseURL: externalBaseUrl ?? 'http://127.0.0.1:5287',
    serviceWorkers: 'block',
    colorScheme: 'light',
    // The suite validates rendered pages. TLS availability belongs in a separate
    // uptime check and local inspection proxies may use an untrusted root.
    ignoreHTTPSErrors: true,
    locale: 'pl-PL',
    timezoneId: 'Europe/Warsaw',
    trace: 'retain-on-failure',
  },
  projects: [
    {
      name: 'desktop-chromium',
      use: {
        ...devices['Desktop Chrome'],
        viewport: { width: 1440, height: 1000 },
      },
    },
    {
      name: 'mobile-chromium',
      testMatch: /navigation\.visual\.spec\.ts/,
      use: {
        ...devices['Pixel 7'],
      },
    },
  ],
});
