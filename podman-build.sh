#!/bin/sh
set -eu

IMAGE=ghcr.io/tometchy/kazdydzienzjezusem
VERBOSE=0

if [ ! -f .env.cloudflare ]; then
  echo "Missing .env.cloudflare."
  echo "Copy .env.cloudflare.example to .env.cloudflare and paste the Cloudflare Tunnel token."
  exit 1
fi

while [ "${1:-}" != "" ]; do
  case "$1" in
    -v|--verbose)
      VERBOSE=1
      shift
      ;;
    *)
      echo "Unknown option: $1"
      exit 1
      ;;
  esac
done

run_step_streaming() {
  step_name="$1"
  shift

  echo "$step_name..."
  "$@"
  echo "$step_name done."
}

run_step() {
  step_name="$1"
  shift

  log_file="$(mktemp)"
  if "$@" >"$log_file" 2>&1; then
    echo "$step_name done."
    rm -f "$log_file"
    return 0
  fi

  echo "$step_name failed:"
  tail -n 50 "$log_file" || true
  rm -f "$log_file"
  return 1
}

if [ "$VERBOSE" -eq 1 ]; then
  run_step_streaming "Build" podman build -t kazdy-dzien . -f KazdyDzienZJezusem/Dockerfile -t "$IMAGE:latest"
  run_step_streaming "Push" podman push --authfile ~/.config/containers/auth.json "$IMAGE:latest"
  echo "Remove stale container..."
  podman rm -f kazdy-dzien >/dev/null 2>&1 || true
  echo "Remove stale container done."
  echo "Compose down..."
  podman compose -f compose.yaml down >/dev/null 2>&1 || true
  echo "Compose down done."
  run_step_streaming "Compose pull" podman compose -f compose.yaml pull
  run_step_streaming "Compose up" podman compose -f compose.yaml up -d
  exit 0
fi

run_step "Build" podman build -q -t kazdy-dzien . -f KazdyDzienZJezusem/Dockerfile -t "$IMAGE:latest"
run_step "Push" podman push -q --authfile ~/.config/containers/auth.json "$IMAGE:latest"

echo "Remove stale container..."
podman rm -f kazdy-dzien >/dev/null 2>&1 || true
echo "Remove stale container done."
echo "Compose down..."
podman compose -f compose.yaml down >/dev/null 2>&1 || true
echo "Compose down done."
run_step "Compose pull" podman compose -f compose.yaml pull
run_step "Compose up" podman compose -f compose.yaml up -d
