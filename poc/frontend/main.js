const $ = id => document.getElementById(id);
const worker = new Worker(new URL('./sqlite-worker.js', import.meta.url), { type: 'module' });
const STORAGE_KEY = 'kdj.search-poc.reports.v1';
let currentReport = null;
let busy = true;

function fmt(ms) {
  return Number.isFinite(Number(ms)) ? Number(ms).toLocaleString('pl-PL', {maximumFractionDigits: 2}) + ' ms' : '—';
}
function enable(canRun) {
  busy = !canRun;
  for (const id of ['search', 'bench', 'rebuild']) $(id).disabled = !canRun;
  $('copy').disabled = !currentReport;
  $('download').disabled = !currentReport;
}
function setStatus(message) { $('status').textContent = message; }
function readHistory() {
  try {
    const saved = JSON.parse(localStorage.getItem(STORAGE_KEY) ?? '[]');
    return Array.isArray(saved) ? saved.filter(x => x?.version === 1) : [];
  } catch { return []; }
}
function writeHistory(reports) {
  try { localStorage.setItem(STORAGE_KEY, JSON.stringify(reports.slice(0, 8))); }
  catch { $('bench-status').textContent += ' Uwaga: przeglądarka nie pozwoliła zapisać raportu lokalnie.'; }
}
function renderHistory() {
  const root = $('history');
  root.replaceChildren();
  const reports = readHistory();
  if (!reports.length) { root.textContent = 'Brak zapisanych raportów na tym urządzeniu.'; return; }
  for (const report of reports) {
    const btn = document.createElement('button');
    btn.className = 'secondary';
    const time = new Date(report.created_at).toLocaleString('pl-PL');
    btn.textContent = time + ' · ' + (report.meta?.documents?.toLocaleString('pl-PL') ?? '?')
      + ' dokumentów · ' + fmt(report.summary?.typical_top20_p50_ms) + ' — wyświetl i kopiuj';
    btn.onclick = () => { currentReport = report; renderReport(report); enable(!busy); };
    root.append(btn);
  }
}
function showMeta(meta) {
  $('records').textContent = Number(meta.documents).toLocaleString('pl-PL');
  $('mode').textContent = meta.mode;
  $('ready').textContent = fmt(meta.total_ready_ms);
  $('built').textContent = meta.index_built ? fmt(meta.index_build_ms) : 'Ponownie użyty';
  const sources = Object.entries(meta.sources ?? {}).map(([source, count]) =>
    source + ': ' + Number(count).toLocaleString('pl-PL')).join(' • ');
  $('sources').textContent = 'Źródła: ' + sources + '; rozmiar pobranego korpusu: '
    + (meta.corpus_gzip_bytes / 1048576).toFixed(1) + ' MiB';
  setStatus(meta.persisted ? 'Gotowe — indeks zapisany na tym urządzeniu.' :
    'Gotowe — UWAGA: indeks działa tylko w RAM. Po zamknięciu strony trzeba go zbudować ponownie.');
  if (!meta.persisted) $('mode').className = 'slow';
}
function renderSearch(data) {
  $('search-stats').textContent = data.count.toLocaleString('pl-PL') + ' trafień • pierwsze 20: '
    + fmt(data.ms) + ' • policzenie wszystkich: ' + fmt(data.countMs);
  $('matches').replaceChildren();
  for (const result of data.results.slice(0, 8)) {
    const item = document.createElement('div');
    item.className = 'result';
    const label = document.createElement('b');
    label.textContent = result.source + ' · ' + result.ref;
    const paragraph = document.createElement('div');
    paragraph.textContent = (result.pl || result.gr || result.en || result.lemma || '').slice(0, 300);
    item.append(label, paragraph);
    $('matches').append(item);
  }
}
function renderReport(report) {
  $('summary-p50').textContent = fmt(report.summary.typical_top20_p50_ms);
  $('summary-p95').textContent = fmt(report.summary.slowest_top20_p95_ms);
  $('bench-rows').replaceChildren();
  for (const row of report.results) {
    const tr = document.createElement('tr');
    for (const value of [row.name, row.hits, fmt(row.top20_p50_ms), fmt(row.top20_p95_ms),
      fmt(row.count_p50_ms), fmt(row.count_p95_ms)]) {
      const cell = document.createElement('td');
      cell.textContent = value;
      tr.append(cell);
    }
    tr.title = row.query;
    $('bench-rows').append(tr);
  }
  $('bench-status').textContent = 'Raport z ' + new Date(report.created_at).toLocaleString('pl-PL')
    + '. ' + report.rounds + ' powtórzeń każdego z ' + report.results.length
    + ' zapytań. Kliknij „Kopiuj raport do schowka” i wklej go do rozmowy.';
  $('copy').disabled = false;
  $('download').disabled = false;
}
function search() {
  if (busy || !$('query').value.trim()) return;
  worker.postMessage({cmd: 'search', query: $('query').value});
}
async function copyReport() {
  if (!currentReport) return;
  try {
    await navigator.clipboard.writeText(JSON.stringify(currentReport, null, 2));
    $('bench-status').textContent = 'Raport skopiowany. Wklej go do ChatGPT.';
  } catch {
    $('bench-status').textContent = 'Nie udało się użyć schowka. Użyj „Pobierz JSON”.';
  }
}
function downloadReport() {
  if (!currentReport) return;
  const file = new Blob([JSON.stringify(currentReport, null, 2)], {type: 'application/json'});
  const url = URL.createObjectURL(file);
  const link = document.createElement('a');
  link.href = url;
  link.download = 'kdj-benchmark-' + currentReport.created_at.replaceAll(':', '-') + '.json';
  link.click();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}
worker.onmessage = ({data}) => {
  if (data.type === 'progress') setStatus('Budowanie indeksu: ' + data.count.toLocaleString('pl-PL') + ' rekordów…');
  if (data.type === 'error') {
    setStatus('Błąd: ' + data.message);
    $('bench-status').textContent = 'W razie błędu spróbuj ponownie otworzyć stronę, a następnie prześlij komunikat.';
    console.error(data.stack);
    enable(false);
  }
  if (data.type === 'ready') {
    showMeta(data.meta);
    enable(true);
    search();
  }
  if (data.type === 'search') renderSearch(data);
  if (data.type === 'benchmark') {
    currentReport = data.report;
    renderReport(currentReport);
    writeHistory([currentReport, ...readHistory()].slice(0, 8));
    renderHistory();
    enable(true);
  }
};
$('search').onclick = search;
$('query').addEventListener('keydown', e => { if (e.key === 'Enter') search(); });
$('bench').onclick = () => {
  if (busy) return;
  enable(false);
  $('bench-status').textContent = 'Trwa pomiar 10 zapytań na tym urządzeniu…';
  worker.postMessage({cmd: 'benchmark'});
};
$('rebuild').onclick = () => {
  if (busy || !confirm('Usunąć lokalny indeks i zbudować go ponownie? Zapisane raporty zostaną zachowane.')) return;
  enable(false);
  setStatus('Ponowne budowanie indeksu…');
  worker.postMessage({cmd: 'rebuild'});
};
$('copy').onclick = copyReport;
$('download').onclick = downloadReport;
renderHistory();
worker.postMessage({cmd: 'init'});
