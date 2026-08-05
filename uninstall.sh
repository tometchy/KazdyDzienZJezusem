#!/bin/sh
set -eu

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
REPO_DIR="$SCRIPT_DIR"
SYSTEMD_DIR="${XDG_CONFIG_HOME:-$HOME/.config}/systemd/user"
COMPOSE_SERVICE_NAME="kazdy-dzien-compose.service"
SYNC_SERVICE_NAME="kazdy-dzien-git-sync"

echo "Stopping project containers and compose stack..."
podman compose -f "$REPO_DIR/compose.yaml" down --remove-orphans >/dev/null 2>&1 || true

echo "Removing project containers..."
podman ps -aq --filter name='^kazdy-dzien$' | xargs -r podman rm -f >/dev/null 2>&1 || true
podman ps -aq --filter label=io.podman.compose.project=kazdydzienzjezusem | xargs -r podman rm -f >/dev/null 2>&1 || true

if command -v systemctl >/dev/null 2>&1; then
  echo "Disabling user systemd units..."
  systemctl --user disable --now "$COMPOSE_SERVICE_NAME" >/dev/null 2>&1 || true
  systemctl --user disable --now "$SYNC_SERVICE_NAME.timer" >/dev/null 2>&1 || true
  systemctl --user disable --now "$SYNC_SERVICE_NAME.service" >/dev/null 2>&1 || true
fi

echo "Removing user systemd unit files..."
rm -f \
  "$SYSTEMD_DIR/$COMPOSE_SERVICE_NAME" \
  "$SYSTEMD_DIR/$SYNC_SERVICE_NAME.service" \
  "$SYSTEMD_DIR/$SYNC_SERVICE_NAME.timer"

if command -v systemctl >/dev/null 2>&1; then
  systemctl --user daemon-reload >/dev/null 2>&1 || true
  systemctl --user reset-failed >/dev/null 2>&1 || true
fi

echo "Cleanup complete."
echo "Kept images, generated artifacts, and the repository itself."
