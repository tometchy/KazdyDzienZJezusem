import { test } from '@playwright/test';
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
