import { expect, Page } from '@playwright/test';

export async function expectProductionScreenshot(
  page: Page,
  path: string,
  snapshotName: string,
) {
  const response = await page.goto(path, { waitUntil: 'networkidle' });

  expect(response, `Expected a response for ${path}`).not.toBeNull();
  expect(response!.status(), `Expected ${path} to load successfully`).toBe(200);

  await page.evaluate(() => document.fonts.ready);
  await expect(page).toHaveScreenshot(`${snapshotName}.png`, {
    fullPage: true,
  });
}
