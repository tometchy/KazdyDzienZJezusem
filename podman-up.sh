#!/bin/sh
set -eu

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"

if [ "$#" -gt 1 ]; then
    echo "Usage: $0 [repo_dir]" >&2
    exit 1
fi

if [ "$#" -eq 1 ]; then
    if [ ! -d "$1" ]; then
        echo "Error: repository directory does not exist: $1" >&2
        exit 1
    fi

    SCRIPT_DIR=$(CDPATH= cd -- "$1" && pwd)
fi

cd "$SCRIPT_DIR"
# Reuse GtdSystem's inherited lock descriptor, or acquire the same lock for a
# manual invocation. Reopening it while GtdSystem owns it would deadlock.
if [ -d "$SCRIPT_DIR/.git" ]; then
    refresh_lock="$SCRIPT_DIR/.git/gtd-kdzj-sync.lock"
    if [ "$(readlink "/proc/$$/fd/9" 2>/dev/null || true)" != "$refresh_lock" ]; then
        exec 9>"$refresh_lock"
    fi
    if ! flock -w 60 9; then
        echo "Another KDZJ refresh is still running; retry later." >&2
        exit 1
    fi
fi
# python-dotenv is already a dependency of podman-compose. Read only this key;
# never source .env as shell code or print the Cloudflare/GitHub token values.
if [ -z "${KDJ_GITHUB_READ_TOKEN_FILE:-}" ]; then
    KDJ_GITHUB_READ_TOKEN_FILE="$(python3 -c \
        'from dotenv import dotenv_values; print(dotenv_values(".env").get("KDJ_GITHUB_READ_TOKEN_FILE") or "")')"
fi
export KDJ_GITHUB_READ_TOKEN_FILE
if [ -z "$KDJ_GITHUB_READ_TOKEN_FILE" ] || [ ! -r "$KDJ_GITHUB_READ_TOKEN_FILE" ] || [ ! -s "$KDJ_GITHUB_READ_TOKEN_FILE" ]; then
    echo "Set KDJ_GITHUB_READ_TOKEN_FILE in .env or the environment to a readable, nonempty readonly token file." >&2
    exit 1
fi

git log -1

# podman-compose 1.0.6 ignores build.no_cache and can hide a failed build.
# Native Podman propagates failure and mounts the secret without saving it.
# Rebuild before stopping anything; the clone must always fetch current master.
podman build --no-cache \
    --secret "id=github_read_token,src=$KDJ_GITHUB_READ_TOKEN_FILE" \
    -f "$SCRIPT_DIR/KazdyDzienZJezusem/Dockerfile" \
    -t localhost/kazdydzienzjezusem_kazdy-dzien:latest "$SCRIPT_DIR"

# Targeted recreation works with podman-compose 1.0.6, whose `up --no-deps`
# can restart dependencies. The helper pulls Cloudflared before stopping any
# service, preserves Redis, and restores a previous container if readiness fails.
exec python3 "$SCRIPT_DIR/scripts/refresh-containers.py" "$SCRIPT_DIR"
