#!/bin/sh
set -eu

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
REPO_DIR="$SCRIPT_DIR"

IMAGE=ghcr.io/tometchy/kazdydzienzjezusem
VERBOSE=0
KAZDY_DZIEN_ARGS="${KAZDY_DZIEN_ARGS:-}"

if [ ! -f "$REPO_DIR/.env.cloudflare" ]; then
  echo "Missing .env.cloudflare."
  echo "Copy .env.cloudflare.example to .env.cloudflare and paste the Cloudflare Tunnel token."
  exit 1
fi

cp "$REPO_DIR/.env.cloudflare" "$REPO_DIR/.env"

while [ "${1:-}" != "" ]; do
  case "$1" in
    -v|--verbose)
      VERBOSE=1
      shift
      ;;
    --all)
      KAZDY_DZIEN_ARGS="--all"
      shift
      ;;
    --vers)
      shift
      if [ "${1:-}" = "" ]; then
        echo "Missing verse references after --vers."
        exit 1
      fi

      KAZDY_DZIEN_ARGS="$*"
      break
      ;;
    --help|-h)
      cat <<'EOF'
Usage: ./setup.sh [--all] [--vers REF [REF ...]] [-v|--verbose]

No arguments rebuilds the image, refreshes topic content only, and starts the stack.
--all regenerates the full New Testament.
--vers regenerates only the listed verse references, for example:
  ./setup.sh --vers jhn3,16 rom1,1 1co13,4
EOF
      exit 0
      ;;
    *)
      echo "Unknown option: $1"
      exit 1
      ;;
  esac
done

export KAZDY_DZIEN_ARGS

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

wait_for_all_generation() {
  if [ "$KAZDY_DZIEN_ARGS" != "--all" ]; then
    return 0
  fi

  echo "Waiting for full NT generation to finish..."
  attempts=0
  until [ -f "$REPO_DIR/IndexHtml/index.html" ]; do
    attempts=$((attempts + 1))
    if [ "$attempts" -ge 1800 ]; then
      echo "Timed out waiting for IndexHtml/index.html."
      exit 1
    fi
    sleep 1
  done
  echo "Full NT generation finished."
}

wait_for_site() {
  timeout_seconds=$((12 * 60 * 60))
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

prepare_all_generation() {
  if [ "$KAZDY_DZIEN_ARGS" = "--all" ]; then
    rm -rf "$REPO_DIR/IndexHtml"
  fi
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
  prepare_all_generation
  run_step_streaming "Compose up" podman compose -f compose.yaml up -d
  wait_for_all_generation
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
prepare_all_generation
run_step "Compose up" podman compose -f compose.yaml up -d
wait_for_all_generation
wait_for_site
