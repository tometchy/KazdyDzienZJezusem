import { test } from '@playwright/test';
import { expectProductionScreenshot } from './visual-test';

test('Church Fathers index', async ({ page }) => {
  await expectProductionScreenshot(page, '/Ojcowie', 'fathers-index');
});

test('Church Father works', async ({ page }) => {
  await expectProductionScreenshot(page, '/Ojcowie/Ignatius', 'father-works');
});

test('Church Father chapter', async ({ page }) => {
  await expectProductionScreenshot(
    page,
    '/Ojcowie/Ignatius/Ef/1',
    'father-chapter',
  );
});
