import ijson
import redis
import json
import re
from zipfile import ZipFile
from bs4 import BeautifulSoup

r = redis.Redis(host='localhost', port=6379, decode_responses=True)

BOOK_MAP = {
    "Matthew": 40,
    "Mark": 41,
    "Luke": 42,
    "John": 43,
    "Acts": 44,
    "Romans": 45,
    "1 Corinthians": 46,
    "2 Corinthians": 47,
    "Galatians": 48,
    "Ephesians": 49,
    "Philippians": 50,
    "Colossians": 51,
    "1 Thessalonians": 52,
    "2 Thessalonians": 53,
    "1 Timothy": 54,
    "2 Timothy": 55,
    "Titus": 56,
    "Philemon": 57,
    "Hebrews": 58,
    "James": 59,
    "1 Peter": 60,
    "2 Peter": 61,
    "1 John": 62,
    "2 John": 63,
    "3 John": 64,
    "Jude": 65,
    "Revelation": 66,
}

# ---------- TR ----------
def load_tr(pipe):
    count = 0
    with open('data/gnt.flat.json', 'rb') as f:
        for item in ijson.items(f, 'item'):
            key = f"gnt:{item['book_name_osis']}:{item['chapter']}:{item['verse']}"
            pipe.set(key, json.dumps(item, ensure_ascii=False))
            count += 1
            if count % 1000 == 0:
                pipe.execute()
    pipe.execute()
    print("TR DONE:", count)

# ---------- TNP (NAPRAWIONE) ----------
def load_tnp(pipe):
    with ZipFile("Biblia_przeklad_Torunski.epub") as z:

        for name in z.namelist():

            if not name.endswith(".html") and not name.endswith(".xhtml"):
                continue

            soup = BeautifulSoup(z.read(name).decode(), "html.parser")

            for node in soup.find_all(id=re.compile(r"v\d+\.\d+")):

                vid = node["id"]
                text = node.get_text(" ", strip=True)

                text = text.replace("\xad", "")
                text = re.sub(r"^\d+\.\s*", "", text)
                text = re.sub(r"\s+", " ", text)
                text = re.sub(r"\s+([,;:.])", r"\1", text)

                m = re.match(r"v(\d+)\.(\d+)", vid)
                if not m:
                    continue

                chapter, verse = m.groups()

                # 🔥 zgadujemy księgę z kontekstu (plik)
                book = None
                for b in BOOK_MAP.keys():
                    if b.lower().replace(" ", "") in name.lower():
                        book = b
                        break

                if not book:
                    continue

                key = f"tnp:{book}:{chapter}:{verse}"
                pipe.set(key, text.strip())

        pipe.execute()
        print("TNP DONE")

# ---------- UBG ----------
def load_ubg(pipe):
    with ZipFile("UBG_2025.epub") as z:

        for name in z.namelist():

            if not name.endswith(".xhtml") or "PL-" not in name:
                continue

            soup = BeautifulSoup(z.read(name).decode(), "html.parser")

            for node in soup.find_all(id=re.compile(r"BG-\d+_\d+")):
                vid = node["id"]
                text = node.get_text(" ", strip=True)

                text = re.sub(r"^\d+\.\d+\s*", "", text)
                text = re.sub(r"\s+", " ", text)

                m = re.match(r"BG-(\d+)_(\d+)", vid)
                if not m:
                    continue

                chapter, verse = m.groups()

                book_num = int(name.split("PL-")[1].split(".")[0])
                book = next((k for k, v in BOOK_MAP.items() if v == book_num), None)

                if not book:
                    continue

                key = f"ubg:{book}:{chapter}:{verse}"
                pipe.set(key, text.strip())

        pipe.execute()
        print("UBG DONE")

pipe = r.pipeline()

load_tr(pipe)
load_tnp(pipe)
load_ubg(pipe)

print("ALL DONE")
