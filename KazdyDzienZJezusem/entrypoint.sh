#!/bin/sh
set -e

redis-server --dir /data --save "" --daemonize yes

# czekamy az dane sie zaladuja (nie tylko ping)
until redis-cli EXISTS gnt:John:1:1 | grep -q 1; do
  sleep 0.1
done

dotnet KazdyDzienZJezusem.dll "$@"
