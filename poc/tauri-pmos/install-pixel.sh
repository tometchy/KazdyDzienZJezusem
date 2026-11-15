#!/bin/sh
# Install the prebuilt MUSL/AArch64 Tauri benchmark on postmarketOS Phosh.
set -eu
HERE="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
BIN="$HERE/kdzj-tauri-benchmark"
if [ ! -f "$BIN" ]; then
  echo "Brak kdzj-tauri-benchmark obok skryptu. Rozpakuj cały artefakt Actions." >&2
  exit 1
fi
if [ "$(uname -m)" != "aarch64" ]; then
  echo "Ten build jest wyłącznie dla ARM64 (aarch64), nie dla $(uname -m)." >&2
  exit 1
fi
if [ ! -f /etc/alpine-release ]; then
  echo "Ten pakiet jest przeznaczony dla postmarketOS/Alpine (musl)." >&2
  exit 1
fi
if command -v doas >/dev/null 2>&1; then
  doas apk add --no-cache webkit2gtk-4.1 font-dejavu ca-certificates
elif command -v sudo >/dev/null 2>&1; then
  sudo apk add --no-cache webkit2gtk-4.1 font-dejavu ca-certificates
else
  echo "Zainstaluj jako root: apk add webkit2gtk-4.1 font-dejavu ca-certificates" >&2
  echo "Następnie uruchom ponownie: $0" >&2
  exit 1
fi
DEST="$HOME/.local/opt/kdzj-tauri-benchmark"
mkdir -p "$DEST" "$HOME/.local/bin" "$HOME/.local/share/applications"
cp "$BIN" "$DEST/kdzj-tauri-benchmark"
chmod 755 "$DEST/kdzj-tauri-benchmark"
cat > "$HOME/.local/bin/kdzj-tauri-benchmark" <<'EOF'
#!/bin/sh
# Workaround for some Qualcomm/Phosh WebKitGTK renderers.
export WEBKIT_DISABLE_DMABUF_RENDERER=1
exec "$HOME/.local/opt/kdzj-tauri-benchmark/kdzj-tauri-benchmark" "$@"
EOF
chmod 755 "$HOME/.local/bin/kdzj-tauri-benchmark"
cat > "$HOME/.local/share/applications/pl.kazdydzienzjezusem.benchmark.desktop" <<EOF
[Desktop Entry]
Type=Application
Name=KDZJ — benchmark SQLite
Exec=$HOME/.local/bin/kdzj-tauri-benchmark
Terminal=false
Categories=Education;Utility;
StartupNotify=true
EOF
echo "Zainstalowano. Uruchom: $HOME/.local/bin/kdzj-tauri-benchmark"
echo "Raport zapiszesz w aplikacji przyciskiem Zapisz zbiorczy JSON do Downloads."
