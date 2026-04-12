import ijson
import redis
import json
import re
from zipfile import ZipFile
from bs4 import BeautifulSoup

r = redis.Redis(host='localhost', port=6379, decode_responses=True)

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

                book = None
                for b in ["Matthew","Mark","Luke","John","Acts","Romans","Jude","Revelation"]:
                    if b.lower() in name.lower():
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

                book_map = {
                    40: "Matthew",
                    41: "Mark",
                    42: "Luke",
                    43: "John",
                    44: "Acts",
                    45: "Romans",
                    65: "Jude",
                    66: "Revelation"
                }

                book = book_map.get(book_num)
                if not book:
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
