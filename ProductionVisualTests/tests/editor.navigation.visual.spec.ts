import { expect, test } from '@playwright/test';
import { expectProductionScreenshot } from './visual-test';

test('hidden login and session-based tag editing', async ({ page, context }) => {
  await expectProductionScreenshot(page, '/login', 'login');
  await page.getByLabel('login', { exact: true }).fill('test-only-not-a-github-token');
  await page.getByRole('button', { name: 'login', exact: true }).click();
  await expect(page.getByRole('status')).toContainText('You are logged in');
  const cookie = (await context.cookies()).find(cookie => cookie.name === 'kdj-editor');
  expect(cookie?.httpOnly).toBe(true);
  expect(cookie?.sameSite).toBe('Strict');
  expect(cookie?.value).not.toContain('test-only');
  expect(await page.evaluate(() => JSON.stringify(localStorage))).not.toContain('test-only');
  await page.goto('/Biblia/TNP/Mt/1');
  const verse = page.locator('[data-tag-fragment="Mt1,1"]');
  await verse.getByText('Add tag', { exact: true }).click();
  await expect(verse.getByRole('combobox')).toBeVisible();
  await expect(page).toHaveScreenshot('tag-editor.png', { fullPage: true });

  // Browser rendering is tested independently of GitHub. Real authorization and
  // repository writes are covered by the backend and temporary-Git tests.
  let csrf = '';
  await page.route('**/api/tags/fragment', async route => {
    const request = route.request();
    csrf = request.headers()['x-csrf-token'];
    expect(request.postDataJSON()).toEqual({ name: 'new-tag', fragment: 'Mt1,1' });
    await route.fulfill({ json: { tags: [{ slug: 'new-tag', name: 'new-tag' }], warning: null } });
  });
  await verse.getByRole('combobox').fill('new-tag');
  await verse.getByRole('button', { name: 'Save', exact: true }).click();
  await expect(verse.getByRole('status')).toHaveText('Saved.');
  await expect(verse.getByRole('link', { name: 'new-tag' })).toBeVisible();
  expect(csrf).toBeTruthy();
  await expect(page).toHaveURL(/\/Biblia\/TNP\/Mt\/1$/);

  for (const path of ['/Biblia/UBG/Mt/1', '/Biblia/TR/Mt/1', '/Biblia/KJV/Mt/1',
    '/Biblia/Werset/Mt/1/1', '/Ojcowie/Ignatius/Ef/1']) {
    await page.goto(path);
    await expect(page.locator('.tag-editor').first()).toBeVisible();
  }
  await page.goto('/login');
  await page.getByRole('button', { name: 'Log out' }).click();
  await page.goto('/Biblia/TNP/Mt/1');
  await expect(page.locator('.tag-editor')).toHaveCount(0);
  await expect(page.locator('a[href="/login"]')).toHaveCount(0);
});
