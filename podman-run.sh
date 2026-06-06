#!/bin/sh
SCRIPT_DIR="/home/tom/Projects/KazdyDzienZJezusem"

podman run --rm \
  -v "$SCRIPT_DIR":/data-out \
  kazdy-dzien $1
