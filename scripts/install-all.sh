#!/bin/sh
set -eu

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"

"$SCRIPT_DIR/install-systemd.sh"

"$SCRIPT_DIR/rebuild.sh"

echo "Installation complete."
echo "Compose stack is enabled and running."
echo "Git sync timer is enabled."
