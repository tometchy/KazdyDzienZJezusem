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
  await expect(page.locator('#greek-polish-status')).toContainText('Static dictionary.');
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

test('all annotation settings are independent, persistent, and stacked in the requested order', async ({ page }) => {
  const healthy = await openProductionPage(page, '/Biblia/Werset/Mt/1/1?sourceTranslation=TR');
  const names = ['polish', 'english', 'grammar', 'pronunciation'];
  const switches = names.map(name => page.locator(`#greek-${name}-toggle`));
  const order = ['grammar', 'english', 'polish', 'pronunciation'];
  const original = await page.locator('.translation-verse-text').allTextContents();
  for (let mask = 1; mask < 16; mask++) {
    for (let index = 0; index < names.length; index++) await switches[index].setChecked(Boolean(mask & (1 << index)));
    const expected = order.filter(name => mask & (1 << names.indexOf(name)));
    const layers = page.locator('ruby').first().locator('rt [data-greek-layer]');
    await expect(layers).toHaveCount(expected.length);
    await expect.poll(() => layers.evaluateAll(nodes => nodes.map(node => (node as HTMLElement).dataset.greekLayer)))
      .toEqual(expected);
  }
  const first = page.locator('ruby').first();
  await expect(first.locator('[data-greek-layer="grammar"]')).toHaveText('N-NSF');
  await expect(first.locator('[data-greek-layer="pronunciation"]')).toHaveText('biblos');
  const second = page.locator('ruby').nth(1);
  await expect(second.locator('[data-greek-layer="grammar"]')).toHaveText('N-GSF');
  await expect(second.locator('[data-greek-layer="pronunciation"]')).toHaveText('geneseos');
  const geometry = await first.evaluate(word => ['grammar', 'english', 'polish', 'pronunciation']
    .map(name => word.querySelector(`[data-greek-layer="${name}"]`)!.getBoundingClientRect().top)
    .concat(word.querySelector('.greek-source-word')!.getBoundingClientRect().top));
  expect(geometry.every((position, index) => index === 0 || position > geometry[index - 1])).toBe(true);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  await test.info().attach('All Greek reading layers', {
    body: await page.screenshot({ fullPage: true }), contentType: 'image/png',
  });
  await expect(page).toHaveScreenshot('greek-all-annotations.png', { fullPage: true });
  // A mixed preference survives a full reload independently for every layer.
  await switches[0].uncheck();
  await switches[2].uncheck();
  await page.reload();
  for (let index = 0; index < names.length; index++)
    await expect(switches[index]).toBeChecked({ checked: index === 1 || index === 3 });
  await expect(page.locator('[data-greek-layer="polish"]')).toHaveCount(0);
  await expect(page.locator('[data-greek-layer="grammar"]')).toHaveCount(0);
  await expect(page.locator('[data-greek-layer="english"]').first()).toBeVisible();
  for (const toggle of switches) await toggle.uncheck();
  await expect(page.locator('ruby')).toHaveCount(0);
  expect(await page.locator('.translation-verse-text').allTextContents()).toEqual(original);
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

test('offline supplements label dictionary meanings in patristic Greek without substituting Polish lemmas', async ({ page }) => {
  const healthy = await openProductionPage(page, '/Ojcowie/Ignatius/Ef/1');
  for (const name of ['polish', 'english', 'pronunciation', 'grammar'])
    await page.locator(`#greek-${name}-toggle`).check();
  const word = page.locator('ruby[data-greek-original="κέκτησθε"]');
  await expect(word.locator('[data-greek-layer="english"]')).toHaveAttribute('data-greek-dictionary', 'true');
  await expect(word.locator('[data-greek-layer="english"]')).toContainText('†');
  await expect(word.locator('[data-greek-layer="polish"]')).toHaveText('?');
  await expect(word.locator('[data-greek-layer="pronunciation"]')).toHaveText('kekteste');
  await expect(page.locator('#greek-supplement-source')).toBeVisible();
  await expect(page.locator('#greek-perseus-source')).toBeVisible();
  for (const [greek, english, pronunciation] of [
    ['ἀναζωπυρήσαντες', 'rekindle', 'anadzopyresantes'],
    ['ἀπηρτίσατε', 'complete', 'apertisate'],
  ]) {
    const added = page.locator(`ruby[data-greek-original="${greek}"]`);
    await expect(added.locator('[data-greek-layer="english"]')).toContainText(english);
    await expect(added.locator('[data-greek-layer="english"]')).toHaveAttribute('data-greek-dictionary', 'true');
    await expect(added.locator('[data-greek-layer="pronunciation"]')).toHaveText(pronunciation);
    await expect(added.locator('[data-greek-layer="polish"]')).toHaveText('?');
    await expect(added.locator('[data-greek-layer="grammar"]')).not.toHaveText('?');
  }
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  await test.info().attach('Patristic dictionary supplements', {
    body: await page.screenshot({ fullPage: true }), contentType: 'image/png',
  });
  await expect(page).toHaveScreenshot('greek-fathers-supplement.png', { fullPage: true });
  healthy();
});
