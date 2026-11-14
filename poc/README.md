# Test SQLite FTS5 w przeglądarce (/POC)

Po przebudowaniu obrazu i uruchomieniu istniejącego stosu Podman strona jest
dostępna pod adresem `https://kazdydzienzjezusem.pl/POC/` (także `/POC`).

Nie uruchamia serwera npm w produkcji. W trakcie `podman build` osobne,
tymczasowe etapy Python i Node:

1. Czytają **rzeczywiste** pliki z `input/{TNP,UBG,KJV,TR,Fathers}` oraz
   `input/GreekPolish/{interlinear,supplement}.jsonl.gz`.
2. Wytwarzają `corpus.jsonl.gz` i manifest zawierający liczbę dokumentów,
   źródła i SHA-256 korpusu.
3. Budują statyczny frontend Vite z oficjalnym SQLite WASM.
4. Umieszczają całość w `/app/wwwroot/POC/` obrazu ASP.NET Core.
   Node i Python nie trafiają do obrazu uruchomieniowego.

Istniejący backend, Redis, sesje, synchronizacja GitHub i Cloudflare Tunnel nie
są modyfikowane. Wszystkie operacje FTS5 wykonuje przeglądarka w Web Workerze.
Przy pierwszym uruchomieniu buduje indeks. Przy kolejnych próbuje otworzyć
trwałą bazę OPFS. Gdy OPFS jest niedostępny, jawnie raportuje „tylko RAM”.

## Raporty

„Uruchom benchmark” mierzy 10 gotowych zapytań, po 20 powtórzeń każdego.
`p50` to mediana, `p95` to 95. percentyl. Wyniki (z nazwą silnika, datą,
UA, liczbą rekordów, hash korpusu, trybem storage, czasem indeksowania)
zostają w `localStorage` tego urządzenia (do ośmiu raportów). Przycisk
„Kopiuj raport” kopiuje JSON do schowka; można też pobrać plik.
Brak konta i tokenów GitHub, raporty nie opuszczają przeglądarki.

Dane w localStorage i OPFS są przypisane do konkretnego originu (domeny)
i mogą zostać usunięte przez przeglądarkę.

## Ograniczenia

- To pomiar SQLite WASM/FTS5, **nie** gotowa produkcyjna wyszukiwarka z parserem
  `mat:`, lematyzacją i łączeniem greki z polskimi wersetami.
- Jeden dokument to jeden rekord ze źródła; TR/TNP/UBG/KJV i interlinearne
  dane są osobnymi rekordami.
- Greka jest normalizowana (bez diakrytyków; końcowa sigma ς → σ);
  nie oznacza to automatycznego wyszukania wszystkich form fleksyjnych.
- Przy pierwszym uruchomieniu transfer i indeksowanie pełnego korpusu mogą
  być kosztowne na Pixelu z 4 GB RAM; należy mierzyć i zimny, i ciepły start.
- W razie zmiany SHA korpusu stary indeks jest odbudowywany.
- OPFS wymaga HTTPS i izolacji COOP/COEP. Nagłówki te ASP.NET dodaje tylko dla /POC.
- POC używa danych przeznaczonych do publikowania w obecnej witrynie. Przed
  dodaniem nowych korpusów należy sprawdzić warunki ich licencji.

## Ręczny build frontendu w trybie developerskim

```bash
python3 poc/prepare.py --repo . --out poc/frontend/public
cd poc/frontend
npm install
npm run build
```

Do produkcji używaj istniejącej komendy `./podman-up.sh`.
