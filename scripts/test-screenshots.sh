#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"
export PATH="$repo_root/artifacts/visual-tools/dotnet:$PATH"
export DOTNET_CLI_HOME="${DOTNET_CLI_HOME:-$repo_root/artifacts/dotnet-home}"
export PLAYWRIGHT_BROWSERS_PATH="${PLAYWRIGHT_BROWSERS_PATH:-$repo_root/artifacts/playwright}"
npm --prefix ProductionVisualTests test -- "$@"
