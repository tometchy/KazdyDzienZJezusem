import {test} from 'node:test';
import assert from 'node:assert/strict';
import {nativeMetricsText} from '../native-report.js';
const fmt = value => value == null ? '—' : value + ' ms';
const coldRun = {phase:'cold', report:{meta:{total_ready_ms:706.612}}};
const warmRun = (value, search) => ({
  phase:'warm',
  report:{
    meta:{total_ready_ms:value},
    summary:{typical_top20_p50_ms:search}
  }
});

test('new Tauri native benchmark has no report and no crash', () => {
  assert.equal(nativeMetricsText(null, fmt), '');
  assert.match(nativeMetricsText({runs:[]}, fmt), /0\/4/);
});

test('first cold run renders before summary exists — Pixel regression', () => {
  const report={version:4,status:'running',runs:[coldRun]};
  assert.match(nativeMetricsText(report,fmt), /Wykonano 1\/4/);
  assert.match(nativeMetricsText(report,fmt), /706\.612 ms/);
});

test('each of three warm runs can render before the aggregate is built', () => {
  const runs=[coldRun,warmRun(120,0.08),warmRun(130,0.07),warmRun(110,0.09)];
  for(let n=2;n<=4;n++){
    const output=nativeMetricsText({status:'running',runs:runs.slice(0,n)},fmt);
    assert.match(output,new RegExp(n+'/4'));
    assert.match(output,/706\.612 ms/);
  }
});

test('complete report renders aggregated cold and warm metrics', () => {
  const report={
    status:'completed',runs:[coldRun,warmRun(120,0.08),warmRun(130,0.07),warmRun(110,0.09)],
    summary:{cold_ready_ms:706.612,warm_ready_p50_ms:120,warm_search_p50_ms:0.08}
  };
  const output=nativeMetricsText(report,fmt);
  assert.match(output,/Zimny start: 706\.612 ms/);
  assert.match(output,/Mediana 3 ciepłych: 120 ms/);
  assert.match(output,/SQL p50 ciepły: 0\.08 ms/);
});

test('partial failed reports can be restored without throwing', () => {
  const report={status:'failed',error:'test error',runs:[coldRun]};
  assert.doesNotThrow(() => nativeMetricsText(report,fmt));
  assert.match(nativeMetricsText(report,fmt), /Wykonano 1\/4/);
});
