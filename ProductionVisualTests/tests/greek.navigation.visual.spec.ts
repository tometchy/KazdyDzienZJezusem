import { expect, test } from '@playwright/test';
import { openProductionPage } from './visual-test';

test('book heading links the full translation name to its books', async ({ page }) => {
  const healthy = await openProductionPage(page, '/Biblia/TNP/Est');
  const heading = page.locator('.bible-heading');
  await expect(heading).not.toContainText('EST');
  await expect(heading.getByRole('link')).toHaveText('Toruński Przekład Nowego Przymierza');
  await expect(page.locator('.bible-translation-toggle')).toHaveText('TNP');
  await expect(page).toHaveScreenshot('bible-book-heading.png', { fullPage: true });
  await heading.getByRole('link').click();
  await expect(page).toHaveURL(/\/Biblia\/TNP$/);
  await expect(page.locator('.book-grid')).toBeVisible();
  healthy();
});

test('Greek switch annotates Bible, fathers, comparison, tags, and literature and survives navigation', async ({ page }) => {
  test.setTimeout(60_000);
  const healthy = await openProductionPage(page, '/Biblia/TR/Ob/15');
  // Real application endpoint and the bundled corpus in isolated Redis.
  const original = await page.locator('.verse-text').first().textContent();
  const toggle = page.getByRole('switch', { name: 'Polish above Greek words' });
  await toggle.check();
  await expect(page.locator('.verse-text rt').first()).toHaveText('I');
  await expect(page.locator('#greek-polish-status')).toContainText('Static Polish dictionary.');
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  await expect(page).toHaveScreenshot('greek-interlinear.png', { fullPage: true });
  await toggle.uncheck();
  await expect(page.locator('ruby')).toHaveCount(0);
  await expect(page.locator('.verse-text').first()).toHaveText(original!);
  await toggle.check();
  const menu = page.getByRole('button', { name: 'Toggle navigation' });
  if (await menu.isVisible()) await menu.click();
  await page.getByRole('link', { name: 'Ojcowie', exact: true }).click();
  await expect(toggle).toBeChecked();
  await expect(page.locator('#greek-polish-status')).toHaveText('No Greek text on this page.');
  await page.getByRole('link', { name: /Ignatius of Antioch/ }).click();
  await expect(page.locator('rt').first()).toBeVisible();
  await page.goto('/Ojcowie/Ignatius/Ef/1');
  await expect(page.locator('.verse-text rt').first()).toBeVisible();
  await page.goto('/Biblia/Werset/J/1/1?sourceTranslation=TR');
  await expect(page.locator('.translation-verse-text rt').first()).toBeVisible();
  const beginning = page.locator('.translation-verse-text ruby').filter({ hasText: 'ἀρχῇ' });
  await expect(beginning.locator('rt')).toHaveText('początku');
  await expect(beginning).toHaveAttribute('data-greek-match', 'passage');
  await page.goto('/Tag/narodzenie-jezusa');
  const select = page.locator('[data-tag-translation-select]');
  await select.selectOption('TR');
  await expect(page.locator('[data-tag-translation="TR"] .verse-text rt').first()).toBeVisible();
  await page.goto('/Teksty');
  await expect(toggle).toBeChecked();
  await expect(page.locator('#greek-polish-status')).toHaveText('No Greek text on this page.');
  // Literature is currently empty; exercise future mixed, inline Greek content.
  await page.locator('main').evaluate(main => {
    const paragraph = document.createElement('p');
    paragraph.innerHTML = 'Read <em>βιβλίον</em>, then βιβλίον·';
    main.append(paragraph);
  });
  await toggle.uncheck();
  await toggle.check();
  await expect(page.locator('main p rt')).toHaveCount(2);
  healthy();
});

test('translation outage keeps the Greek readable and allows retry', async ({ page }) => {
  await openProductionPage(page, '/Biblia/TR/J/1');
  await page.route('**/api/greek-interlinear', route => route.fulfill({ status: 503, json: { error: 'Translation is unavailable.' } }));
  const original = await page.locator('.verse-text').first().textContent();
  const toggle = page.getByRole('switch', { name: 'Polish above Greek words' });
  await toggle.check();
  await expect(page.locator('#greek-polish-status')).toContainText('Translation is unavailable.');
  await expect(page.locator('.verse-text').first()).toHaveText(original!);
  await toggle.uncheck();
  await expect(page.locator('ruby')).toHaveCount(0);
});
