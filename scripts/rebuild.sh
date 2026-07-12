#!/bin/sh
set -eu

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
REPO_DIR="$(dirname "$SCRIPT_DIR")"

cd "$REPO_DIR"

if [ "${1:-}" != "" ]; then
  echo "Generating requested verse output..."
  ./podman-run.sh "$@"
fi

echo "Rebuilding and refreshing the host stack..."
./podman-build.sh
