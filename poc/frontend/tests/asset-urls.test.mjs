import { test } from 'node:test';
import assert from 'node:assert/strict';
import { corpusAssetUrl } from '../asset-urls.js';

// Vite's bundled worker runs from /POC/assets/, whereas publicDir files
// are copied into /POC/. This regression caused "Brak manifestu korpusu".
const bundledWorker = 'https://kazdydzienzjezusem.pl/POC/assets/sqlite-worker-xyz.js';

test('compiled worker loads the manifest from /POC/, not from /POC/assets/', () => {
  assert.equal(
    corpusAssetUrl('corpus-manifest.json', '/POC/', bundledWorker),
    'https://kazdydzienzjezusem.pl/POC/corpus-manifest.json'
  );
});

test('compiled worker loads the gzip corpus from /POC/', () => {
  assert.equal(
    corpusAssetUrl('corpus.jsonl.gz', '/POC/', bundledWorker),
    'https://kazdydzienzjezusem.pl/POC/corpus.jsonl.gz'
  );
});

test('data URLs also work under a different origin', () => {
  assert.equal(
    corpusAssetUrl('corpus-manifest.json', '/POC/', 'http://localhost:8766/POC/assets/sqlite-worker.js'),
    'http://localhost:8766/POC/corpus-manifest.json'
  );
});

test('compiled worker fetches prebuilt database from /POC/, never /POC/assets/', () => {
  assert.equal(
    corpusAssetUrl('corpus-fts5.sqlite3.gz', '/POC/', bundledWorker),
    'https://kazdydzienzjezusem.pl/POC/corpus-fts5.sqlite3.gz'
  );
});
