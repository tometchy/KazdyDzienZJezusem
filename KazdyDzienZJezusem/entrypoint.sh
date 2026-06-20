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

dotnet KazdyDzienZJezusem.dll "$@"

if [ ! -f /data-out/IndexHtml/index.html ]; then
  echo "Quartz HTML was not generated: /data-out/IndexHtml/index.html is missing"
  exit 1
fi

echo "Serving /data-out/IndexHtml on http://0.0.0.0:8080"
#exec httpd -f -p 0.0.0.0:8080 -h /data-out/IndexHtml

shutdown() {
  echo "Stopping..."

  if [ -n "${httpd_pid:-}" ]; then
    kill "$httpd_pid" 2>/dev/null || true
    wait "$httpd_pid" 2>/dev/null || true
  fi

  if [ -f /tmp/redis.pid ]; then
    redis-cli shutdown nosave >/dev/null 2>&1 || kill "$(cat /tmp/redis.pid)" 2>/dev/null || true
  fi
}

trap shutdown INT TERM

httpd -f -p 0.0.0.0:8080 -h /data-out/IndexHtml &
httpd_pid=$!

wait "$httpd_pid"