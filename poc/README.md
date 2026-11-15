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
`/kdj-search-poc-v2.sqlite3` i pozwala porównać stare pomiary.
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

## Jak mierzyć — jednym przyciskiem

1. Otwórz /POC po przebudowie obrazu; zaczekaj, aż zakończy się inicjalizacja
   pojedynczego wariantu.
2. Kliknij **Uruchom cały test A/B/C** i zostaw kartę aktywną. Na telefonie
   pozostaw ekran włączony (przejście do tła może zakłócić pomiar).
3. Po zakończeniu kliknij **Kopiuj jeden zbiorczy JSON** lub **Pobierz zbiorczy
   JSON**. Prześlij jeden plik lub wklej jeden JSON, zamiast 12 raportów.

Automatyczna sekwencja wykonuje po kolei A/B/C: dla każdego wariantu usuwa
tylko jego lokalną bazę, uruchamia **nowy Worker** i mierzy jeden zimny start;
następnie trzykrotnie zamyka bazę i uruchamia nowy Worker z zachowanym
indeksem, mierząc trzy ciepłe starty. Każdy z 12 przebiegów obejmuje 10
zapytań × 20 powtórzeń. Jedna paczka JSON v3 przechowuje komplet 12
szczegółowych raportów v2, metadane urządzenia, mediana trzech ciepłych
startów, czasy zimne, kontrolę jednakowych liczb trafień, liczbę wykonanych
przebiegów i status. Zapis jest lokalny, w localStorage pod kluczem
`kdj.search-poc.full-suite.v1`. Przerwany/błędny przebieg zachowuje
raporty już wykonane. Starsze raporty v1/v2 pozostają dostępne.

**Ważne:** zimny start oznacza brak lokalnej bazy, nie wyczyszczony cache
HTTP, cache WASM ani świeży profil przeglądarki. Pomiary nie symulują
pierwszego pobrania na zupełnie nowym urządzeniu. Nie zamykaj karty podczas
sekwencji.

Pojedyncze przyciski resetowania, benchmarkowania i ponownego otwarcia
nadal działają niezależnie. Są przydatne do diagnostyki.

### Ręczny test (opcjonalnie)

1. Wybierz A; kliknij Resetuj indeks, uruchom benchmark.
2. Kliknij Zamknij i otwórz ponownie (nowy Web Worker).
3. Powtórz dla B i C; ten tryb zapisuje pojedyncze raporty v2.

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
tnp_missing_vs_ubg. W drzewie projektu TNP obejmuje 47 ksiąg, UBG 66.
**Nie jest to dowód usterki parsera**: 47 ksiąg odpowiada wydaniu
Przekładu toruńskiego z 17.04.2023, opisanemu przez wydawcę jako „Nowe
Przymierze oraz Księgi Mądrościowe, Księgi Pięciu Megilot i Proroków
Mniejszych”. Źródła:

- https://www.swch.pl/tnp/index.html (wydanie 17.04.2023)
- https://odkryjbiblie.pl/porownaj/przeklady/ (47 ksiąg wydania 2022/2023)

Wydania nowsze mogą mieć więcej ksiąg; np. opis wydania drukowanego
z 2025 r. wymienia dodatkowo Rodzaju, Wyjścia i Kapłańską. Nie można
automatycznie uzupełniać brakujących 19 ksiąg tekstem UBG. Przy migracji
źródła trzeba osobno sprawdzić wersję EPUB, aktualność i licencję.

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
