#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"
tools_dir="$repo_root/artifacts/visual-tools"
mkdir -p "$tools_dir"
export DOTNET_CLI_HOME="${DOTNET_CLI_HOME:-$repo_root/artifacts/dotnet-home}"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export PLAYWRIGHT_BROWSERS_PATH="${PLAYWRIGHT_BROWSERS_PATH:-$repo_root/artifacts/playwright}"
export PATH="$tools_dir/dotnet:$PATH"

if [[ "${1:-}" != "" && "${1:-}" != "--with-deps" ]] || (( $# > 1 )); then
  echo 'Usage: bash scripts/setup-visual-tests.sh [--with-deps]' >&2
  exit 2
fi

for dependency in node npm python3 curl tar make cc sha256sum; do
  command -v "$dependency" >/dev/null || { echo "Missing setup dependency: $dependency" >&2; exit 1; }
done

if ! command -v dotnet >/dev/null || ! dotnet --list-sdks | grep -q '^10\.'; then
  curl --fail --location --retry 3 https://dot.net/v1/dotnet-install.sh -o "$tools_dir/dotnet-install.sh"
  bash "$tools_dir/dotnet-install.sh" --channel 10.0 --install-dir "$tools_dir/dotnet" --no-path
fi

# A private Redis 8 binary: no root, daemon, container, system service, or RDB export.
if [[ ! -x "$tools_dir/redis-8.2.2/src/redis-server" || ! -x "$tools_dir/redis-8.2.2/src/redis-cli" ]]; then
  curl --fail --location --retry 3 https://download.redis.io/releases/redis-8.2.2.tar.gz -o "$tools_dir/redis-8.2.2.tar.gz"
  echo "4e340e8e822a82114b6fb0f7ca581b749fa876e31e36e9fbcb75416bec9d0608  $tools_dir/redis-8.2.2.tar.gz" | sha256sum --check
  tar -xzf "$tools_dir/redis-8.2.2.tar.gz" -C "$tools_dir"
  make -C "$tools_dir/redis-8.2.2/src" -j2 MALLOC=libc OPTIMIZATION=-O2 redis-server redis-cli
fi

if [[ ! -f "$tools_dir/liberation-fonts-ttf-2.1.5/LiberationSans-Regular.ttf" ]]; then
  curl --fail --location --retry 3 https://github.com/liberationfonts/liberation-fonts/files/7261482/liberation-fonts-ttf-2.1.5.tar.gz -o "$tools_dir/liberation-fonts-ttf-2.1.5.tar.gz"
  echo "7191c669bf38899f73a2094ed00f7b800553364f90e2637010a69c0e268f25d0  $tools_dir/liberation-fonts-ttf-2.1.5.tar.gz" | sha256sum --check
  tar -xzf "$tools_dir/liberation-fonts-ttf-2.1.5.tar.gz" -C "$tools_dir"
fi

dotnet restore KazdyDzienZJezusem.slnx
npm --prefix ProductionVisualTests ci
cd ProductionVisualTests
if [[ "${1:-}" == "--with-deps" ]]; then
  npx --no-install playwright install --with-deps chromium
else
  npx --no-install playwright install chromium
fi
