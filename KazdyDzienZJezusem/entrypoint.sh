#!/bin/sh
set -e

redis-server --dir /data --save "" --pidfile /tmp/redis.pid --logfile /tmp/redis.log --daemonize yes

# czekamy az dane sie zaladuja (nie tylko ping)
attempts=0
until redis-cli EXISTS gnt:John:1:1 2>/dev/null | grep -q 1; do
  attempts=$((attempts + 1))
  if [ "$attempts" -ge 100 ]; then
    echo "Redis nie wystartowal poprawnie. Log:"
    cat /tmp/redis.log || true
    exit 1
  fi
  sleep 0.1
done

if [ -n "${KAZDY_DZIEN_ARGS:-}" ]; then
  # shellcheck disable=SC2086
  set -- $KAZDY_DZIEN_ARGS "$@"
fi

dotnet KazdyDzienZJezusem.dll "$@"

if [ ! -f /data-out/IndexHtml/index.html ]; then
  echo "Quartz HTML was not generated: /data-out/IndexHtml/index.html is missing"
  exit 1
fi

echo "Serving /data-out/IndexHtml on http://0.0.0.0:8080"


shutdown() {
  echo "Stopping..."

  if [ -n "${server_pid:-}" ]; then
    kill "$server_pid" 2>/dev/null || true
    wait "$server_pid" 2>/dev/null || true
  fi

  if [ -f /tmp/redis.pid ]; then
    redis-cli shutdown nosave >/dev/null 2>&1 || kill "$(cat /tmp/redis.pid)" 2>/dev/null || true
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
