#!/bin/sh
set -eu

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"

# podman-compose 1.0.6 does not reliably recreate containers when only the
# image changes, so remove the existing stack before bringing it back up.
podman compose -f "$SCRIPT_DIR/compose.yaml" down
exec podman compose -f "$SCRIPT_DIR/compose.yaml" up --build --force-recreate -d "$@"
