#!/bin/sh
set -eu

SOURCE_ROOT="/opt/prebuilt/IndexHtml"
TARGET_ROOT="/data-out/IndexHtml"

if [ ! -d "$SOURCE_ROOT" ]; then
  echo "Missing prebuilt site: $SOURCE_ROOT"
  exit 1
fi

mkdir -p /data-out
rm -rf "$TARGET_ROOT"
cp -a "$SOURCE_ROOT" "$TARGET_ROOT"

echo "Serving /data-out/IndexHtml on http://0.0.0.0:8080"

shutdown() {
  echo "Stopping..."

  if [ -n "${server_pid:-}" ]; then
    kill "$server_pid" 2>/dev/null || true
    wait "$server_pid" 2>/dev/null || true
  fi
}

trap shutdown INT TERM

node <<'NODE' &
const http = require("http");
const fs = require("fs");
const path = require("path");

const root = "/data-out/IndexHtml";
const types = {
  ".html": "text/html; charset=utf-8",
  ".css": "text/css; charset=utf-8",
  ".js": "application/javascript; charset=utf-8",
  ".json": "application/json; charset=utf-8",
  ".webp": "image/webp",
  ".png": "image/png",
  ".jpg": "image/jpeg",
  ".jpeg": "image/jpeg",
  ".svg": "image/svg+xml",
  ".ico": "image/x-icon",
  ".txt": "text/plain; charset=utf-8",
  ".xml": "application/xml; charset=utf-8",
};

function resolveFile(urlPath) {
  const decoded = decodeURIComponent(urlPath.split("?")[0]);
  const normalized = path.normalize(decoded).replace(/^(\.\.[/\\])+/, "");
  const relative = normalized.replace(/^[/\\]+/, "") || "index.html";
  const requested = path.join(root, relative);

  const candidates = [
    requested,
    requested.endsWith(path.sep) ? path.join(requested, "index.html") : `${requested}.html`,
    path.join(requested, "index.html"),
  ];

  for (const candidate of candidates) {
    const resolved = path.resolve(candidate);
    if (!resolved.startsWith(root + path.sep) && resolved !== root) continue;
    try {
      if (fs.statSync(resolved).isFile()) return resolved;
    } catch {
      // Try the next candidate.
    }
  }

  return null;
}

http.createServer((req, res) => {
  const file = resolveFile(req.url || "/");

  if (!file) {
    res.writeHead(404, { "content-type": "text/plain; charset=utf-8" });
    res.end("404 Not Found\n");
    return;
  }

  res.writeHead(200, { "content-type": types[path.extname(file)] || "application/octet-stream" });
  fs.createReadStream(file).pipe(res);
}).listen(8080, "0.0.0.0");
NODE
server_pid=$!

wait "$server_pid"
