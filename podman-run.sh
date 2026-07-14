#!/bin/sh
set -eu

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
if [ "$#" -eq 0 ]; then
  exec "$SCRIPT_DIR/setup.sh"
fi

exec "$SCRIPT_DIR/setup.sh" --vers "$@"
