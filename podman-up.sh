#!/bin/sh
set -eu

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"

exec podman compose -f "$SCRIPT_DIR/compose.yaml" up --build -d "$@"
