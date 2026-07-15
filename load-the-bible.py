import ijson
import redis
import json
import re
from zipfile import ZipFile
from bs4 import BeautifulSoup

r = redis.Redis(host='localhost', port=6379, decode_responses=True)

BOOK_MAP = {
    "mat": ("Matthew", "Ewangelia Mateusza", 40, "Matt"),
    "mar": ("Mark", "Ewangelia Marka", 41, "Mark"),
    "luk": ("Luke", "Ewangelia Łukasza", 42, "Luke"),
    "jhn": ("John", "Ewangelia Jana", 43, "John"),
    "act": ("Acts", "Dzieje Apostolskie", 44, "Acts"),
    "rom": ("Romans", "List do Rzymian", 45, "Romans"),

    "1co": ("1 Corinthians", "1 List do Koryntian", 46, "1Cor"),
    "2co": ("2 Corinthians", "2 List do Koryntian", 47, "2Cor"),
    "gal": ("Galatians", "List do Galacjan", 48, "Gal"),
    "eph": ("Ephesians", "List do Efezjan", 49, "Eph"),
    "php": ("Philippians", "List do Filipian", 50, "Phil"),
    "col": ("Colossians", "List do Kolosan", 51, "Col"),

    "1th": ("1 Thessalonians", "1 List do Tesaloniczan", 52, "1Thess"),
    "2th": ("2 Thessalonians", "2 List do Tesaloniczan", 53, "2Thess"),

    "1ti": ("1 Timothy", "1 List do Tymoteusza", 54, "1Tim"),
    "2ti": ("2 Timothy", "2 List do Tymoteusza", 55, "2Tim"),

    "tit": ("Titus", "List do Tytusa", 56, "Titus"),
    "phm": ("Philemon", "List do Filemona", 57, "Phlm"),
    "heb": ("Hebrews", "List do Hebrajczyków", 58, "Heb"),
    "jas": ("James", "List Jakuba", 59, "Jas"),

    "1pe": ("1 Peter", "1 List Piotra", 60, "1Pet"),
    "2pe": ("2 Peter", "2 List Piotra", 61, "2Pet"),

    "1jn": ("1 John", "1 List Jana", 62, "1John"),
    "2jn": ("2 John", "2 List Jana", 63, "2John"),
    "3jn": ("3 John", "3 List Jana", 64, "3John"),

    "jud": ("Jude", "List Judy", 65, "Jude"),
    "rev": ("Revelation", "Objawienie Jana", 66, "Rev"),
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

# ---------- TNP ----------
def load_tnp(pipe):
    with ZipFile("data/Biblia_przeklad_Torunski.epub") as z:
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

                file_stem = name.rsplit("/", 1)[-1].rsplit(".", 1)[0]
                book = None
                for _, (book_en, _, _, tnp_file_stem) in BOOK_MAP.items():
                    if tnp_file_stem == file_stem:
                        book = book_en
                        break

                if book is None:
                    continue

                key = f"tnp:{book}:{chapter}:{verse}"
                pipe.set(key, text.strip())

        pipe.execute()
        print("TNP DONE")

# ---------- UBG ----------
def load_ubg(pipe):
    with ZipFile("data/UBG_2025.epub") as z:
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

                book = None
                for _, (book_en, _, mapped_num, _) in BOOK_MAP.items():
                    if mapped_num == book_num:
                        book = book_en
                        break

                if book is None:
                    continue

                key = f"ubg:{book}:{chapter}:{verse}"
                pipe.set(key, text.strip())

        pipe.execute()
        print("UBG DONE")

# ---------- KJV ----------
def load_kjv(pipe):
    with open("data/verses-1769.json", "r") as f:
        data = json.load(f)

    count = 0

    for ref, text in data.items():
        # "John 3:16"
        m = re.match(r"(.+?) (\d+):(\d+)", ref)
        if not m:
            continue

        book, chapter, verse = m.groups()

        key = f"kjv:{book}:{chapter}:{verse}"
        pipe.set(key, text.strip())

        count += 1
        if count % 2000 == 0:
            pipe.execute()

    pipe.execute()
    print("KJV DONE:", count)

# ---------- MAIN ----------
pipe = r.pipeline()

load_tr(pipe)
load_tnp(pipe)
load_ubg(pipe)
load_kjv(pipe)

print("ALL DONE")
