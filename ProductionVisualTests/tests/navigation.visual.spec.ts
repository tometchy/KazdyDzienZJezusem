import { expect, test } from '@playwright/test';
import { readdirSync } from 'node:fs';
import path from 'node:path';
import { expectProductionScreenshot } from './visual-test';

test('Bible translation selection', async ({ page }) => {
  await expectProductionScreenshot(page, '/Biblia', 'bible-index');
});

test('Bible chapter', async ({ page }) => {
  await expectProductionScreenshot(page, '/Biblia/TNP/1Kor/1', 'bible-chapter');
});

test('verse comparison', async ({ page }) => {
  await expectProductionScreenshot(
    page,
    '/Biblia/Werset/Ob/15/1?sourceTranslation=UBG',
    'verse-comparison',
  );
});

test('tag details', async ({ page }) => {
  await expectProductionScreenshot(page, '/Tag/narodzenie-jezusa', 'tag-details');
});

test('tag index lists the complete catalog', async ({ page }) => {
  await expectProductionScreenshot(page, '/Tagi', 'tag-index');
  const tags = readdirSync(path.resolve(__dirname, '../../tags'))
    .filter(file => file.endsWith('.md'))
    .map(file => path.basename(file, '.md'))
    .sort((left, right) => left.localeCompare(right, 'pl'));
  const links = page.locator('main li a');
  await expect(links).toHaveText(tags);
  for (const tag of tags) {
    await expect(page.getByRole('link', { name: tag, exact: true }))
      .toHaveAttribute('href', `/Tag/${encodeURIComponent(tag)}`);
  }
  await links.first().click();
  await expect(page.getByRole('heading', { name: tags[0], exact: true })).toBeVisible();
});

test('navigation links to literature and tags', async ({ page }) => {
  await expectProductionScreenshot(page, '/Teksty', 'literature');
  const toggle = page.getByRole('button', { name: 'Toggle navigation' });
  if (await toggle.isVisible()) await toggle.click();
  const navigation = page.locator('header nav');
  await expect(navigation.getByRole('link', { name: 'Teksty', exact: true })).toHaveCount(0);
  await navigation.getByRole('link', { name: 'Tagi', exact: true }).click();
  await expect(page).toHaveURL(/\/Tagi$/);
  await expect(page.getByRole('heading', { name: 'Tagi', exact: true })).toBeVisible();
  if (await toggle.isVisible() && await toggle.getAttribute('aria-expanded') === 'false') {
    await toggle.click();
  }
  await navigation.getByRole('link', { name: 'Literatura', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Literatura', exact: true })).toBeVisible();
});

test('chapter with linked tags', async ({ page }) => {
  await expectProductionScreenshot(page, '/Biblia/TNP/Mt/1', 'tagged-chapter');
  await expect(page.getByRole('link', { name: 'narodzenie-jezusa' }).first()).toBeVisible();
});
