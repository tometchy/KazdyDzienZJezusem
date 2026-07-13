#!/bin/sh
set -eu

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
REPO_DIR="$(dirname "$SCRIPT_DIR")"

cd "$REPO_DIR"

BUILD_ARGS=""
VERSE_ARGS=""

for arg in "$@"; do
  case "$arg" in
    -v|--verbose)
      BUILD_ARGS="--verbose"
      ;;
    *)
      VERSE_ARGS="$VERSE_ARGS $arg"
      ;;
  esac
done

if [ "${VERSE_ARGS# }" != "" ]; then
  echo "Generating requested verse output..."
  # shellcheck disable=SC2086
  ./podman-run.sh ${VERSE_ARGS# }
fi

echo "Rebuilding and refreshing the host stack..."
./podman-build.sh $BUILD_ARGS
