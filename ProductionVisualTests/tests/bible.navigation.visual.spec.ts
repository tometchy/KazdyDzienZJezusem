import { expect, Page, test } from '@playwright/test';
import { openProductionPage } from './visual-test';

function translationMenu(page: Page) {
  return page.locator('.bible-translation-breadcrumb');
}

async function chooseTranslation(page: Page, code: string) {
  const menu = translationMenu(page);
  await menu.getByRole('button', { name: 'Bible translation', exact: true }).click();
  await menu.getByRole('link', { name: new RegExp(`^${code} —`) }).click();
  await expect(menu.getByRole('button', { name: 'Bible translation', exact: true }))
    .toHaveText(code);
}

for (const { level, suffix, content } of [
  { level: 'book list', suffix: '', content: '.book-grid' },
  { level: 'chapter list', suffix: '/Ob', content: '.chapter-grid' },
  { level: 'chapter', suffix: '/Ob/15', content: '.verse-list' },
]) {
  test(`changing the breadcrumb translation preserves the ${level}`, async ({ page }) => {
    const expectHealthyResources = await openProductionPage(page, `/Biblia/UBG${suffix}`);

    await chooseTranslation(page, 'KJV');

    await expect(page).toHaveURL(new RegExp(`/Biblia/KJV${suffix}$`));
    await expect(page.locator(content)).toBeVisible();
    if (level === 'chapter') {
      await expect(page.locator('.verse-text').first())
        .toContainText('And I saw another sign in heaven');
    }
    expectHealthyResources();
  });
}

test('Bible translation dropdown', async ({ page }) => {
  await openProductionPage(page, '/Biblia/UBG/Ob/15');
  const menu = translationMenu(page);
  await menu.getByRole('button', { name: 'Bible translation', exact: true }).click();
  await expect(menu.getByRole('link', { name: /^KJV —/ })).toBeVisible();
  await expect(page).toHaveScreenshot('bible-translation-dropdown.png', { fullPage: true });
});

for (const initialHash of ['#3', '#w3']) {
  test(`translation changes retain ${initialHash} and the latest verse after repeated Unpoly navigation`, async ({ page }) => {
    const expectHealthyResources = await openProductionPage(page, `/Biblia/UBG/Ob/15${initialHash}`);
    // A full reload would remove this marker and hide a broken Unpoly lifecycle.
    await page.evaluate(() => { document.documentElement.dataset.bibleNavigationMarker = 'present'; });
    await expect(page).toHaveURL(/\/Biblia\/UBG\/Ob\/15#3$/);

    await chooseTranslation(page, 'KJV');
    await expect(page).toHaveURL(/\/Biblia\/KJV\/Ob\/15#3$/);
    await expect(page.locator('.bible-verse.is-targeted')).toHaveAttribute('id', '3');

    // The menu must read the current fragment, not retain the original #3.
    await page.evaluate(() => { window.location.hash = '#5'; });
    await expect(page.locator('.bible-verse.is-targeted')).toHaveAttribute('id', '5');
    await chooseTranslation(page, 'TNP');
    await expect(page).toHaveURL(/\/Biblia\/TNP\/Ob\/15#5$/);
    await expect(page.locator('.bible-verse.is-targeted')).toHaveAttribute('id', '5');
    await chooseTranslation(page, 'UBG');
    await expect(page).toHaveURL(/\/Biblia\/UBG\/Ob\/15#5$/);
    await expect(page.locator('.bible-verse.is-targeted')).toHaveAttribute('id', '5');
    await expect(page.locator('html')).toHaveAttribute('data-bible-navigation-marker', 'present');

    // Going up one level must not carry a verse fragment to the chapter list.
    await page.getByRole('link', { name: 'Wszystkie rozdziały', exact: true }).first().click();
    await expect(page.locator('.chapter-grid')).toBeVisible();
    await chooseTranslation(page, 'KJV');
    await expect(page).toHaveURL(/\/Biblia\/KJV\/Ob$/);
    await expect(page.locator('html')).toHaveAttribute('data-bible-navigation-marker', 'present');
    expectHealthyResources();
  });
}

for (const suffix of ['/Rdz', '/Rdz/1']) {
  test(`translations without the current book are unavailable at ${suffix}`, async ({ page }) => {
    const expectHealthyResources = await openProductionPage(page, `/Biblia/UBG${suffix}`);
    const menu = translationMenu(page);
    await menu.getByRole('button', { name: 'Bible translation', exact: true }).click();
    for (const code of ['TNP', 'TR']) {
      const unavailable = menu.locator('.dropdown-item').filter({ hasText: new RegExp(`^\\s*${code} —`) });
      await expect(unavailable).toBeVisible();
      await expect(unavailable).toHaveAttribute('aria-disabled', 'true');
      await expect(menu.getByRole('link', { name: new RegExp(`^${code} —`) })).toHaveCount(0);
    }
    await menu.getByRole('link', { name: /^KJV —/ }).click();
    await expect(page).toHaveURL(new RegExp(`/Biblia/KJV${suffix}$`));
    expectHealthyResources();
  });
}

test('changing comparison translation keeps the verse and updates its return links', async ({ page }) => {
  const expectHealthyResources = await openProductionPage(
    page, '/Biblia/Werset/Ob/15/3?sourceTranslation=UBG',
  );
  await chooseTranslation(page, 'KJV');
  await expect(page).toHaveURL(/\/Biblia\/Werset\/Ob\/15\/3\?sourceTranslation=KJV$/);
  await expect(page.locator('.translation-comparison')).toBeVisible();
  const breadcrumb = page.getByRole('navigation', { name: 'breadcrumb', exact: true });
  await expect(breadcrumb.getByRole('link', { name: 'Ob', exact: true }))
    .toHaveAttribute('href', '/Biblia/KJV/Ob');
  await expect(breadcrumb.getByRole('link', { name: 'Rozdział 15', exact: true }))
    .toHaveAttribute('href', '/Biblia/KJV/Ob/15');
  const returnLink = page.getByRole('link', { name: '← Wróć do rozdziału', exact: true });
  await expect(returnLink).toHaveAttribute('href', '/Biblia/KJV/Ob/15#3');
  await returnLink.click();
  await expect(page).toHaveURL(/\/Biblia\/KJV\/Ob\/15#3$/);
  await expect(page.locator('.bible-verse.is-targeted')).toHaveAttribute('id', '3');
  expectHealthyResources();
});
