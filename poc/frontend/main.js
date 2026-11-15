import {FULL_SUITE_KEY, STRATEGIES, WARM_REPETITIONS, runFullSuite, workerCommand} from './full-benchmark.js';
const $ = id => document.getElementById(id);
const OLD_STORAGE_KEY = 'kdj.search-poc.reports.v1';
const STORAGE_KEY = 'kdj.search-poc.reports.v2';
let worker, currentReport = null, busy = true;
let suiteRunning = false, suiteReport = null, suiteAbortController = null;
const fmt = n => Number.isFinite(Number(n))
  ? Number(n).toLocaleString('pl-PL', {maximumFractionDigits: 2}) + ' ms' : '—';
const mib = n => Number.isFinite(Number(n))
  ? (Number(n) / 1048576).toLocaleString('pl-PL', {maximumFractionDigits: 2}) + ' MiB' : '—';
function setStatus(message) { $('status').textContent = message; }
function enable(ready) {
  busy = !ready;
  const interactive = ready && !suiteRunning;
  for (const id of ['search', 'bench', 'reset', 'reopen']) $(id).disabled = !interactive;
  $('strategy').disabled = suiteRunning;
  $('run-all').disabled = !interactive;
  $('cancel-all').disabled = !suiteRunning;
  $('suite-copy').disabled = !suiteReport || suiteRunning;
  $('suite-download').disabled = !suiteReport || suiteRunning;
  $('copy').disabled = !currentReport || suiteRunning;
  $('download').disabled = !currentReport || suiteRunning;
}
function safeHistory(key, version) {
  try {
    const list = JSON.parse(localStorage.getItem(key) ?? '[]');
    return Array.isArray(list) ? list.filter(row => row?.version === version) : [];
  } catch { return []; }
}
function history() {
  return [...safeHistory(STORAGE_KEY, 2), ...safeHistory(OLD_STORAGE_KEY, 1)]
    .sort((a, b) => b.created_at.localeCompare(a.created_at));
}
function renderComparison() {
  const latest = new Map();
  for (const report of safeHistory(STORAGE_KEY, 2)) {
    const variant = report.meta?.strategy;
    if (!variant) continue;
    const phase = report.meta?.index_reused ? 'warm' : 'cold';
    const key = variant + ':' + phase;
    if (!latest.has(key)) latest.set(key, report);
  }
  const root = $('comparison-rows');
  root.replaceChildren();
  for (const [variant, name] of [
    ['local-build', 'A · lokalne FTS5'],
    ['prebuilt-opfs', 'B · gotowy OPFS'],
    ['prebuilt-sahpool', 'C · gotowy SAH Pool']
  ]) {
    const cold = latest.get(variant + ':cold');
    const warm = latest.get(variant + ':warm');
    const tr = document.createElement('tr');
    const fields = [
      name,
      cold ? fmt(cold.meta.total_ready_ms) : '—',
      warm ? fmt(warm.meta.total_ready_ms) : '—',
      cold ? mib(cold.meta.downloaded_bytes) : '—',
      cold ? fmt(cold.meta.import_ms || cold.meta.index_build_ms) : '—',
      cold ? fmt(cold.summary.typical_top20_p50_ms) : '—',
      warm ? fmt(warm.summary.typical_top20_p50_ms) : '—'
    ];
    for (const value of fields) {
      const td = document.createElement('td');
      td.textContent = value;
      tr.append(td);
    }
    root.append(tr);
  }
}

function writeHistory(report) {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(
      [report, ...safeHistory(STORAGE_KEY, 2)].slice(0, 40)));
  } catch {
    $('bench-status').textContent += ' Uwaga: nie udało się zapisać raportu lokalnie.';
  }
}
function renderHistory() {
  $('history').replaceChildren();
  renderComparison();
  const items = history();
  if (!items.length) { $('history').textContent = 'Brak zapisanych raportów.'; return; }
  for (const report of items) {
    const button = document.createElement('button');
    button.className = 'secondary';
    button.textContent = new Date(report.created_at).toLocaleString('pl-PL') +
      ' · ' + (report.meta?.strategy ?? 'Starszy POC') +
      ' · ' + (report.meta?.documents ?? '?').toLocaleString('pl-PL') + ' rekordów' +
      ' · p50 ' + fmt(report.summary?.typical_top20_p50_ms);
    button.onclick = () => { currentReport = report; renderReport(report); };
    $('history').append(button);
  }
}
function showMeta(meta) {
  $('records').textContent = Number(meta.documents).toLocaleString('pl-PL');
  $('mode').textContent = meta.mode;
  $('ready').textContent = fmt(meta.total_ready_ms);
  $('built').textContent = meta.index_built ? fmt(meta.index_build_ms) :
    meta.index_imported ? 'Zaimportowany' : 'Ponownie użyty';
  $('download-ms').textContent = fmt(meta.download_ms);
  $('download-bytes').textContent = mib(meta.downloaded_bytes);
  $('decompress-ms').textContent = fmt(meta.decompress_ms);
  $('import-ms').textContent = fmt(meta.import_ms);
  $('open-ms').textContent = fmt(meta.db_open_ms);
  $('database-bytes').textContent = mib(meta.database_bytes);
  $('memory').textContent = 'Brak wiarygodnego API RAM';
  $('storage').textContent = meta.storage_usage_bytes == null
    ? 'Brak danych' : mib(meta.storage_usage_bytes) + ' (cała domena)';
  $('sources').textContent = 'Źródła: ' + Object.entries(meta.sources ?? {})
    .map(([name, count]) => name + ': ' + Number(count).toLocaleString('pl-PL')).join(' · ') +
    '; księgi TNP: ' + (meta.books_by_source?.TNP ?? '?') +
    '; brakujące w TNP wobec UBG: ' + (meta.tnp_missing_vs_ubg?.length ?? '?') +
    '. SHA korpusu: ' + String(meta.corpus_hash).slice(0, 16) + '…';
  setStatus(meta.persisted ? (meta.index_reused
    ? 'Gotowe — otwarto istniejący indeks bez pobierania korpusu.'
    : 'Gotowe — indeks zapisany lokalnie.') : 'Gotowe — wyłącznie RAM (bez trwałości).');
}
function renderSearch(data) {
  $('search-stats').textContent = data.count.toLocaleString('pl-PL') +
    ' trafień · pierwsze 20: ' + fmt(data.ms) + ' · COUNT: ' + fmt(data.countMs);
  $('matches').replaceChildren();
  for (const result of data.results.slice(0, 8)) {
    const item = document.createElement('div');
    item.className = 'result';
    const title = document.createElement('b');
    title.textContent = result.source + ' · ' + result.ref;
    const body = document.createElement('div');
    body.textContent = (result.pl || result.gr || result.en || result.lemma || '').slice(0, 300);
    item.append(title, body);
    $('matches').append(item);
  }
}
function renderReport(report) {
  $('summary-p50').textContent = fmt(report.summary.typical_top20_p50_ms);
  $('summary-p95').textContent = fmt(report.summary.slowest_top20_p95_ms);
  $('bench-rows').replaceChildren();
  for (const row of report.results) {
    const tr = document.createElement('tr');
    for (const cellValue of [row.name, row.hits, fmt(row.top20_p50_ms),
      fmt(row.top20_p95_ms), fmt(row.count_p50_ms), fmt(row.count_p95_ms)]) {
      const cell = document.createElement('td');
      cell.textContent = cellValue;
      tr.append(cell);
    }
    tr.title = row.query;
    $('bench-rows').append(tr);
  }
  $('bench-status').textContent = new Date(report.created_at).toLocaleString('pl-PL') +
    ' · ' + (report.meta?.strategy ?? 'Wariant oryginalny') +
    ' · ' + report.rounds + ' powtórzeń. Raport można skopiować jako JSON.';
  $('copy').disabled = $('download').disabled = false;
}
function renderSuite() {
  const report = suiteReport;
  $('suite-summary-rows').replaceChildren();
  if (!report) {
    $('suite-progress').textContent = 'Brak zbiorczego testu. Kliknij przycisk, aby wykonać wszystkie 12 przebiegów.';
    return;
  }
  for (const {id, label} of STRATEGIES) {
    const data = report.summary?.by_strategy?.[id] ?? {};
    const tr = document.createElement('tr');
    const values = [
      label, fmt(data.cold_total_ready_ms), fmt(data.warm_total_ready_p50_ms),
      fmt(data.cold_search_p50_ms), fmt(data.warm_search_p50_ms),
      String(data.warm_runs ?? 0) + '/' + WARM_REPETITIONS
    ];
    for (const value of values) {
      const td = document.createElement('td');
      td.textContent = value;
      tr.append(td);
    }
    $('suite-summary-rows').append(tr);
  }
  const state = report.status === 'completed' ? 'Ukończono' :
    report.status === 'cancelled' ? 'Przerwano' : 'Niepowodzenie';
  $('suite-progress').textContent = state + ' · ' +
    (report.summary?.runs_completed ?? 0) + '/' +
    (report.summary?.expected_runs ?? 12) + ' przebiegów. ' +
    (report.error || 'Jeden zbiorczy raport JSON gotowy do skopiowania.');
  $('suite-copy').disabled = $('suite-download').disabled = suiteRunning;
}
function restoreSuite() {
  try {
    const previous = JSON.parse(localStorage.getItem(FULL_SUITE_KEY) ?? 'null');
    if (previous?.version === 3 && previous.kind === 'one-click-full-benchmark')
      suiteReport = previous;
  } catch { /* Storage may not be available */ }
  renderSuite();
}
function startWorker() {
  if (suiteRunning) return;
  worker?.terminate();
  enable(false);
  setStatus('Uruchamianie SQLite WASM (' + $('strategy').selectedOptions[0].text + ')…');
  worker = new Worker(new URL('./sqlite-worker.js', import.meta.url), { type: 'module' });
  worker.onmessage = handleWorkerMessage;
  worker.postMessage({cmd: 'init', strategy: $('strategy').value});
}
function search() {
  if (busy || !$('query').value.trim()) return;
  worker.postMessage({cmd: 'search', query: $('query').value});
}
function handleWorkerMessage({data}) {
  if (data.type === 'waiting')
    setStatus('Oczekiwanie na dostęp do SQLite (inna karta może obecnie korzystać z indeksu)…');
  if (data.type === 'progress')
    setStatus('Indeksowanie: ' + data.count.toLocaleString('pl-PL') + ' rekordów…');
  if (data.type === 'error') {
    setStatus('Błąd (' + $('strategy').value + '): ' + data.message);
    $('bench-status').textContent = 'Możesz wybrać inny wariant. Komunikat błędu zachowaj do diagnostyki.';
    console.error(data.stack);
    enable(false);
  }
  if (data.type === 'ready') {
    showMeta(data.meta);
    enable(true);
    search();
  }
  if (data.type === 'search') {
    renderSearch(data);
    setStatus('Gotowe — indeks dostępny również w innych kartach.');
  }
  if (data.type === 'benchmark') {
    currentReport = data.report;
    renderReport(currentReport);
    writeHistory(currentReport);
    renderHistory();
    enable(true);
  }
  if (data.type === 'reset' || data.type === 'closed') startWorker();
}
$('run-all').onclick = async () => {
  if (busy || suiteRunning) return;
  suiteRunning = true;
  suiteAbortController = new AbortController();
  suiteReport = null;
  enable(false);
  $('suite-summary-rows').replaceChildren();
  $('suite-progress').textContent = 'Zamykanie aktualnej bazy przed serią testów…';
  const previousWorker = worker;
  worker = null;
  let wakeLock;
  try {
    // The long mobile run should not be interrupted by normal screen timeout.
    wakeLock = await navigator.wakeLock?.request('screen').catch(() => null);
    if (previousWorker) {
      previousWorker.onmessage = null; // Prevent the manual closed handler from spawning another Worker.
      try {
        await workerCommand(previousWorker, {cmd: 'close'}, 'closed',
          {signal: suiteAbortController.signal});
      } finally {
        previousWorker.terminate();
      }
    }
    suiteReport = await runFullSuite({
      makeWorker: () => new Worker(new URL('./sqlite-worker.js', import.meta.url), {type: 'module'}),
      onProgress: message => { $('suite-progress').textContent = message; },
      signal: suiteAbortController.signal
    });
    try {
      localStorage.setItem(FULL_SUITE_KEY, JSON.stringify(suiteReport));
    } catch {
      $('suite-progress').textContent += ' (Nie można zapisać lokalnie; skopiuj wynik.)';
    }
    renderSuite();
  } catch (error) {
    $('suite-progress').textContent = 'Nie udało się rozpocząć: ' + String(error);
  } finally {
    try { await wakeLock?.release(); } catch { /* Wake Lock may have expired */ }
    suiteRunning = false;
    suiteAbortController = null;
    startWorker(); // Restore manual search without changing the selected strategy.
    if (suiteReport) renderSuite();
  }
};
$('cancel-all').onclick = () => suiteAbortController?.abort();
$('suite-copy').onclick = async () => {
  if (!suiteReport) return;
  try {
    await navigator.clipboard.writeText(JSON.stringify(suiteReport, null, 2));
    $('suite-progress').textContent = 'Skopiowano jeden zbiorczy JSON (' +
      suiteReport.runs.length + ' przebiegów).';
  } catch {
    $('suite-progress').textContent = 'Schowek niedostępny — użyj „Pobierz zbiorczy JSON”.';
  }
};
$('suite-download').onclick = () => {
  if (!suiteReport) return;
  const url = URL.createObjectURL(new Blob([JSON.stringify(suiteReport, null, 2)],
    {type: 'application/json'}));
  const link = document.createElement('a');
  link.href = url;
  link.download = 'kdj-fts5-pelny-test-' +
    suiteReport.created_at.replaceAll(':', '-') + '.json';
  link.click();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
};
$('strategy').onchange = startWorker;
$('search').onclick = search;
$('query').addEventListener('keydown', event => { if (event.key === 'Enter') search(); });
$('bench').onclick = () => {
  if (busy) return;
  enable(false);
  $('bench-status').textContent = 'Trwa pomiar 10 zapytań × 20 powtórzeń…';
  worker.postMessage({cmd: 'benchmark'});
};
$('reopen').onclick = () => {
  if (busy) return;
  enable(false);
  setStatus('Zamykanie bazy przed pomiarem ponownego otwarcia…');
  worker.postMessage({cmd: 'close'});
};
$('reset').onclick = () => {
  if (busy || !confirm('Usunąć WYŁĄCZNIE indeks wybranego wariantu? ' +
    'Raporty i pozostałe warianty pozostaną nietknięte.')) return;
  enable(false);
  setStatus('Usuwanie wybranego indeksu (bez usuwania raportów)…');
  worker.postMessage({cmd: 'reset'});
};
$('copy').onclick = async () => {
  if (!currentReport) return;
  try {
    await navigator.clipboard.writeText(JSON.stringify(currentReport, null, 2));
    $('bench-status').textContent = 'Skopiowano raport JSON.';
  } catch {
    $('bench-status').textContent = 'Schowek niedostępny — użyj „Pobierz JSON”.';
  }
};
$('download').onclick = () => {
  if (!currentReport) return;
  const url = URL.createObjectURL(new Blob([JSON.stringify(currentReport, null, 2)],
    {type: 'application/json'}));
  const link = document.createElement('a');
  link.href = url;
  link.download = 'kdj-fts5-' + currentReport.created_at.replaceAll(':', '-') + '.json';
  link.click();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
};
renderHistory();
restoreSuite();
startWorker();
