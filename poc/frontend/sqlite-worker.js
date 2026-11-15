import sqlite3InitModule from '@sqlite.org/sqlite-wasm';
import { corpusAssetUrl } from './asset-urls.js';

const dataUrl = filename => corpusAssetUrl(filename, import.meta.env.BASE_URL, self.location.href);

let db;
let mode = 'niewykryty';
let total = 0;
let manifest;
let meta;
const DATABASE_NAME = '/kdj-search-poc-v2.sqlite3';
const normalize = value => String(value ?? '').normalize('NFD').toLowerCase()
  .replaceAll('ς', 'σ').replaceAll('ł', 'l').replace(/\p{M}/gu, '');
const normalizeQuery = query => query.replace(/[\p{L}\p{M}]+/gu, word =>
  /^(AND|OR|NOT|NEAR)$/i.test(word) ? word.toUpperCase() : normalize(word));

const cases = [
  ['Polskie OR', 'pl:wiara OR pl:miłość'],
  ['Dokładna fraza', 'pl:"na początku"'],
  ['Grecka forma', 'gr:λόγος'],
  ['Greka i polski', 'gr:ἀγάπη AND pl:miłość'],
  ['Tylko TR', 'source:TR AND gr:λόγος'],
  ['Pisma ojców', 'source:Ojcowie AND gr:ἀγάπη'],
  ['Trudniejsze AND/OR', 'pl:życie AND (pl:światło OR pl:prawda)'],
  ['Grecka fraza', 'gr:"λόγος ἦν"'],
  ['Grecka forma w słowniku', 'source:Slownik AND gr:λόγος'],
  ['Tylko TNP', 'source:TNP AND pl:światło']
];

function top20(query) {
  const started = performance.now();
  const results = db.selectObjects(
    'SELECT source, ref, pl, gr, en, lemma FROM docs_fts WHERE docs_fts MATCH ? LIMIT 20',
    [normalizeQuery(query)]
  );
  return { ms: performance.now() - started, results };
}
function count(query) {
  const started = performance.now();
  const hits = db.selectValue('SELECT COUNT(*) FROM docs_fts WHERE docs_fts MATCH ?',
    [normalizeQuery(query)]);
  return { ms: performance.now() - started, hits: Number(hits) };
}
function percent(values, fraction) {
  const sorted = [...values].sort((a, b) => a - b);
  return Number(sorted[Math.min(sorted.length - 1, Math.ceil(fraction * sorted.length) - 1)].toFixed(3));
}
async function readCorpus() {
  const response = await fetch(dataUrl('corpus.jsonl.gz'));
  if (!response.ok) throw Error('Nie udało się pobrać korpusu: HTTP ' + response.status);
  // Browsers may transparently decompress responses marked Content-Encoding:gzip.
  // Inspect actual first bytes rather than assuming the .gz URL means raw gzip.
  const raw = new Uint8Array(await response.arrayBuffer());
  if (raw.length < 2) throw Error('Pusty korpus');
  if (raw[0] === 0x1f && raw[1] === 0x8b) {
    return new Blob([raw]).stream().pipeThrough(new DecompressionStream('gzip'))
      .pipeThrough(new TextDecoderStream());
  }
  if (new TextDecoder().decode(raw.subarray(0, 80)).trimStart().startsWith('{')) {
    return new Blob([raw]).stream().pipeThrough(new TextDecoderStream());
  }
  throw Error('Nieoczekiwana zawartość korpusu — sprawdź odpowiedź serwera.');
}
async function importData() {
  const started = performance.now();
  const reader = (await readCorpus()).getReader();
  let buffer = '', inserted = 0;
  db.exec('BEGIN IMMEDIATE');
  const statement = db.prepare('INSERT INTO docs_fts(source,ref,pl,gr,en,lemma) VALUES(?,?,?,?,?,?)');
  const add = row => {
    statement.bind([
      normalize(row.source), String(row.ref ?? ''), normalize(row.pl),
      normalize(row.gr), normalize(row.en), normalize(row.lemma)
    ]).stepReset().clearBindings();
    inserted++;
    if (inserted % 10000 === 0) postMessage({ type: 'progress', count: inserted });
  };
  try {
    for (;;) {
      const { done, value } = await reader.read();
      if (done) break;
      buffer += value;
      let end;
      while ((end = buffer.indexOf('\n')) >= 0) {
        const line = buffer.slice(0, end);
        buffer = buffer.slice(end + 1);
        if (line.trim()) add(JSON.parse(line));
      }
    }
    if (buffer.trim()) add(JSON.parse(buffer));
    if (inserted !== manifest.records) {
      throw Error('Niepełny korpus: ' + inserted + ' zamiast ' + manifest.records);
    }
    statement.finalize();
    db.exec({
      sql: 'INSERT OR REPLACE INTO meta(key,value) VALUES(?,?)',
      bind: ['corpus_sha256', manifest.gzip_sha256]
    });
    db.exec('COMMIT');
  } catch (error) {
    try { statement.finalize(); } catch { /* already finalized */ }
    try { db.exec('ROLLBACK'); } catch { /* ignore rollback errors */ }
    throw error;
  }
  return Math.round(performance.now() - started);
}
async function buildIndex(force) {
  const previous = db.selectValue("SELECT value FROM meta WHERE key='corpus_sha256'");
  const complete = db.selectValue("SELECT name FROM sqlite_master WHERE type='table' AND name='docs_fts'");
  const mustBuild = force || previous !== manifest.gzip_sha256 || !complete;
  let time = 0;
  if (mustBuild) {
    db.exec('DROP TABLE IF EXISTS docs_fts');
    db.exec("CREATE VIRTUAL TABLE docs_fts USING fts5(source,ref UNINDEXED,pl,gr,en,lemma, tokenize='unicode61 remove_diacritics 2')");
    time = await importData();
  }
  total = Number(db.selectValue('SELECT COUNT(*) FROM docs_fts'));
  return { mustBuild: Boolean(mustBuild), time };
}
async function init() {
  const started = performance.now();
  const sqlite3 = await sqlite3InitModule();
  const wasmBoot = Math.round(performance.now() - started);
  const res = await fetch(dataUrl('corpus-manifest.json'), { cache: 'no-store' });
  if (!res.ok) throw Error('Brak manifestu korpusu (HTTP ' + res.status + '; ' + res.url + ')');
  manifest = await res.json();
  if (manifest.synthetic || manifest.records < 1) throw Error('Benchmark wymaga rzeczywistych danych');

  const storageErrors = [];
  if (sqlite3.oo1.OpfsDb) {
    try {
      db = new sqlite3.oo1.OpfsDb(DATABASE_NAME, 'c');
      mode = 'OPFS (trwały)';
    } catch (error) { storageErrors.push(String(error)); }
  }
  if (!db && sqlite3.installOpfsSAHPoolVfs) {
    try {
      const pool = await sqlite3.installOpfsSAHPoolVfs();
      db = new pool.OpfsSAHPoolDb(DATABASE_NAME);
      mode = 'OPFS SAH (trwały)';
    } catch (error) { storageErrors.push(String(error)); }
  }
  if (!db) {
    db = new sqlite3.oo1.DB();
    mode = 'Tylko RAM (nietrwały)';
  }
  const opened = performance.now();
  db.exec('CREATE TABLE IF NOT EXISTS meta(key TEXT PRIMARY KEY, value TEXT)');
  const indexed = await buildIndex(false);
  meta = {
    engine: 'SQLite FTS5 / WASM',
    sqlite_version: sqlite3.version.libVersion,
    mode, persisted: mode.startsWith('OPFS'),
    storage_errors: storageErrors,
    documents: total,
    sources: manifest.sources,
    corpus_gzip_bytes: manifest.compressed_bytes,
    corpus_hash: manifest.gzip_sha256,
    wasm_boot_ms: wasmBoot,
    index_built: indexed.mustBuild,
    index_build_ms: indexed.time,
    db_open_and_index_ms: Math.round(performance.now() - opened),
    total_ready_ms: Math.round(performance.now() - started),
    cross_origin_isolated: globalThis.crossOriginIsolated
  };
  return meta;
}
async function rebuild() {
  const started = performance.now();
  const indexed = await buildIndex(true);
  meta = { ...meta, index_built: true, index_build_ms: indexed.time,
    total_ready_ms: Math.round(performance.now() - started) };
  return meta;
}
function benchmark(rounds = 20) {
  const results = [];
  for (const [name, query] of cases) {
    const times = [], counts = [];
    let hits = 0;
    for (let i = 0; i < rounds; i++) {
      times.push(top20(query).ms);
      const measured = count(query);
      counts.push(measured.ms);
      hits = measured.hits;
    }
    results.push({
      name, query, hits,
      top20_p50_ms: percent(times, 0.5), top20_p95_ms: percent(times, 0.95),
      count_p50_ms: percent(counts, 0.5), count_p95_ms: percent(counts, 0.95)
    });
  }
  const top50 = results.map(r => r.top20_p50_ms);
  const top95 = results.map(r => r.top20_p95_ms);
  return {
    version: 1, created_at: new Date().toISOString(), engine: 'SQLite FTS5 / WASM',
    device: { user_agent: navigator.userAgent, hardware_concurrency: navigator.hardwareConcurrency ?? null },
    meta: { ...meta },
    rounds, results,
    summary: { typical_top20_p50_ms: percent(top50, 0.5),
      slowest_top20_p95_ms: Math.max(...top95) }
  };
}
self.onmessage = async ({ data: message }) => {
  try {
    if (message.cmd === 'init') postMessage({ type: 'ready', meta: await init() });
    else if (message.cmd === 'rebuild') postMessage({ type: 'ready', meta: await rebuild() });
    else if (message.cmd === 'search') {
      const limited = top20(message.query), full = count(message.query);
      postMessage({ type: 'search', ...limited, count: full.hits, countMs: full.ms });
    } else if (message.cmd === 'benchmark') {
      postMessage({ type: 'benchmark', report: benchmark() });
    }
  } catch (error) {
    postMessage({ type: 'error', message: String(error), stack: error?.stack ?? '' });
  }
};
