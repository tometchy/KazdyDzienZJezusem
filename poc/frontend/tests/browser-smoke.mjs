#!/usr/bin/env node
/** Browser integration smoke: real Web Worker, OPFS and SAH Pool, no server API. */
import assert from 'node:assert/strict';
import { createServer } from 'node:http';
import { readFile, writeFile } from 'node:fs/promises';
import { resolve, extname, sep } from 'node:path';
import { chromium } from 'playwright';

const root = resolve(import.meta.dirname, '../dist');
const types = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.wasm': 'application/wasm',
  '.json': 'application/json',
  '.gz': 'application/gzip'
};
const server = createServer(async (request, response) => {
  response.setHeader('Cross-Origin-Opener-Policy', 'same-origin');
  response.setHeader('Cross-Origin-Embedder-Policy', 'require-corp');
  response.setHeader('Cache-Control', 'no-store');
  const path = decodeURIComponent(new URL(request.url, 'http://localhost').pathname);
  const relative = path === '/POC/' || path === '/POC' ? 'index.html'
    : path.startsWith('/POC/') ? path.slice(5) : '';
  const file = resolve(root, relative);
  if (!relative || (file !== root && !file.startsWith(root + sep))) {
    response.writeHead(404).end('Not found'); return;
  }
  try {
    const bytes = await readFile(file);
    response.writeHead(200, {'Content-Type': types[extname(file)] ?? 'application/octet-stream'});
    response.end(bytes);
  } catch {
    response.writeHead(404).end('Not found');
  }
});

await new Promise((resolveListen, reject) => {
  server.once('error', reject);
  server.listen(0, '127.0.0.1', resolveListen);
});
const origin = 'http://127.0.0.1:' + server.address().port;
const browser = await chromium.launch({headless: true});
const page = await browser.newPage({serviceWorkers: 'block'});
const reports = [];
page.on('pageerror', error => console.error('PAGE ERROR:', error.message));
page.on('console', message => { if (message.type() === 'error') console.error('BROWSER ERROR:', message.text()); });
const ready = async (target = page) => {
  await target.waitForFunction(
    () => {
      const status = document.querySelector('#status')?.textContent ?? '';
      return status.startsWith('Gotowe') || status.startsWith('Błąd');
    }, null, {timeout: 180_000}
  );
  const status = await target.locator('#status').innerText();
  assert.ok(status.startsWith('Gotowe'), 'SQLite worker failed: ' + status);
  assert.equal(await target.locator('#bench').isEnabled(), true);
};
const run = async (strategy, phase) => {
  await ready();
  await page.locator('#query').fill('source:Slownik AND gr:λόγος');
  await page.locator('#search').click();
  await page.waitForFunction(
    () => document.querySelector('#search-stats')?.textContent?.includes('trafień'),
    null, {timeout: 30000}
  );
  const stat = await page.locator('#search-stats').innerText();
  assert.ok(!stat.startsWith('0 trafień'), 'Greek dictionary forms must be searchable: ' + stat);
  const before = await page.evaluate(() => {
    const list = JSON.parse(localStorage.getItem('kdj.search-poc.reports.v2') ?? '[]');
    return list[0]?.created_at ?? '';
  });
  await page.locator('#bench').click();
  await page.waitForFunction(
    before => {
      const list = JSON.parse(localStorage.getItem('kdj.search-poc.reports.v2') ?? '[]');
      return !!list[0] && list[0].created_at !== before;
    }, before, {timeout: 180000}
  );
  const report = await page.evaluate(
    () => JSON.parse(localStorage.getItem('kdj.search-poc.reports.v2'))[0]
  );
  assert.equal(report.meta.strategy, strategy);
  assert.equal(report.meta.documents > 110000, true);
  assert.equal(report.results.length, 10);
  assert.equal(report.results.find(x => x.name === 'Grecka forma w słowniku').hits > 0, true);
  if (phase === 'cold') {
    assert.ok(report.meta.downloaded_bytes > 0, strategy + ': cold start must download data');
    if (strategy !== 'local-build') {
      assert.equal(report.meta.index_imported, true);
      assert.equal(report.meta.index_build_ms, 0);
    }
  } else {
    assert.equal(report.meta.index_reused, true, strategy + ': warm start must reuse index');
    assert.equal(report.meta.downloaded_bytes, 0);
  }
  reports.push({strategy, phase, report});
  console.log(strategy, phase, 'ready=', report.meta.total_ready_ms,
    'downloaded=', report.meta.downloaded_bytes,
    'import=', report.meta.import_ms, 'build=', report.meta.index_build_ms,
    'p50=', report.summary.typical_top20_p50_ms);
};
try {
  await page.goto(origin + '/POC/', {waitUntil: 'domcontentloaded'});
  assert.equal(await page.evaluate(() => crossOriginIsolated), true);
  for (const strategy of ['local-build', 'prebuilt-opfs', 'prebuilt-sahpool']) {
    if (strategy !== 'local-build') {
      await page.locator('#strategy').selectOption(strategy);
    }
    await run(strategy, 'cold');
    await page.locator('#reopen').click();
    await run(strategy, 'warm');
    // Confirm a reset re-imports only this variant without erasing previous reports.
    page.once('dialog', dialog => dialog.accept());
    await page.locator('#reset').click();
    await ready();
    const text = await page.locator('#download-bytes').innerText();
    assert.ok(text !== '0 MiB', 'Reset must cause a fresh download: ' + text);
    assert.ok((await page.locator('#history button').count()) >= reports.length);
  }
  const baselineHits = reports[0].report.results.map(entry => [entry.query, entry.hits]);
  for (const run of reports) {
    assert.deepEqual(run.report.results.map(entry => [entry.query, entry.hits]),
      baselineHits, 'FTS hit counts must match for all variants and starts: ' +
      run.strategy + ' / ' + run.phase);
  }
  // Real integration of the single-button suite, including isolated purge and
  // 3 fresh-worker warm starts per variant. This also verifies one persisted JSON.
  await ready();
  await page.locator('#run-all').click();
  await page.waitForFunction(
    () => {
      try {
        const row = JSON.parse(localStorage.getItem('kdj.search-poc.full-suite.v1') ?? 'null');
        return row?.version === 3;
      } catch { return false; }
    }, null, {timeout: 900000}
  );
  const suite = await page.evaluate(
    () => JSON.parse(localStorage.getItem('kdj.search-poc.full-suite.v1'))
  );
  assert.equal(suite.status, 'completed', 'Full suite error: ' + suite.error);
  assert.equal(suite.runs.length, 12);
  assert.equal(suite.summary.runs_completed, 12);
  assert.equal(suite.summary.hit_counts_consistent, true);
  assert.equal(suite.configuration.http_cache_cleared, false);
  assert.equal(suite.configuration.fresh_worker_for_every_run, true);
  for (const strategy of ['local-build', 'prebuilt-opfs', 'prebuilt-sahpool']) {
    const selected = suite.runs.filter(row => row.strategy === strategy);
    assert.deepEqual(selected.map(row => row.phase), ['cold', 'warm', 'warm', 'warm']);
    assert.deepEqual(selected.map(row => row.repetition), [1, 1, 2, 3]);
    assert.equal(selected[0].report.meta.index_reused, false);
    assert.ok(selected[0].report.meta.downloaded_bytes > 0);
    assert.ok(selected[0].report.meta.index_built || selected[0].report.meta.index_imported);
    for (const warm of selected.slice(1)) {
      assert.equal(warm.report.meta.index_reused, true);
      assert.equal(warm.report.meta.downloaded_bytes, 0);
      assert.equal(warm.report.results.length, 10);
      assert.deepEqual(warm.report.results.map(x => [x.query, x.hits]),
        baselineHits, 'Full suite must preserve FTS hit parity');
    }
    assert.equal(suite.summary.by_strategy[strategy].warm_runs, 3);
    assert.ok(suite.summary.by_strategy[strategy].warm_total_ready_p50_ms > 0);
  }
  assert.equal(await page.locator('#suite-copy').isEnabled(), true);
  assert.equal(await page.locator('#suite-download').isEnabled(), true);
  // Two browser tabs share ONE origin-wide SQLite database and must never
  // leave an exclusive SAH Pool open in the inactive tab.
  const second = await page.context().newPage();
  second.on('pageerror', error => console.error('SECOND PAGE ERROR:', error.message));
  try {
    await page.locator('#strategy').selectOption('prebuilt-sahpool');
    await ready();
    await second.goto(origin + '/POC/', {waitUntil: 'domcontentloaded'});
    await ready(second);
    await second.locator('#strategy').selectOption('prebuilt-sahpool');
    await ready(second);
    const searchIn = async tab => {
      await tab.locator('#query').fill('source:Slownik AND gr:λόγος');
      await tab.evaluate(() => { document.querySelector('#search-stats').textContent = ''; });
      await tab.locator('#search').click();
      await tab.waitForFunction(() =>
        document.querySelector('#search-stats')?.textContent?.includes('trafień'),
        null, {timeout: 60000});
      const hits = await tab.locator('#search-stats').innerText();
      assert.ok(!hits.startsWith('0 trafień'), 'Cannot query from second tab: ' + hits);
    };
    // Simultaneous queries, not sequential opens: each tab must obtain access
    // and return hits instead of failing due to a locked SAH pool.
    await Promise.all([searchIn(page), searchIn(second)]);
    // Reset/import in one tab while the other stays open must not lock it out.
    page.once('dialog', dialog => dialog.accept());
    await page.locator('#reset').click();
    await ready();
    await searchIn(second);
    await searchIn(page);
    console.log('PASS multi-tab SAH Pool: 2 tabs, concurrent search, reset and reopen');
  } finally {
    await second.close();
  }
  await writeFile(resolve(root, 'poc-browser-full-suite-results.json'),
    JSON.stringify(suite, null, 2));
  console.log('PASS single-button full suite: 12 cold/warm Chromium runs');
  await writeFile(resolve(root, 'poc-browser-smoke-results.json'),
    JSON.stringify(reports, null, 2));
  console.log('PASS browser OPFS comparison on Chromium,', reports.length, 'reports');
} finally {
  await page.close();
  await browser.close();
  await new Promise(resolveClose => server.close(resolveClose));
}
