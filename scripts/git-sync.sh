#!/bin/sh
set -eu

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
REPO_DIR="$(dirname "$SCRIPT_DIR")"

cd "$REPO_DIR"

if [ ! -d .git ]; then
  echo "This script must be run from a git repository."
  exit 1
fi

LOCK_FILE="/tmp/kazdy-dzien-git-sync.lock"
exec 9>"$LOCK_FILE"
if ! flock -n 9; then
  echo "A previous sync is still running; skipping this tick."
  exit 0
fi

current_branch="$(git branch --show-current)"
if [ "$current_branch" != "master" ]; then
  echo "Expected branch master, got ${current_branch:-detached}."
  exit 1
fi

git fetch origin master

local_head="$(git rev-parse master)"
remote_head="$(git rev-parse origin/master)"

if [ "$local_head" = "$remote_head" ]; then
  echo "master is up to date."
  exit 0
fi

echo "Updating master from origin/master..."
git reset --hard origin/master

echo "Rebuilding and pushing the image..."
./podman-build.sh
