#!/bin/sh

echo "podman-run.sh is deprecated. Use compose/systemd instead."
exit 1

SCRIPT_DIR="/home/tom/Projects/KazdyDzienZJezusem"

podman run --rm \
  -p 8080:8080 \
  -v "$SCRIPT_DIR":/data-out \
  kazdy-dzien "$@"


# podman run --rm \
#  kazdy-dzien $1
