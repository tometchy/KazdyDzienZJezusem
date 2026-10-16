import { expect, test } from '@playwright/test';
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

test('chapter with linked tags', async ({ page }) => {
  await expectProductionScreenshot(page, '/Biblia/TNP/Mt/1', 'tagged-chapter');
  await expect(page.getByRole('link', { name: 'narodzenie-jezusa' }).first()).toBeVisible();
});
