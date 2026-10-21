import { expect, test } from '@playwright/test';
import path from 'node:path';

const tagPath = '/Tag/narodzenie-jezusa';
const storageKey = 'kdj:tag-translation:narodzenie-jezusa';
const unrelatedStorageKey = 'kdj:tag-translation:another-tag';

test.beforeEach(async ({ page, baseURL }) => {
  await page.route('https://cdn.jsdelivr.net/npm/unpoly@3.14.3/*', async route => {
    const asset = new URL(route.request().url()).pathname.split('/').pop()!;
    await route.fulfill({
      path: path.resolve(__dirname, '../node_modules/unpoly', asset),
      headers: { 'Access-Control-Allow-Origin': new URL(baseURL!).origin },
    });
  });
});

test('tag passages use Torun by default and remember a changed translation across reload and navigation', async ({ page }) => {
  await page.goto(tagPath);
  const selector = page.getByLabel('Translation', { exact: true });
  const visibleVerses = page.locator('[data-tag-translation]:visible .tag-passage .verse-text');

  await expect(selector).toHaveValue('TNP');
  await expect(visibleVerses).toHaveCount(28);
  await expect(visibleVerses.first()).toContainText('A z narodzeniem Jezusa – Mesjasza było tak');
  await expect(page.getByRole('link', { name: 'Mt1,18-25', exact: true }))
    .toHaveAttribute('href', '/Biblia/TNP/Mt/1#18');

  await selector.selectOption('KJV');
  await expect(page.locator('[data-tag-translation="TNP"]')).toBeHidden();
  await expect(visibleVerses).toHaveCount(28);
  await expect(visibleVerses.first()).toContainText('Now the birth of Jesus Christ was on this wise');
  await expect(page.getByRole('link', { name: 'Mt1,18-25', exact: true }))
    .toHaveAttribute('href', '/Biblia/KJV/Mt/1#18');
  expect(await page.evaluate(key => localStorage.getItem(key), storageKey)).toBe('KJV');

  await page.reload();
  await expect(selector).toHaveValue('KJV');
  await expect(visibleVerses.first()).toContainText('Now the birth of Jesus Christ was on this wise');

  // This marker survives Unpoly replacements but would disappear on a full reload.
  await page.evaluate(() => { document.documentElement.dataset.tagNavigationMarker = 'present'; });
  const navigationToggle = page.getByRole('button', { name: 'Toggle navigation' });
  if (await navigationToggle.isVisible()) await navigationToggle.click();
  await page.locator('header nav').getByRole('link', { name: 'Tagi', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Tagi', exact: true })).toBeVisible();
  await page.getByRole('link', { name: 'narodzenie-jezusa', exact: true }).click();
  await expect(selector).toHaveValue('KJV');
  await expect(visibleVerses).toHaveCount(28);
  await expect(visibleVerses.first()).toContainText('Now the birth of Jesus Christ was on this wise');
  await expect(page.locator('html')).toHaveAttribute('data-tag-navigation-marker', 'present');
});

for (const savedTranslation of [null, 'unsupported-translation']) {
  test(`tag preferences are isolated and ${savedTranslation ? 'an invalid choice' : 'no saved choice'} defaults to Torun`, async ({ page }) => {
    await page.addInitScript(({ key, unrelatedKey, saved }) => {
      localStorage.setItem(unrelatedKey, 'KJV');
      if (saved) localStorage.setItem(key, saved);
    }, { key: storageKey, unrelatedKey: unrelatedStorageKey, saved: savedTranslation });

    await page.goto(tagPath);
    const selector = page.getByLabel('Translation', { exact: true });
    await expect(selector).toHaveValue('TNP');
    await expect(page.locator('[data-tag-translation="TNP"]')).toBeVisible();

    await selector.selectOption('UBG');
    const ubgPanel = page.locator('[data-tag-translation="UBG"]');
    await expect(ubgPanel).toBeVisible();
    await expect(ubgPanel.locator('.verse-text').first())
      .toContainText('A z narodzeniem Jezusa Chrystusa było tak');
    expect(await page.evaluate(key => localStorage.getItem(key), storageKey)).toBe('UBG');
    expect(await page.evaluate(key => localStorage.getItem(key), unrelatedStorageKey)).toBe('KJV');
  });
}

test('tag translation changes remain usable when browser storage rejects reads and writes', async ({ page }) => {
  const errors: string[] = [];
  page.on('pageerror', error => errors.push(error.message));
  await page.addInitScript(() => {
    Object.defineProperty(window, 'localStorage', {
      configurable: true,
      value: {
        getItem() { throw new DOMException('Storage is blocked.', 'SecurityError'); },
        setItem() { throw new DOMException('Storage is blocked.', 'SecurityError'); },
      },
    });
  });

  await page.goto(tagPath);
  const selector = page.getByLabel('Translation', { exact: true });
  await expect(selector).toHaveValue('TNP');
  await selector.selectOption('KJV');
  await expect(page.locator('[data-tag-translation="KJV"]')).toBeVisible();
  await expect(page.locator('[data-tag-translation="KJV"] .verse-text').first())
    .toContainText('Now the birth of Jesus Christ was on this wise');
  await selector.selectOption('UBG');
  await expect(page.locator('[data-tag-translation="UBG"]')).toBeVisible();
  await expect(page.locator('[data-tag-translation="KJV"]')).toBeHidden();
  expect(errors).toEqual([]);
});
