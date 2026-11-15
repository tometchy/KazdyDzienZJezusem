import {test} from 'node:test';
import assert from 'node:assert/strict';
import {unlinkSahIfPresent} from '../sahpool-utils.js';

test('cold purge of an absent SAH virtual file does not call unlink', () => {
  let attempts = 0;
  const pool = {
    getFileNames: () => ['/another.sqlite3'],
    unlink: () => { attempts++; throw Error('Expected no unlink call'); }
  };
  assert.equal(unlinkSahIfPresent(pool, '/kdj-prebuilt-sahpool-v1.sqlite3'), false);
  assert.equal(attempts, 0);
});

test('purge of an existing SAH file calls unlink exactly once', () => {
  let attempts = 0;
  const name = '/kdj-prebuilt-sahpool-v1.sqlite3';
  const pool = {
    getFileNames: () => [name],
    unlink: value => { assert.equal(value, name); attempts++; return true; }
  };
  assert.equal(unlinkSahIfPresent(pool, name), true);
  assert.equal(attempts, 1);
});

test('unexpected SAH errors still propagate instead of being hidden', () => {
  const name = '/kdj-prebuilt-sahpool-v1.sqlite3';
  const pool = {
    getFileNames: () => [name],
    unlink: () => { throw new Error('real I/O failure'); }
  };
  assert.throws(() => unlinkSahIfPresent(pool, name), /real I\/O failure/);
});
