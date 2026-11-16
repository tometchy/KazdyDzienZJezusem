/** Pure summary formatter, safe while the native benchmark has only 0–3 runs.
 * WebKitGTK calls render() after EVERY run. The aggregate report.summary only
 * exists after all four runs have finished successfully.
 */
export function nativeMetricsText(report, fmt) {
  if (!report) return '';
  const runs = Array.isArray(report.runs) ? report.runs : [];
  const summary = report.summary;

  if (!summary) {
    const cold = runs.find(run => run?.phase === 'cold')?.report?.meta?.total_ready_ms;
    return 'Wykonano ' + runs.length + '/4 przebiegów' +
      (cold == null ? '' : ' · Zimny start: ' + fmt(cold)) +
      ' · Mediana ciepłych startów po zakończeniu testu.';
  }

  return 'Zimny start: ' + fmt(summary.cold_ready_ms) +
    ' · Mediana 3 ciepłych: ' + fmt(summary.warm_ready_p50_ms) +
    ' · SQL p50 ciepły: ' + fmt(summary.warm_search_p50_ms);
}
