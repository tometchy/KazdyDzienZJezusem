import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { gunzipSync } from 'node:zlib';
import { readFileSync, readdirSync, statSync } from 'node:fs';
import { join } from 'node:path';

const output = new URL('../dist/', import.meta.url).pathname;
const manifestPath = join(output, 'corpus-manifest.json');
const corpusPath = join(output, 'corpus.jsonl.gz');
const manifest = JSON.parse(readFileSync(manifestPath, 'utf8'));
assert.equal(manifest.synthetic, false, 'POC must use real input/ data');
assert.ok(manifest.records > 0, 'POC must contain records');
assert.ok(statSync(corpusPath).size > 100, 'POC gzip corpus must be shipped');

const sqliteGzip = readFileSync(join(output, 'corpus-fts5.sqlite3.gz'));
assert.equal(sqliteGzip.length, manifest.sqlite_gzip_bytes, 'Compressed SQLite length must match manifest');
assert.equal(createHash('sha256').update(sqliteGzip).digest('hex'),
  manifest.sqlite_gzip_sha256, 'Compressed SQLite hash must match manifest');
const sqlite = gunzipSync(sqliteGzip);
assert.equal(sqlite.length, manifest.sqlite_bytes, 'Raw SQLite length must match manifest');
assert.equal(createHash('sha256').update(sqlite).digest('hex'),
  manifest.sqlite_sha256, 'Raw SQLite hash must match manifest');
assert.equal(sqlite.subarray(0, 16).toString('binary'), 'SQLite format 3\\0'.replace('\\0', '\0'),
  'Bundled SQLite must have the correct file header');
assert.ok(!readdirSync(output).includes('corpus-fts5.sqlite3'),
  'Do not accidentally ship uncompressed and compressed copies');

const assetDir = join(output, 'assets');
const javascript = readdirSync(assetDir)
  .filter(file => file.endsWith('.js'))
  .map(file => readFileSync(join(assetDir, file), 'utf8'))
  .join('\n');
assert.ok(javascript.includes('corpus-manifest.json'), 'Manifest URL must be included in built worker');
assert.ok(javascript.includes('corpus.jsonl.gz'), 'Corpus URL must be included in built worker');
assert.ok(javascript.includes('corpus-fts5.sqlite3.gz'), 'Prebuilt SQLite URL must be bundled');
assert.ok(javascript.includes('/POC/'), 'Compiled bundle must use the /POC/ base');
console.log('POC build verified:', manifest.records, 'real records, gzip + Vite worker assets');
