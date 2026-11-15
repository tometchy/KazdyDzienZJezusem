# Test SQLite FTS5 w przeglądarce (/POC) — warianty porównawcze

Strona: https://kazdydzienzjezusem.pl/POC (także /POC/). **Nie modyfikuje
produkcyjnego Redisa, ASP.NET Core, tagów ani synchronizacji GitHub.**

## Co zostało zmienione

Build obrazu Podman ma dwa tymczasowe etapy:

1. Python przetwarza prawdziwe pliki z input/ do korpusu JSONL gzip.
   Od tej wersji indeksuje również formy słownikowe z głównego korpusu
   interlinearnego (wcześniej pomijano je, poza 2831 hasłami supplement).
2. Python sqlite3 (z FTS5) tworzy kompletny plik corpus-fts5.sqlite3
   zgodny ze schematem przeglądarkowego POC, sprawdza integralność SQLite
   i FTS5, zapisuje SHA-256, następnie kompresuje go gzip.
3. Node/Vite dołącza corpus.jsonl.gz, corpus-fts5.sqlite3.gz, manifest
   i SQLite WASM do statycznego katalogu /POC.
4. Surowy plik .sqlite3 jest usuwany z artefaktów WWW, aby nie publikować
   drugiej kopii bazy. Python i Node nie są instalowane w produkcyjnym kontenerze.

### Porównywane warianty

| Wariant | Zimny start (brak lokalnej bazy) | Ciepły start |
| --- | --- | --- |
| A · local-build | Pobranie korpusu, dekompresja, wstawianie do FTS5 w Workerze | Otworzenie istniejącej bazy |
| B · prebuilt-opfs | Pobranie skompresowanej bazy, SHA-256, dekompresja i OpfsDb.importDb() | Otworzenie istniejącej bazy |
| C · prebuilt-sahpool | Jak B, ale PoolUtil.importDb() | Otworzenie bazy w SAH Pool |

Warianty mają odrębne nazwy baz; A zachowuje dawną nazwę
/ kdj-search-poc-v2.sqlite3 (bez odstępu) i pozwala porównać stare pomiary.
Reset usuwa wyłącznie bazę aktualnie wybranego wariantu, **nie** localStorage,
raporty ani inne bazy.

W tabeli widzisz osobno transfer, realne pobrane bajty, dekompresję, import,
budowanie indeksu, otwarcie bazy przy ponownym użyciu, rozmiar SQLite,
czas całkowity oraz p50/p95. Kod pobiera archiwum do pamięci i weryfikuje
SHA-256 pliku gzip i bazy — to obciążenie także wchodzi w total_ready_ms.
Wariant A zachowuje wyszukiwania FTS5, ale buduje indeks lokalnie.

**Nie ma wiarygodnego, jednolitego API do pomiaru RAM** dla wszystkich tych
przeglądarek; do JSON trafia memory_bytes=null. navigator.storage.estimate()
raportuje szacunkowe zużycie *całego originu*, a nie RAM ani rozmiar pojedynczej
bazy. Z tego nie wolno wyprowadzać wniosków o maksymalnej pamięci Pixela.

## Jak mierzyć

1. Otwórz /POC po przebudowie obrazu z tego PR.
2. Wybierz A; kliknij Resetuj indeks, uruchom benchmark, skopiuj raport JSON.
3. Kliknij Zamknij i otwórz ponownie (nowy Web Worker) i zmierz ponownie.
4. Powtórz osobno dla B oraz C. Na każdym urządzeniu testuj oba starty.
5. Wysyłaj raporty z Pixela, X13s i Z510. Sprawdź również liczbę trafień,
   zwłaszcza source:Slownik AND gr:λόγος.

Reset uruchamia nowego Workera, dlatego obejmuje ponowne ładowanie SQLite
WASM, ale zasoby HTTP i Service Worker/cache przeglądarki mogą pozostać
rozgrzane. Do pomiaru prawdziwego pierwszego pobrania użyj osobnego profilu
lub narzędzi sieciowych; nie czyść całej pamięci domeny, jeżeli chcesz zachować
raporty. Wszystkie 20 powtórzeń danego zapytania są identyczne — odczyt
p50/p95 **obejmuje cache** i nie zastępuje testu zmiennych zapytań.

Raporty v2 są zapisywane pod nowym kluczem localStorage; raporty v1 pod
kdj.search-poc.reports.v1 są tylko odczytywane, nie zmieniane ani kasowane.

## Kontrole poprawności danych

Manifest podaje liczby dokumentów per źródło, liczbę ksiąg i listę
tnp_missing_vs_ubg. W aktualnym drzewie projektu TNP zawiera pliki dla
47 ksiąg, UBG dla 66. To **nie** dowodzi, że publikowana TNP rzeczywiście
obejmuje tylko 47 ksiąg — dowodzi jedynie, że obecny katalog input/TNP jest
niepełny względem UBG. Należy osobno zweryfikować konwerter i EPUB.

Słownik FTS5 indeksuje formy i dostępne glosy; nie jest to jeszcze pełen
model morfologiczny, analizator zapytań mat:/tnp:/lemma:/strong: ani indeks
łączący słownik z TR. Normalizacja usuwa akcenty i przydechy, zamienia
końcowe ς w σ, nie zamienia natomiast wszystkich form fleksyjnych na lematy.
Zmienia to semantykę niektórych zapytań i wymaga rozróżnienia w przyszłości
wyszukiwania ścisłego od znormalizowanego.

## Testy

Automatyczny workflow .github/workflows/poc-sqlite.yml wykonuje testy
syntetycznych fixture'ów, buduje bazę z *rzeczywistych* danych repo,
weryfikuje FTS5 / rozmiary / SHA-256 oraz buduje frontend Vite.
Nie wymaga konta użytkownika, tokenów ani dostępu do produkcyjnego Redisa.

Lokalnie (z checkout repo):

    python3 -m unittest discover -s tests -p test_poc_sqlite.py -v
    python3 poc/prepare.py --repo . --out poc/frontend/public
    python3 poc/build_sqlite.py --out poc/frontend/public
    rm poc/frontend/public/corpus-fts5.sqlite3
    cd poc/frontend
    npm install --no-audit --no-fund
    npm run test:urls && npm run build && npm run check:dist

Do wdrożenia użyj swojego ./podman-up.sh po zatwierdzeniu PR.

## Tematy na następny etap

- HTTP Range / stronicowany VFS: osobny POC, nie należy uznawać go
  za sprawdzony tylko dlatego, że import pełnego indeksu działa.
- Aktualizacje fragmentów: potrzebny manifest wersji/shardów, schemat
  migracji i transakcyjna podmiana, nie ręczne patchowanie skompresowanej bazy.
- Dzielenie indeksów po zasobach, księgach, językach oraz licencjach.
- Testy query diversity, cold-cache sieci, limitów pamięci 4 GB i SEO.
