import sqlite3InitModule from '@sqlite.org/sqlite-wasm';
import { corpusAssetUrl } from './asset-urls.js';

const asset = name => corpusAssetUrl(name, import.meta.env.BASE_URL, self.location.href);
const DATABASES = Object.freeze({
  'local-build': '/kdj-search-poc-v2.sqlite3', // Keep the original POC database intact.
  'prebuilt-opfs': '/kdj-prebuilt-opfs-v1.sqlite3',
  'prebuilt-sahpool': '/kdj-prebuilt-sahpool-v1.sqlite3'
});
export const STRATEGY_NAMES = Object.keys(DATABASES);
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

let sqlite3, pool, db, manifest, meta, strategy, mode, total;
const now = () => performance.now();
const ms = x => Number(x.toFixed(2));

function top20(query) {
  const start = now();
  const results = db.selectObjects(
    'SELECT source,ref,pl,gr,en,lemma FROM docs_fts WHERE docs_fts MATCH ? LIMIT 20',
    [normalizeQuery(query)]);
  return { ms: now() - start, results };
}
function count(query) {
  const start = now();
  const hits = db.selectValue('SELECT COUNT(*) FROM docs_fts WHERE docs_fts MATCH ?',
    [normalizeQuery(query)]);
  return { ms: now() - start, hits: Number(hits) };
}
function percent(values, fraction) {
  const sorted = [...values].sort((a, b) => a - b);
  return Number(sorted[Math.min(sorted.length - 1, Math.ceil(fraction * sorted.length) - 1)].toFixed(3));
}
function validateDatabase(expectedSchema) {
  try {
    const sha = db.selectValue("SELECT value FROM meta WHERE key='corpus_sha256'");
    const table = db.selectValue("SELECT name FROM sqlite_master WHERE type='table' AND name='docs_fts'");
    const schema = db.selectValue("SELECT value FROM meta WHERE key='schema_version'");
    const rows = Number(db.selectValue("SELECT COUNT(*) FROM docs_fts"));
    return sha === manifest.gzip_sha256 && table === 'docs_fts' &&
      rows === manifest.records && (!expectedSchema || schema === '2');
  } catch { return false; }
}
async function downloadManifest() {
  const response = await fetch(asset('corpus-manifest.json'), { cache: 'no-store' });
  if (!response.ok) throw Error('Brak manifestu (HTTP ' + response.status + ')');
  manifest = await response.json();
  if (manifest.synthetic || !manifest.records || !manifest.gzip_sha256)
    throw Error('Niepoprawny manifest rzeczywistego korpusu');
}
function bytesHex(buffer) {
  return [...new Uint8Array(buffer)].map(x => x.toString(16).padStart(2, '0')).join('');
}
async function verifiedDownload(filename, length, sha) {
  const start = now();
  const response = await fetch(asset(filename));
  if (!response.ok) throw Error('Błąd pobierania ' + filename + ': HTTP ' + response.status);
  const bytes = new Uint8Array(await response.arrayBuffer());
  const downloadMs = ms(now() - start);
  const verifyStart = now();
  const isGzip = bytes[0] === 0x1f && bytes[1] === 0x8b;
  // Do not trust Content-Encoding: the server may transparently decompress.
  if (isGzip) {
    if (bytes.byteLength !== length)
      throw Error('Nieprawidłowy rozmiar pobranego archiwum: ' + filename);
    if (bytesHex(await crypto.subtle.digest('SHA-256', bytes)) !== sha)
      throw Error('Niepoprawny SHA-256 pobranego archiwum: ' + filename);
  }
  return { bytes, isGzip, downloadMs, verifyMs: ms(now() - verifyStart) };
}
async function inflate(download) {
  const start = now();
  const bytes = download.isGzip
    ? new Uint8Array(await new Response(
        new Blob([download.bytes]).stream().pipeThrough(new DecompressionStream('gzip'))
      ).arrayBuffer())
    : download.bytes;
  return { bytes, decompressionMs: ms(now() - start) };
}
async function readPreparedSnapshot() {
  const data = await verifiedDownload('corpus-fts5.sqlite3.gz',
    manifest.sqlite_gzip_bytes, manifest.sqlite_gzip_sha256);
  const uncompressed = await inflate(data);
  if (uncompressed.bytes.byteLength !== manifest.sqlite_bytes)
    throw Error('Niepoprawny rozmiar rozpakowanej bazy');
  if (new TextDecoder().decode(uncompressed.bytes.subarray(0, 16)) !== 'SQLite format 3\u0000')
    throw Error('Pobrany plik nie jest bazą SQLite');
  const verifyStart = now();
  if (bytesHex(await crypto.subtle.digest('SHA-256', uncompressed.bytes)) !== manifest.sqlite_sha256)
    throw Error('Niepoprawny SHA-256 rozpakowanej bazy');
  return {
    bytes: uncompressed.bytes,
    downloadMs: data.downloadMs,
    downloadedBytes: data.bytes.byteLength,
    decompressionMs: uncompressed.decompressionMs,
    verifyMs: ms(data.verifyMs + now() - verifyStart)
  };
}
async function readCorpus() {
  const data = await verifiedDownload('corpus.jsonl.gz',
    manifest.compressed_bytes, manifest.gzip_sha256);
  const inflated = await inflate(data);
  return {
    ...inflated, downloadMs: data.downloadMs,
    downloadedBytes: data.bytes.byteLength, verifyMs: data.verifyMs
  };
}
async function buildIndex() {
  const corpus = await readCorpus();
  const insertStart = now();
  const text = new TextDecoder().decode(corpus.bytes);
  db.exec('DROP TABLE IF EXISTS docs_fts');
  db.exec("CREATE VIRTUAL TABLE docs_fts USING fts5(source,ref UNINDEXED,pl,gr,en,lemma, tokenize='unicode61 remove_diacritics 2')");
  db.exec('BEGIN IMMEDIATE');
  const insert = db.prepare('INSERT INTO docs_fts(source,ref,pl,gr,en,lemma) VALUES(?,?,?,?,?,?)');
  let added = 0;
  try {
    for (const line of text.split('\n')) {
      if (!line) continue;
      const row = JSON.parse(line);
      insert.bind([normalize(row.source), String(row.ref ?? ''), normalize(row.pl),
        normalize(row.gr), normalize(row.en), normalize(row.lemma)]).stepReset().clearBindings();
      if (++added % 10000 === 0) postMessage({ type: 'progress', count: added });
    }
    insert.finalize();
    if (added !== manifest.records) throw Error('Niepełny korpus: ' + added);
    db.exec({sql: 'INSERT OR REPLACE INTO meta(key,value) VALUES(?,?)',
      bind: ['corpus_sha256', manifest.gzip_sha256]});
    db.exec({sql: 'INSERT OR REPLACE INTO meta(key,value) VALUES(?,?)',
      bind: ['schema_version', '2']});
    db.exec('COMMIT');
  } catch (error) {
    try { insert.finalize(); } catch { /* already closed */ }
    try { db.exec('ROLLBACK'); } catch { /* ignore */ }
    throw error;
  }
  return {
    indexBuildMs: ms(now() - insertStart), downloadMs: corpus.downloadMs,
    downloadedBytes: corpus.downloadedBytes, decompressionMs: corpus.decompressionMs,
    verifyMs: corpus.verifyMs
  };
}
function openExisting(filename) {
  try {
    return strategy === 'prebuilt-sahpool'
      ? new pool.OpfsSAHPoolDb(filename, 'w')
      : new sqlite3.oo1.OpfsDb(filename, 'w');
  } catch { return null; }
}
async function setupStorage() {
  if (strategy === 'prebuilt-sahpool') {
    if (!sqlite3.installOpfsSAHPoolVfs) throw Error('SAH Pool niedostępny w tej przeglądarce');
    pool = await sqlite3.installOpfsSAHPoolVfs();
    mode = 'OPFS SAH Pool (trwały)';
  } else {
    if (!sqlite3.oo1.OpfsDb && strategy === 'prebuilt-opfs')
      throw Error('Standardowy OPFS niedostępny w tej przeglądarce');
    mode = sqlite3.oo1.OpfsDb ? 'OPFS (trwały)' : 'Tylko RAM (nietrwały)';
  }
}
async function init(selected) {
  if (!(selected in DATABASES)) throw Error('Nieznany wariant testowy');
  if (db) { db.close(); db = null; }
  strategy = selected;
  const started = now();
  const wasmStart = now();
  sqlite3 ??= await sqlite3InitModule();
  const wasmBootMs = ms(now() - wasmStart);
  await downloadManifest();
  if (strategy !== 'local-build' && (!manifest.sqlite_sha256 ||
      !manifest.sqlite_gzip_sha256 || !manifest.sqlite_bytes))
    throw Error('Brak gotowej bazy w manifeście — sprawdź Dockerfile');
  await setupStorage();
  const filename = DATABASES[strategy];
  const storageStart = now();
  let reused = false, imported = false;
  let indexBuildMs = 0, importMs = 0, downloadMs = 0, downloadedBytes = 0;
  let decompressionMs = 0, verifyMs = 0, storageErrors = [];
  const dbOpenStart = now();
  if (mode !== 'Tylko RAM (nietrwały)') {
    db = openExisting(filename);
    if (db && validateDatabase(strategy !== 'local-build')) reused = true;
    else if (db) { db.close(); db = null; }
  }
  const reusedOpenMs = reused ? ms(now() - dbOpenStart) : 0;
  if (!reused) {
    if (strategy === 'local-build') {
      if (!db) {
        if (mode.startsWith('OPFS')) {
          try { db = new sqlite3.oo1.OpfsDb(filename, 'c'); }
          catch (error) { storageErrors.push(String(error)); }
        }
        if (!db) {
          db = new sqlite3.oo1.DB();
          mode = 'Tylko RAM (nietrwały)';
        }
      }
      db.exec('CREATE TABLE IF NOT EXISTS meta(key TEXT PRIMARY KEY,value TEXT)');
      const result = await buildIndex();
      indexBuildMs = result.indexBuildMs;
      ({ downloadMs, downloadedBytes, decompressionMs, verifyMs } = result);
    } else {
      // importDb must only run while the destination database is *closed*.
      const snapshot = await readPreparedSnapshot();
      ({ downloadMs, downloadedBytes, decompressionMs, verifyMs } = snapshot);
      const startImport = now();
      const written = strategy === 'prebuilt-opfs'
        ? await sqlite3.oo1.OpfsDb.importDb(filename, snapshot.bytes)
        : await pool.importDb(filename, snapshot.bytes);
      importMs = ms(now() - startImport);
      if (written !== manifest.sqlite_bytes)
        throw Error('Importer zapisał niepełną bazę: ' + written);
      imported = true;
      db = openExisting(filename);
      if (!db || !validateDatabase(true))
        throw Error('Baza po imporcie nie przechodzi kontroli integralności/metadanych');
    }
  }
  total = Number(db.selectValue('SELECT COUNT(*) FROM docs_fts'));
  if (total !== manifest.records) throw Error('Różna liczba dokumentów po otwarciu SQLite');
  const databaseBytes = Number(db.selectValue('PRAGMA page_count')) *
    Number(db.selectValue('PRAGMA page_size'));
  const storageEstimate = typeof navigator.storage?.estimate === 'function'
    ? await navigator.storage.estimate().catch(() => null) : null;
  meta = {
    engine: 'SQLite FTS5 / WASM', sqlite_version: sqlite3.version.libVersion,
    strategy, mode, persisted: mode !== 'Tylko RAM (nietrwały)',
    storage_errors: storageErrors, documents: total, sources: manifest.sources,
    books_by_source: manifest.books_by_source ?? {},
    tnp_missing_vs_ubg: manifest.tnp_missing_vs_ubg ?? [],
    corpus_gzip_bytes: manifest.compressed_bytes, corpus_hash: manifest.gzip_sha256,
    sqlite_snapshot_sha256: manifest.sqlite_sha256, sqlite_snapshot_bytes: manifest.sqlite_bytes,
    database_bytes: databaseBytes,
    wasm_boot_ms: wasmBootMs,
    download_ms: downloadMs, downloaded_bytes: downloadedBytes,
    decompress_ms: decompressionMs, verify_ms: verifyMs, import_ms: importMs,
    db_open_ms: reusedOpenMs, storage_and_index_ms: ms(now() - storageStart),
    index_built: indexBuildMs > 0, index_build_ms: indexBuildMs,
    index_imported: imported, index_reused: reused,
    total_ready_ms: ms(now() - started),
    storage_usage_bytes: storageEstimate?.usage ?? null, // Entire origin, NOT database/RAM.
    storage_quota_bytes: storageEstimate?.quota ?? null,
    memory_bytes: null, memory_note: 'Brak porównywalnego pomiaru RAM w przeglądarkach',
    cross_origin_isolated: globalThis.crossOriginIsolated,
    cache_note: '20 identycznych powtórzeń każdego zapytania; wyniki obejmują cache'
  };
  return meta;
}
async function reset() {
  if (db) { db.close(); db = null; }
  const filename = DATABASES[strategy];
  if (strategy === 'prebuilt-sahpool') {
    if (!pool) throw Error('SAH Pool nie jest zainicjalizowany');
    pool.unlink(filename);
  } else if (mode.startsWith('OPFS')) {
    const root = await navigator.storage.getDirectory();
    await root.removeEntry(filename.slice(1)).catch(error => {
      if (error.name !== 'NotFoundError') throw error;
    });
  }
  // A new Worker is required to measure WASM boot again and reproduce cold start.
  return {strategy};
}
function benchmark(rounds = 20) {
  const results = [];
  for (const [name, query] of cases) {
    const times = [], counts = [];
    let hits = 0;
    for (let i = 0; i < rounds; i++) {
      times.push(top20(query).ms);
      const result = count(query);
      counts.push(result.ms);
      hits = result.hits;
    }
    results.push({
      name, query, hits, top20_p50_ms: percent(times, 0.5),
      top20_p95_ms: percent(times, 0.95),
      count_p50_ms: percent(counts, 0.5), count_p95_ms: percent(counts, 0.95)
    });
  }
  return {
    version: 2, created_at: new Date().toISOString(),
    engine: 'SQLite FTS5 / WASM',
    device: {user_agent: navigator.userAgent, hardware_concurrency: navigator.hardwareConcurrency ?? null},
    meta: {...meta}, rounds, results,
    summary: {
      typical_top20_p50_ms: percent(results.map(x => x.top20_p50_ms), 0.5),
      slowest_top20_p95_ms: Math.max(...results.map(x => x.top20_p95_ms))
    }
  };
}
self.onmessage = async ({ data: message }) => {
  try {
    if (message.cmd === 'init') postMessage({type: 'ready', meta: await init(message.strategy)});
    else if (message.cmd === 'reset') postMessage({type: 'reset', ...await reset()});
    else if (message.cmd === 'search') {
      const limited = top20(message.query), full = count(message.query);
      postMessage({type: 'search', ...limited, count: full.hits, countMs: full.ms});
    } else if (message.cmd === 'benchmark') {
      postMessage({type: 'benchmark', report: benchmark()});
    }
  } catch (error) {
    postMessage({type: 'error', message: String(error), stack: error?.stack ?? ''});
  }
};
