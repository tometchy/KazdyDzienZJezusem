// URLs for the generated public corpus are relative to the Vite *site base*,
// not the compiled Web Worker's /POC/assets/ location.
// In production Vite replaces import.meta.env.BASE_URL with '/POC/'.
export function corpusAssetUrl(filename, baseUrl, workerUrl) {
  if (!/^[a-z0-9][a-z0-9.-]*$/i.test(filename)) {
    throw new Error('Invalid corpus asset name');
  }
  if (baseUrl === './') {
    // Tauri packages assets at the app origin root, Vite puts the worker in /assets/.
    return new URL('../' + filename, workerUrl).href;
  }
  if (!baseUrl.startsWith('/') || !baseUrl.endsWith('/')) {
    throw new Error('Expected absolute or relative "./" Vite base URL');
  }
  return new URL(baseUrl + filename, workerUrl).href;
}
