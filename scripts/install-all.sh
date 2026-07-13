#!/bin/sh
set -eu

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
REPO_DIR="$(dirname "$SCRIPT_DIR")"

if [ ! -f "$REPO_DIR/.env.cloudflare" ]; then
  echo "Missing .env.cloudflare."
  echo "Copy .env.cloudflare.example to .env.cloudflare and paste the tunnel token from Cloudflare."
  exit 1
fi

cp "$REPO_DIR/.env.cloudflare" "$REPO_DIR/.env"

"$SCRIPT_DIR/install-systemd.sh"

"$SCRIPT_DIR/rebuild.sh"

echo "Installation complete."
echo "Compose stack is enabled and running."
echo "Git sync timer is enabled."
