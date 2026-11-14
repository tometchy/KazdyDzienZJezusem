#!/usr/bin/env bash
# Check the real deployed ASP.NET Core route after building/restarting the stack.
# Usage: bash scripts/smoke-test-poc.sh [https://kazdydzienzjezusem.pl]
set -euo pipefail

origin="${1:-https://kazdydzienzjezusem.pl}"
origin="${origin%/}"
tmpdir="$(mktemp -d)"
trap 'rm -rf "$tmpdir"' EXIT

for path in /POC /POC/; do
  status="$(curl -sS --max-time 30 --max-redirs 0 \
    -D "$tmpdir/headers" -o "$tmpdir/body" -w '%{http_code}' \
    "$origin$path")"
  if [[ "$status" != "200" ]]; then
    echo "FAIL: $path returned HTTP $status (expected 200 without redirects)" >&2
    grep -iE '^(location|HTTP/)' "$tmpdir/headers" >&2 || true
    exit 1
  fi
  if ! grep -q 'Test wydajności wyszukiwarki' "$tmpdir/body"; then
    echo "FAIL: $path did not return benchmark HTML" >&2
    exit 1
  fi
  if ! grep -iq '^cross-origin-opener-policy: same-origin' "$tmpdir/headers"; then
    echo "FAIL: $path did not provide OPFS isolation headers" >&2
    exit 1
  fi
  echo "PASS: $path -> HTTP 200, benchmark HTML and OPFS headers"
done

status="$(curl -sS --max-time 30 --max-redirs 0 \
  -o "$tmpdir/body" -w '%{http_code}' "$origin/POC/corpus-manifest.json")"
if [[ "$status" != "200" ]] || ! grep -q '"synthetic": false' "$tmpdir/body"; then
  echo "FAIL: POC manifest missing or synthetic (HTTP $status)" >&2
  exit 1
fi
echo "PASS: /POC/corpus-manifest.json contains real corpus information"
