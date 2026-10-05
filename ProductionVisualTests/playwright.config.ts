import { defineConfig, devices } from '@playwright/test';

const productionBaseUrl = 'https://kazdydzienzjezusem.pl';

export default defineConfig({
  testDir: './tests',
  outputDir: 'test-results',
  fullyParallel: true,
  forbidOnly: true,
  retries: 1,
  reporter: [
    ['list'],
    ['html', { outputFolder: 'playwright-report', open: 'never' }],
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
    baseURL: process.env.KDJ_VISUAL_BASE_URL ?? productionBaseUrl,
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
