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

echo "Script dit: $SCRIPT_DIR"

# podman-compose 1.0.6 does not reliably recreate containers when only the
# image changes, so remove the existing stack before bringing it back up.
podman compose -f "$SCRIPT_DIR/compose.yaml" down
exec podman compose -f "$SCRIPT_DIR/compose.yaml" up --build --force-recreate -d "$@"
