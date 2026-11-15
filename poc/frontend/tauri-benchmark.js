/** Native Tauri/pMOS benchmark on the SAME frontend as the website POC.
 * WebKitGTK's OPFS support is checked, never inferred from Chromium results. */
import {invoke} from '@tauri-apps/api/core';

const $ = id => document.getElementById(id);
const STORAGE_KEY = 'kdj.search-poc.tauri-native.v1';
let report = null;
let running = false;
const fmt = n => Number.isFinite(Number(n))
  ? Number(n).toLocaleString('pl-PL', {maximumFractionDigits: 3}) + ' ms' : '—';
const median = items => {
  if (!items.length) return null;
  const sorted = [...items].sort((a,b) => a-b);
  return sorted[Math.floor(sorted.length / 2)];
};

async function diagnostics() {
  const details = {
    shell: 'Tauri 2 / WebKitGTK (Linux)',
    user_agent: navigator.userAgent,
    cross_origin_isolated: globalThis.crossOriginIsolated,
    web_locks: typeof navigator.locks?.request === 'function',
    opfs_get_directory: typeof navigator.storage?.getDirectory === 'function',
    sync_access_handle: typeof FileSystemFileHandle !== 'undefined' &&
      typeof FileSystemFileHandle.prototype.createSyncAccessHandle === 'function'
  };
  if (details.opfs_get_directory) {
    try {
      const root = await navigator.storage.getDirectory();
      details.opfs_usable = Boolean(root?.getFileHandle);
    } catch (error) {
      details.opfs_usable = false;
      details.opfs_error = String(error);
    }
  } else {
    details.opfs_usable = false;
  }
  details.sahpool_prerequisites = details.opfs_usable &&
    details.sync_access_handle && details.web_locks;
  details.standard_opfs_prerequisites =
    details.sahpool_prerequisites && details.cross_origin_isolated;
  $('tauri-diagnostics').textContent = JSON.stringify(details, null, 2);
  $('tauri-warning').textContent = details.sahpool_prerequisites
    ? 'API potrzebne do testu SAH Pool wyglądają na dostępne; pełny test SQLite WASM wymaga jeszcze osobnego potwierdzenia.'
    : 'WebKitGTK nie udostępnia wszystkich API wymaganych przez SAH Pool. Natywny SQLite FTS5 działa bez OPFS; ten test NIE mierzy SAH Pool.';
  return details;
}

function render() {
  $('tauri-report-rows').replaceChildren();
  if (!report) return;
  for (const item of report.runs) {
    const tr = document.createElement('tr');
    for (const value of [
      item.phase === 'cold' ? 'Zimny' : 'Ciepły ' + item.repetition + '/3',
      fmt(item.report.meta.total_ready_ms),
      fmt(item.report.summary.typical_top20_p50_ms),
      fmt(item.report.summary.slowest_top20_p95_ms),
      item.report.meta.sqlite_version
    ]) {
      const td = document.createElement('td');
      td.textContent = value;
      tr.append(td);
    }
    $('tauri-report-rows').append(tr);
  }
  const summary = report.summary;
  $('tauri-metrics').textContent = 'Zimny start: ' + fmt(summary.cold_ready_ms) +
    ' · Mediana 3 ciepłych: ' + fmt(summary.warm_ready_p50_ms) +
    ' · SQL p50 ciepły: ' + fmt(summary.warm_search_p50_ms);
  $('tauri-copy').disabled = $('tauri-save').disabled = running;
}

async function run() {
  if (running) return;
  running = true;
  report = {
    version: 4, kind: 'tauri-postmarketos-native-sqlite',
    created_at: new Date().toISOString(), status: 'running',
    device: {
      user_agent: navigator.userAgent,
      hardware_concurrency: navigator.hardwareConcurrency ?? null
    },
    webview: JSON.parse($('tauri-diagnostics').textContent),
    configuration: {
      cold_runs: 1, warm_runs: 3, rounds: 20,
      data_source: 'SQLite FTS5 snapshot bundled inside native ARM64/musl executable',
      caveat: 'NATIVE SQLite test; do not compare to OPFS/SAH query-only timings without accounting for WebView and IPC'
    },
    runs: []
  };
  $('tauri-start').disabled = true;
  $('tauri-copy').disabled = $('tauri-save').disabled = true;
  try {
    for (let i = 0; i < 4; i++) {
      const phase = i === 0 ? 'cold' : 'warm';
      $('tauri-progress').textContent = 'Pomiar ' + (i+1) + '/4: ' +
        (phase === 'cold' ? 'usunięcie i import natywnej bazy' : 'ponowne otwarcie zapisanej bazy') + '…';
      const measured = await invoke('native_benchmark', {reset: i === 0});
      if (phase === 'cold' && measured.meta.index_reused)
        throw new Error('Natywny zimny start użył istniejącego indeksu');
      if (phase === 'warm' && !measured.meta.index_reused)
        throw new Error('Natywny ciepły start odtworzył indeks');
      report.runs.push({strategy:'native-sqlite',phase,
        repetition: i === 0 ? 1 : i,report:measured});
      render();
    }
    const cold = report.runs[0].report;
    const warm = report.runs.slice(1).map(x=>x.report);
    report.summary = {
      runs_completed: 4, expected_runs: 4,
      cold_ready_ms: cold.meta.total_ready_ms,
      warm_ready_p50_ms: median(warm.map(x=>x.meta.total_ready_ms)),
      cold_search_p50_ms: cold.summary.typical_top20_p50_ms,
      warm_search_p50_ms: median(warm.map(x=>x.summary.typical_top20_p50_ms)),
      hit_counts_consistent: report.runs.every(x =>
        JSON.stringify(x.report.results.map(y=>[y.name,y.hits])) ===
        JSON.stringify(cold.results.map(y=>[y.name,y.hits])))
    };
    if(!report.summary.hit_counts_consistent) throw new Error('Rozbieżne liczby trafień');
    report.status='completed';
    $('tauri-progress').textContent = 'Gotowe: 4/4 przebiegi · jeden raport JSON.';
  } catch (error) {
    report.status='failed';
    report.error=String(error);
    $('tauri-progress').textContent = 'Błąd pomiaru: ' + report.error +
      ' · możesz zapisać częściowy raport.';
  } finally {
    report.finished_at=new Date().toISOString();
    try{localStorage.setItem(STORAGE_KEY,JSON.stringify(report));}catch{ /* not fatal */}
    running=false;
    $('tauri-start').disabled=false;
    $('tauri-copy').disabled=$('tauri-save').disabled=false;
    render();
  }
}

export async function initNativeBenchmark() {
  $('tauri-panel').hidden = false;
  for (const section of document.querySelectorAll('main.shell > section.panel')) {
    if (section.id !== 'tauri-panel') section.hidden = true;
  }
  document.querySelector('h1').textContent = 'Tauri 2 / postmarketOS — benchmark SQLite';
  document.querySelector('main > p > a').hidden = true;
  $('tauri-start').onclick = run;
  $('tauri-legacy').disabled = false;
  $('tauri-legacy').onclick = () =>
    window.dispatchEvent(new CustomEvent('kdzj:try-webkit-opfs'));
  $('tauri-copy').onclick = async () => {
    if (!report) return;
    try {
      await navigator.clipboard.writeText(JSON.stringify(report, null, 2));
      $('tauri-progress').textContent = 'Zbiorczy JSON skopiowany.';
    } catch (error) {
      $('tauri-progress').textContent = 'Schowek niedostępny (' + error + '). Użyj zapisu do Downloads.';
    }
  };
  $('tauri-save').onclick = async () => {
    if (!report) return;
    try {
      const path = await invoke('save_report_json', {contents:JSON.stringify(report, null, 2)});
      $('tauri-progress').textContent = 'Raport zapisany: ' + path;
    } catch (error) {
      $('tauri-progress').textContent = 'Nie można zapisać raportu: ' + error;
    }
  };
  try {
    const previous=JSON.parse(localStorage.getItem(STORAGE_KEY)??'null');
    if (previous?.version===4){report=previous;render();}
  } catch { /* previous unavailable */ }
  await diagnostics();
  $('tauri-start').disabled=false;
}
