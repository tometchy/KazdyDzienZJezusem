/** One-click A/B/C benchmark. Each measured run gets its own fresh Web Worker. */
export const FULL_SUITE_KEY = 'kdj.search-poc.full-suite.v1';
export const WARM_REPETITIONS = 3;
export const STRATEGIES = Object.freeze([
  {id: 'local-build', label: 'A · lokalne FTS5'},
  {id: 'prebuilt-opfs', label: 'B · gotowy OPFS'},
  {id: 'prebuilt-sahpool', label: 'C · SAH Pool'}
]);

const median = values => {
  if (!values.length) return null;
  const ordered = [...values].sort((a, b) => a - b);
  const mid = Math.floor(ordered.length / 2);
  return Number((ordered.length % 2
    ? ordered[mid] : (ordered[mid - 1] + ordered[mid]) / 2).toFixed(3));
};

/** Request/response bridge; errors, cancellation and timeouts never hang the suite. */
export function workerCommand(worker, command, expectedType, {signal, onProgress} = {}) {
  return new Promise((resolve, reject) => {
    if (signal?.aborted) { reject(new Error('Przerwano przez użytkownika')); return; }
    let timeout;
    const cleanup = () => {
      clearTimeout(timeout);
      worker.removeEventListener('message', onMessage);
      worker.removeEventListener('error', onError);
      worker.removeEventListener('messageerror', onError);
      signal?.removeEventListener('abort', onAbort);
    };
    const finish = (error, value) => {
      cleanup();
      if (error) reject(error); else resolve(value);
    };
    const onAbort = () => finish(new Error('Przerwano przez użytkownika'));
    const onError = event => finish(new Error(event.message || 'Błąd Web Workera'));
    const onMessage = ({data}) => {
      if (data.type === 'progress' || data.type === 'waiting') {
        onProgress?.(data);
        return;
      }
      if (data.type === 'error') {
        finish(new Error(data.message || 'Błąd podczas benchmarku')); return;
      }
      if (data.type === expectedType) finish(null, data);
    };
    worker.addEventListener('message', onMessage);
    worker.addEventListener('error', onError);
    worker.addEventListener('messageerror', onError);
    signal?.addEventListener('abort', onAbort, {once: true});
    timeout = setTimeout(() => finish(new Error('Przekroczono limit czasu: ' + command.cmd)), 360000);
    worker.postMessage(command);
  });
}

export function summarizeSuite(runs) {
  const byStrategy = {};
  for (const {id, label} of STRATEGIES) {
    const own = runs.filter(run => run.strategy === id);
    const cold = own.find(run => run.phase === 'cold')?.report ?? null;
    const warms = own.filter(run => run.phase === 'warm').map(run => run.report);
    byStrategy[id] = {
      label,
      cold_total_ready_ms: cold?.meta.total_ready_ms ?? null,
      cold_search_p50_ms: cold?.summary.typical_top20_p50_ms ?? null,
      cold_downloaded_bytes: cold?.meta.downloaded_bytes ?? null,
      warm_runs: warms.length,
      warm_total_ready_p50_ms: median(warms.map(r => r.meta.total_ready_ms)),
      warm_search_p50_ms: median(warms.map(r => r.summary.typical_top20_p50_ms)),
      warm_slowest_p95_max_ms: warms.length
        ? Math.max(...warms.map(r => r.summary.slowest_top20_p95_ms)) : null
    };
  }
  const sequences = runs.map(run =>
    run.report.results.map(result => [result.query, result.hits]));
  return {
    runs_completed: runs.length,
    expected_runs: STRATEGIES.length * (1 + WARM_REPETITIONS),
    hit_counts_consistent: sequences.every(sequence =>
      JSON.stringify(sequence) === JSON.stringify(sequences[0])),
    by_strategy: byStrategy
  };
}

/** Removes just this test variant's database, not HTTP cache or any other origin data. */
export async function runFullSuite({makeWorker, onProgress = () => {}, signal} = {}) {
  const started = new Date().toISOString();
  const runs = [];
  let activeWorker = null;
  let error = null;
  const expected = STRATEGIES.length * (1 + WARM_REPETITIONS);
  const start = performance.now();
  const checkAbort = () => {
    if (signal?.aborted) throw new Error('Przerwano przez użytkownika');
  };
  try {
    for (const {id, label} of STRATEGIES) {
      checkAbort();
      onProgress('Resetowanie wyłącznie indeksu ' + label + '… (' +
        runs.length + '/' + expected + ')');
      activeWorker = makeWorker();
      await workerCommand(activeWorker, {cmd: 'purge', strategy: id},
        'purged', {signal});
      activeWorker.terminate();
      activeWorker = null;

      for (let i = 0; i <= WARM_REPETITIONS; i++) {
        checkAbort();
        const phase = i === 0 ? 'cold' : 'warm';
        const phaseName = i === 0 ? 'zimny' : 'ciepły ' + i + '/' + WARM_REPETITIONS;
        const prefix = label + ' · ' + phaseName + ' · przebieg ' +
          (runs.length + 1) + '/' + expected;
        onProgress(prefix + ' — uruchamianie bazy…');
        activeWorker = makeWorker();
        const ready = await workerCommand(activeWorker, {cmd: 'init', strategy: id},
          'ready', {
            signal, onProgress: data =>
              onProgress(data.type === 'waiting'
                ? prefix + ' — czekam na zwolnienie SQLite przez inną kartę…'
                : prefix + ' — indeksowanie ' + data.count + ' dokumentów…')
          });
        const meta = ready.meta;
        if (!meta.persisted) throw new Error(prefix + ': trwały magazyn OPFS jest niedostępny');
        if (i === 0 && meta.index_reused)
          throw new Error(prefix + ': zimny start niespodziewanie użył istniejącego indeksu');
        if (i > 0 && (!meta.index_reused || meta.downloaded_bytes !== 0))
          throw new Error(prefix + ': ciepły start nie wykorzystał istniejącego indeksu');
        if (i === 0 && (id === 'local-build' ? !meta.index_built : !meta.index_imported))
          throw new Error(prefix + ': zimny start nie utworzył/nie zaimportował indeksu');
        onProgress(prefix + ' — test 10 zapytań × 20 powtórzeń…');
        const response = await workerCommand(activeWorker, {cmd: 'benchmark'},
          'benchmark', {signal});
        const report = response.report;
        if (!report?.results?.length || report.meta.strategy !== id)
          throw new Error(prefix + ': niekompletny raport');
        runs.push({strategy: id, phase, repetition: i === 0 ? 1 : i, report});
        const summary = summarizeSuite(runs);
        if (!summary.hit_counts_consistent)
          throw new Error(prefix + ': liczba trafień nie jest zgodna z pozostałymi przebiegami');
        onProgress(prefix + ' — zakończono (' + runs.length + '/' + expected + ').');
        await workerCommand(activeWorker, {cmd: 'close'}, 'closed', {signal});
        activeWorker.terminate();
        activeWorker = null;
      }
    }
  } catch (failure) {
    error = String(failure?.message || failure);
  } finally {
    activeWorker?.terminate();
  }
  const finished = new Date().toISOString();
  return {
    version: 3,
    kind: 'one-click-full-benchmark',
    created_at: finished,
    started_at: started,
    finished_at: finished,
    duration_ms: Number((performance.now() - start).toFixed(2)),
    status: error ? (signal?.aborted ? 'cancelled' : 'failed') : 'completed',
    error,
    device: runs[0]?.report.device ?? {
      user_agent: navigator.userAgent,
      hardware_concurrency: navigator.hardwareConcurrency ?? null
    },
    configuration: {
      strategy_order: STRATEGIES.map(x => x.id),
      cold_runs_per_strategy: 1,
      warm_runs_per_strategy: WARM_REPETITIONS,
      search_rounds_per_query: 20,
      fresh_worker_for_every_run: true,
      local_database_reset_before_cold: true,
      http_cache_cleared: false,
      cache_note: 'Zimny = brak lokalnego indeksu; cache HTTP/przeglądarki może pozostać ciepły. Zapytania są powtarzane i obejmują cache.'
    },
    summary: summarizeSuite(runs),
    runs
  };
}
