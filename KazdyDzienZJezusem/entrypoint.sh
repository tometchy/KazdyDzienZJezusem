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
