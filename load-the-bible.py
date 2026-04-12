import ijson
import redis
import json
import re
from zipfile import ZipFile
from bs4 import BeautifulSoup

r = redis.Redis(host='localhost', port=6379, decode_responses=True)

BOOK_MAP = {
    "mat": ("Matthew", 40, "Matt.html"),
    "mar": ("Mark", 41, "Mark.html"),
    "luk": ("Luke", 42, "Luke.html"),
    "jhn": ("John", 43, "John.html"),
    "act": ("Acts", 44, "Acts.html"),
    "rom": ("Romans", 45, "Rom.html"),

    "1co": ("1 Corinthians", 46, "1Cor.html"),
    "2co": ("2 Corinthians", 47, "2Cor.html"),
    "gal": ("Galatians", 48, "Gal.html"),
    "eph": ("Ephesians", 49, "Eph.html"),
    "php": ("Philippians", 50, "Phil.html"),
    "col": ("Colossians", 51, "Col.html"),

    "1th": ("1 Thessalonians", 52, "1Thess.html"),
    "2th": ("2 Thessalonians", 53, "2Thess.html"),

    "1ti": ("1 Timothy", 54, "1Tim.html"),
    "2ti": ("2 Timothy", 55, "2Tim.html"),

    "tit": ("Titus", 56, "Titus.html"),
    "phm": ("Philemon", 57, "Phlm.html"),
    "heb": ("Hebrews", 58, "Heb.html"),
    "jas": ("James", 59, "Jas.html"),

    "1pe": ("1 Peter", 60, "1Pet.html"),
    "2pe": ("2 Peter", 61, "2Pet.html"),

    "1jn": ("1 John", 62, "1John.html"),
    "2jn": ("2 John", 63, "2John.html"),
    "3jn": ("3 John", 64, "3John.html"),

    "jud": ("Jude", 65, "Jude.html"),
    "rev": ("Revelation", 66, "Rev.html"),
}

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

def load_tnp(pipe):
    with ZipFile("Biblia przeklad Torunski.epub") as z:
        for code, (book_en, _, file_name) in BOOK_MAP.items():
            if file_name not in z.namelist():
                continue

            soup = BeautifulSoup(z.read(file_name).decode(), "html.parser")

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
                key = f"tnp:{book_en}:{chapter}:{verse}"

                pipe.set(key, text.strip())

        pipe.execute()

def load_ubg(pipe):
    with ZipFile("UBG_2025.epub") as z:
        for code, (book_en, book_num, _) in BOOK_MAP.items():

            fname = f"OEBPS/Text/PL-{book_num}.xhtml"
            if fname not in z.namelist():
                continue

            soup = BeautifulSoup(z.read(fname).decode(), "html.parser")

            for node in soup.find_all(id=re.compile(r"BG-\d+_\d+")):
                vid = node["id"]
                text = node.get_text(" ", strip=True)

                text = re.sub(r"^\d+\.\d+\s*", "", text)
                text = re.sub(r"\s+", " ", text)

                m = re.match(r"BG-(\d+)_(\d+)", vid)
                if not m:
                    continue

                chapter, verse = m.groups()
                key = f"ubg:{book_en}:{chapter}:{verse}"

                pipe.set(key, text.strip())

        pipe.execute()

pipe = r.pipeline()

load_tr(pipe)
load_tnp(pipe)
load_ubg(pipe)

print("ALL DONE")
