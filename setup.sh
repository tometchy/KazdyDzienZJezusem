#!/bin/sh
set -eu

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
REPO_DIR="$SCRIPT_DIR"

IMAGE=ghcr.io/tometchy/kazdydzienzjezusem
VERBOSE=0
TOPICS_ONLY=0

while [ "${1:-}" != "" ]; do
  case "$1" in
    -v|--verbose)
      VERBOSE=1
      shift
      ;;
    --topics-only|--topics)
      TOPICS_ONLY=1
      shift
      ;;
    --help|-h)
      cat <<'EOF'
Usage: ./setup.sh [--topics-only] [-v|--verbose]

No arguments rebuilds the image and starts the stack.
The full NT and topic HTML are prebuilt in the Docker image, so startup only seeds the mounted HTML directory.
--topics-only regenerates Index/Topics from the source Topics/ directory and exits without touching Quartz or the running stack.
EOF
      exit 0
      ;;
    --all)
      shift
      ;;
    --vers)
      shift
      while [ "${1:-}" != "" ] && [ "${1#-}" = "$1" ]; do
        shift
      done
      ;;
    *)
      echo "Unknown option: $1"
      exit 1
      ;;
  esac
done

if [ "$TOPICS_ONLY" -eq 1 ]; then
  cd "$REPO_DIR"
  python3 "$REPO_DIR/scripts/regenerate-topics.py"
  exit 0
fi

if [ ! -f "$REPO_DIR/.env.cloudflare" ]; then
  echo "Missing .env.cloudflare."
  echo "Copy .env.cloudflare.example to .env.cloudflare and paste the Cloudflare Tunnel token."
  exit 1
fi

cp "$REPO_DIR/.env.cloudflare" "$REPO_DIR/.env"

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

wait_for_site() {
  timeout_hours="${QUARTZ_BUILD_TIMEOUT_HOURS:-48}"
  timeout_seconds=$((timeout_hours * 60 * 60))
  echo "Waiting for site to answer on http://127.0.0.1:8080/..."
  attempts=0
  until curl -fsS --max-time 2 http://127.0.0.1:8080/ >/dev/null 2>&1; do
    attempts=$((attempts + 1))
    if [ "$attempts" -ge "$timeout_seconds" ]; then
      echo "Timed out waiting for the site to become available."
      exit 1
    fi
    sleep 1
  done
  echo "Site is available."
}

cd "$REPO_DIR"

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
  wait_for_site
  exit 0
fi

run_step "Build" podman build -q -t kazdy-dzien . -f KazdyDzienZJezusem/Dockerfile -t "$IMAGE:latest"
run_step "Push" podman push -q --authfile ~/.config/containers/auth.json "$IMAGE:latest"

echo "Remove compose project containers..."
podman ps -aq --filter label=io.podman.compose.project=kazdydzienzjezusem | xargs -r podman rm -f >/dev/null 2>&1 || true
echo "Remove compose project containers done."

echo "Remove stale container..."
podman rm -f kazdy-dzien >/dev/null 2>&1 || true
echo "Remove stale container done."
echo "Compose down..."
podman compose -f compose.yaml down >/dev/null 2>&1 || true
echo "Compose down done."
run_step "Compose pull" podman compose -f compose.yaml pull
run_step "Compose up" podman compose -f compose.yaml up -d
wait_for_site
