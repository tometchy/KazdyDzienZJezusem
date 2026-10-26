import { expect, Page, test } from '@playwright/test';
import nodePath from 'node:path';

export async function openProductionPage(
  page: Page,
  path: string,
) {
  const unexpectedRequests: string[] = [];
  const failedResources: string[] = [];
  const baseOrigin = new URL(test.info().project.use.baseURL!).origin;
  // Serve the exact production CDN dependency from the pinned npm package.
  // This exercises Unpoly while keeping browser requests independent of internet access.
  await page.route('**/*', async route => {
    const url = new URL(route.request().url());
    const asset = /^\/npm\/unpoly@3\.14\.3\/(unpoly\.min\.(?:css|js))$/.exec(url.pathname);
    if (url.origin === 'https://cdn.jsdelivr.net' && asset) {
      await route.fulfill({
        path: nodePath.resolve(__dirname, '../node_modules/unpoly', asset[1]),
        headers: { 'Access-Control-Allow-Origin': baseOrigin },
      });
    } else if (url.origin === baseOrigin) {
      await route.continue();
    } else {
      unexpectedRequests.push(url.href);
      await route.abort();
    }
  });
  page.on('response', response => {
    if (response.status() >= 400) failedResources.push(`${response.status()} ${response.url()}`);
  });
  const response = await page.goto(path, { waitUntil: 'networkidle' });

  expect(response, `Expected a response for ${path}`).not.toBeNull();
  expect(response!.status(), `Expected ${path} to load successfully`).toBe(200);

  await page.evaluate(() => document.fonts.ready);
  const expectHealthyResources = () => {
    expect(unexpectedRequests, 'Unexpected external browser requests').toEqual([]);
    expect(failedResources, 'Failed page resources').toEqual([]);
  };
  expectHealthyResources();
  return expectHealthyResources;
}

export async function expectProductionScreenshot(
  page: Page,
  path: string,
  snapshotName: string,
) {
  await openProductionPage(page, path);
  await expect(page).toHaveScreenshot(`${snapshotName}.png`, {
    fullPage: true,
  });
}
