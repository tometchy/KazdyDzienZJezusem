#!/bin/sh
set -eu

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
REPO_DIR="$(dirname "$SCRIPT_DIR")"
SYSTEMD_DIR="${XDG_CONFIG_HOME:-$HOME/.config}/systemd/user"
SERVICE_NAME="kazdy-dzien-git-sync"
COMPOSE_SERVICE_NAME="kazdy-dzien-compose.service"

if ! command -v podman >/dev/null 2>&1; then
  echo "podman is required."
  exit 1
fi

if ! command -v podman-compose >/dev/null 2>&1 && ! command -v docker-compose >/dev/null 2>&1; then
  echo "Installing podman-compose..."
  sudo apt update
  sudo apt install -y podman-compose
fi

mkdir -p "$SYSTEMD_DIR"

cp "$REPO_DIR/systemd/user/kazdy-dzien-compose.service" "$SYSTEMD_DIR/$COMPOSE_SERVICE_NAME"

cat > "$SYSTEMD_DIR/$SERVICE_NAME.service" <<EOF
[Unit]
Description=Fetch repository changes and rebuild the image

[Service]
Type=oneshot
WorkingDirectory=$REPO_DIR
ExecStart=$REPO_DIR/scripts/git-sync.sh
EOF

cat > "$SYSTEMD_DIR/$SERVICE_NAME.timer" <<EOF
[Unit]
Description=Run git sync every five minutes

[Timer]
OnBootSec=5min
OnUnitActiveSec=5min
Persistent=true
Unit=$SERVICE_NAME.service

[Install]
WantedBy=timers.target
EOF

systemctl --user daemon-reload
systemctl --user enable --now "$COMPOSE_SERVICE_NAME"
systemctl --user enable --now "$SERVICE_NAME.timer"

echo "Installed $COMPOSE_SERVICE_NAME, $SERVICE_NAME.service, and $SERVICE_NAME.timer"
