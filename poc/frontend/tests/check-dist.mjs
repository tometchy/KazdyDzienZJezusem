import assert from 'node:assert/strict';
import { readFileSync, readdirSync, statSync } from 'node:fs';
import { join } from 'node:path';

const output = new URL('../dist/', import.meta.url).pathname;
const manifestPath = join(output, 'corpus-manifest.json');
const corpusPath = join(output, 'corpus.jsonl.gz');
const manifest = JSON.parse(readFileSync(manifestPath, 'utf8'));
assert.equal(manifest.synthetic, false, 'POC must use real input/ data');
assert.ok(manifest.records > 0, 'POC must contain records');
assert.ok(statSync(corpusPath).size > 100, 'POC gzip corpus must be shipped');

const assetDir = join(output, 'assets');
const javascript = readdirSync(assetDir)
  .filter(file => file.endsWith('.js'))
  .map(file => readFileSync(join(assetDir, file), 'utf8'))
  .join('\n');
assert.ok(javascript.includes('corpus-manifest.json'), 'Manifest URL must be included in built worker');
assert.ok(javascript.includes('corpus.jsonl.gz'), 'Corpus URL must be included in built worker');
assert.ok(javascript.includes('/POC/'), 'Compiled bundle must use the /POC/ base');
console.log('POC build verified:', manifest.records, 'real records, gzip + Vite worker assets');
