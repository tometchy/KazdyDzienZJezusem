# POC — Tauri 2 na Pixelu 3a XL / postmarketOS

Ten katalog służy do **natywnej aplikacji Linux ARM64 na postmarketOS/Alpine/musl**, a nie do aplikacji Android. Obejmuje dokładnie ten sam frontend Vite z `poc/frontend/`; nie tworzy drugiego UI, PWA ani serwera HTTP.

**Status:** eksperyment architektoniczny. Produkcyjne KDZJ nadal korzysta z ASP.NET Core/Redis i nie jest jeszcze przepisane.

## Pobierz i uruchom na Pixelu

1. Zmerguj PR po zaliczeniu natywnego CI.
2. Wejdź w GitHub Actions → **Tauri 2 postmarketOS ARM64 Alpine/musl** → najnowszy zielony przebieg, pobierz artefakt `kdzj-tauri-postmarketos-aarch64-musl` (ZIP).
3. Rozpakuj ZIP, następnie tar.gz. W katalogu z `kdzj-tauri-benchmark` uruchom:

```sh
tar -xzf kdzj-tauri-postmarketos-aarch64-musl.tar.gz -C .
sh ./install-pixel.sh
~/.local/bin/kdzj-tauri-benchmark
```

`install-pixel.sh` zainstaluje brakujące zależności Alpine za pomocą `doas` lub `sudo` i doda launcher Phosh. Nie instaluje aplikacji Android. Natywny plik wykonywalny jest skompilowany z musl (nie z glibc). Własne dane programu znajdują się w systemowym katalogu Tauri app data, a JSON zapisywany jest w Downloads.

Na Pixelu kliknij **Uruchom cały test Tauri: 1 zimny + 3 ciepłe** i **Zapisz zbiorczy JSON do Downloads**. Prześlij jeden JSON, który zawiera szczegółowe czasy oraz diagnostykę WebKitGTK/OPFS.

## Co jest mierzone

- Natywny **SQLite FTS5** (Rust/rusqlite z osadzonym SQLite), nie SQLite WASM.
- Skompresowany zrzut tej samej bazy źródłowej (115317 dokumentów w obecnym korpusie) jest **wbudowany w binarkę**. Przy zimnym uruchomieniu aplikacja sprawdza SHA-256 i rozpakowuje bazę do katalogu danych, a przy trzech kolejnych otwiera już zapisany plik.
- Dziesięć zapytań, każde dwadzieścia razy (TOP 20 + COUNT), p50/p95 i porównanie liczby trafień.
- `navigator.storage.getDirectory`, `createSyncAccessHandle`, `navigator.locks` i `crossOriginIsolated` w natywnym WebKitGTK są mierzone i dołączane do JSON. Nie należy zakładać dostępności OPFS na podstawie Chromium.
- JSON oznacza strategię `native-sqlite` i nie może być porównywany 1:1 z `prebuilt-sahpool` bez uwzględnienia IPC, WebView i innych warunków.

To **nie wymaga internetu** na Pixelu ani serwerowego API. Nie wykonano jeszcze uruchomienia na rzeczywistym Pixelu — raport z urządzenia jest konieczny do potwierdzenia zgodności i wydajności WebKitGTK/Phosh.

## Budowanie

W GitHub Actions zadanie `ubuntu-24.04-arm` generuje korpus i frontend, a następnie buduje binarkę **wewnątrz ARM64 Alpine 3.24**, z `webkit2gtk-4.1-dev`, `rust`, `cargo` i `pkgconf`, bez QEMU. Artefakt ZIP zawiera tar.gz oraz installer. Nie należy próbować przenosić pliku z builda `aarch64-unknown-linux-gnu` Ubuntu na Alpine.

Lokalnie na Alpine ARM64 można wykonać analogiczne kroki:

```sh
apk add build-base pkgconf cargo rust python3 py3-sqlite3 nodejs npm \
  webkit2gtk-4.1-dev openssl-dev libayatana-appindicator-dev librsvg-dev
python3 poc/prepare.py --repo . --out poc/frontend/public
python3 poc/build_sqlite.py --out poc/frontend/public
rm poc/frontend/public/corpus-fts5.sqlite3
cd poc/frontend
npm install
KDZJ_TAURI=1 npm run build
cargo build --manifest-path src-tauri/Cargo.toml --release
```

W pierwszej kolejności korzystaj z automatycznego builda: kompilacja Tauri/WebKitGTK na samym Pixelu z 4 GB RAM może być kosztowna.

## GitHub Pages — statyczny frontend, bez PWA

Dostępny jest osobny workflow `Publish static POC to GitHub Pages (no backend)`, uruchamiany **ręcznie** po włączeniu źródła Pages: GitHub Actions. Generuje statyczne zasoby HTML/JS/WASM/SQLite. Domyślny projektowy URL to `https://tometchy.github.io/KazdyDzienZJezusem/POC/`.

To **nie jest druga aplikacja do instalacji**. Tauri zawiera ten sam frontend jako lokalne pliki i pozostaje jedyną planowaną aplikacją. GitHub Pages jest wyłącznie hostingiem statycznym; nie zastępuje automatycznie wszystkich funkcji istniejącego backendu. Dopóki nie przepiszemy pozostałych ekranów/edycji, strona statyczna publikuje tylko POC.

Standardowy `opfs` VFS SQLite WASM wymaga nagłówków COOP/COEP. GitHub Pages nie zapewnia swobodnej konfiguracji takich nagłówków, więc **wariant B może być niedostępny** na Pages. Wariant C `opfs-sahpool` nie wymaga tych nagłówków, ale nadal wymaga działającego OPFS i SyncAccessHandle. Natywny silnik SQLite w Tauri jest od tych API niezależny.

## Pozostałe kroki do całkowitego usunięcia backendu

Trzeba przenieść do statycznego/natywnego modelu wszystkie właściwe ekrany wersetów, interlinear, odsyłacze, edycję tagów, zarządzanie aktualizacjami, synchronizację i ewentualne funkcje uwierzytelniania. PR z POC nie usuwa ASP.NET/Redis i nie zmienia produkcyjnej domeny. Po udanym eksperymencie plan migracji można realizować bez utrzymywania dwóch interfejsów.
