#!/usr/bin/env python3

import json
import re
import unicodedata
from pathlib import Path
from zipfile import ZipFile
from bs4 import BeautifulSoup

WIP_FILE = "/home/tom/RefLite/Zbawienie/Strona/Wip.md"
TR_DATA = "/home/tom/Projects/textus-receptus/data/gnt.flat.json"
TNP_EPUB = "/home/tom/RefLite/Zbawienie/Biblia/Biblia przeklad Torunski.epub"
UBG_EPUB = "/home/tom/RefLite/Zbawienie/Biblia/UBG_2025.epub"

OUT_BIBLE = Path("/home/tom/RefLite/Index/Biblia")
OUT_GREEK = Path("/home/tom/RefLite/Index/Graeca")

OUT_BIBLE.mkdir(parents=True, exist_ok=True)
OUT_GREEK.mkdir(parents=True, exist_ok=True)


BOOK_MAP = {
    "mat": ("Matthew", "Ewangelia Mateusza", 40, "Ewangelia-Mateusza", "Matt.html"),
    "mar": ("Mark", "Ewangelia Marka", 41, "Ewangelia-Marka", "Mark.html"),
    "luk": ("Luke", "Ewangelia Łukasza", 42, "Ewangelia-Lukasza", "Luke.html"),
    "jhn": ("John", "Ewangelia Jana", 43, "Ewangelia-Jana", "John.html"),
    "act": ("Acts", "Dzieje Apostolskie", 44, "Dzieje-Apostolskie", "Acts.html"),
    "rom": ("Romans", "List do Rzymian", 45, "List-do-Rzymian", "Rom.html"),

    "1co": ("1 Corinthians", "1 List do Koryntian", 46, "1-List-do-Koryntian", "1Cor.html"),
    "2co": ("2 Corinthians", "2 List do Koryntian", 47, "2-List-do-Koryntian", "2Cor.html"),
    "gal": ("Galatians", "List do Galacjan", 48, "List-do-Galacjan", "Gal.html"),
    "eph": ("Ephesians", "List do Efezjan", 49, "List-do-Efezjan", "Eph.html"),
    "php": ("Philippians", "List do Filipian", 50, "List-do-Filipian", "Phil.html"),
    "col": ("Colossians", "List do Kolosan", 51, "List-do-Kolosan", "Col.html"),

    "1th": ("1 Thessalonians", "1 List do Tesaloniczan", 52, "1-List-do-Tesaloniczan", "1Thess.html"),
    "2th": ("2 Thessalonians", "2 List do Tesaloniczan", 53, "2-List-do-Tesaloniczan", "2Thess.html"),

    "1ti": ("1 Timothy", "1 List do Tymoteusza", 54, "1-List-do-Tymoteusza", "1Tim.html"),
    "2ti": ("2 Timothy", "2 List do Tymoteusza", 55, "2-List-do-Tymoteusza", "2Tim.html"),

    "tit": ("Titus", "List do Tytusa", 56, "List-do-Tytusa", "Titus.html"),
    "phm": ("Philemon", "List do Filemona", 57, "List-do-Filemona", "Phlm.html"),
    "heb": ("Hebrews", "List do Hebrajczyków", 58, "List-do-Hebrajczykow", "Heb.html"),
    "jas": ("James", "List Jakuba", 59, "List-Jakuba", "Jas.html"),

    "1pe": ("1 Peter", "1 List Piotra", 60, "1-List-Piotra", "1Pet.html"),
    "2pe": ("2 Peter", "2 List Piotra", 61, "2-List-Piotra", "2Pet.html"),

    "1jn": ("1 John", "1 List Jana", 62, "1-List-Jana", "1John.html"),
    "2jn": ("2 John", "2 List Jana", 63, "2-List-Jana", "2John.html"),
    "3jn": ("3 John", "3 List Jana", 64, "3-List-Jana", "3John.html"),

    "jud": ("Jude", "List Judy", 65, "List-Judy", "Jude.html"),
    "rev": ("Revelation", "Objawienie Jana", 66, "Objawienie-Jana", "Rev.html"),
}


def normalize(text):
    return unicodedata.normalize("NFC", text)


def clean_word(word):
    word = normalize(word)
    return re.sub(r"[.,;·]", "", word)


def read_wip():
    return Path(WIP_FILE).read_text()


def parse_reference(text):
    return re.search(r'blueletterbible\.org/kjv/(\w+)/(\d+)/(\d+)/s_(\d+)', text).groups()


def clean_kjv(text):
    text = re.sub(r'\[G\d+\]\(.*?\)', '', text)
    return re.sub(r'\s+', ' ', text).strip()


def get_kjv(text):
    return clean_kjv(re.search(r'\)\s*-\s*(.+)', text).group(1))


def load_tr():
    with open(TR_DATA) as f:
        return json.load(f)


def build_verse_index(data):
    return {(v["book_name"], v["chapter"], v["verse"]): v for v in data}


def build_lemma_index(data):
    index = {}
    for v in data:
        for w in v["words"]:
            lemma = normalize(w["dictionary_form"])
            if lemma not in index or w["grammar"].endswith("NSM"):
                index[lemma] = w
    return index


def table(word, lemma_link=True):
    lemma = clean_word(word["dictionary_form"])
    greek = clean_word(word["greek"])
    lemma_val = f"[[{lemma}]]" if lemma_link else lemma

    return f"""| greek | {greek} |
|---|---|
| transliteration | {word['transliteration']} |
| strong | {word['strong']} |
| grammar | {word['grammar']} |
| grammar_human | {word['grammar_human']} |
| dictionary_form | {lemma_val} |
| definition | {word['definition']} |
"""


def write_word_file(word, lemma_index):
    form = clean_word(word["greek"])
    lemma = clean_word(word["dictionary_form"])

    (OUT_GREEK / f"{form}.md").write_text(table(word, True))

    if lemma in lemma_index:
        rec = lemma_index[lemma].copy()
        rec["greek"] = lemma
        (OUT_GREEK / f"{lemma}.md").write_text(table(rec, False))


def load_tnp(path):

    cache = {}

    with ZipFile(path) as z:

        for code, (book_en, _, _, _, file_name) in BOOK_MAP.items():

            if file_name not in z.namelist():
                continue

            soup = BeautifulSoup(z.read(file_name).decode(), "html.parser")

            verses = {}

            for node in soup.find_all(id=re.compile(r"v\d+\.\d+")):

                vid = node["id"]
                text = node.get_text(" ", strip=True)

                text = text.replace("\xad", "")
                text = re.sub(r"\s+[A-Z][a-z]{1,3}\s\d+,\d+.*$", "", text)
                text = re.sub(r"\*+", "", text)
                text = re.sub(r"^\d+\.\s*", "", text)
                text = re.sub(r"\s+", " ", text)
                text = re.sub(r"\s+([,;:.])", r"\1", text)

                verses[vid] = text.strip()

            cache[book_en] = verses

    return cache


def fetch_tnp(cache, book, chapter, verse):
    return cache.get(book, {}).get(f"v{chapter}.{verse}", "TNP NOT FOUND")


def load_ubg(path):

    cache = {}

    with ZipFile(path) as z:

        for name in z.namelist():

            if not name.endswith(".xhtml") or "PL-" not in name:
                continue

            soup = BeautifulSoup(z.read(name).decode(), "html.parser")

            verses = {}

            for node in soup.find_all(id=re.compile(r"BG-\d+_\d+")):
                vid = node["id"]
                text = node.get_text(" ", strip=True)

                text = re.sub(r"^\d+\.\d+\s*", "", text)
                text = re.sub(r"\s+", " ", text)

                verses[vid] = text.strip()

            cache[name] = verses

    return cache


def fetch_ubg(cache, book_num, chapter, verse):
    return cache.get(f"OEBPS/Text/PL-{book_num}.xhtml", {}).get(
        f"BG-{chapter}_{verse}", "UBG NOT FOUND"
    )


def main():

    tnp_cache = load_tnp(TNP_EPUB)
    ubg_cache = load_ubg(UBG_EPUB)

    wip = read_wip()

    book_code, chapter, verse, verse_id = parse_reference(wip)

    book_en, book_pl, book_num, slug, _ = BOOK_MAP[book_code]

    kjv = get_kjv(wip)

    tr_data = load_tr()
    verse_data = build_verse_index(tr_data)[(book_en, int(chapter), int(verse))]
    lemma_index = build_lemma_index(tr_data)

    tr_text = " ".join(
        f"[[{clean_word(w['greek'])}]]"
        for w in verse_data["words"]
        if not write_word_file(w, lemma_index)
    )

    tnp_text = fetch_tnp(tnp_cache, book_en, chapter, verse)
    ubg_text = fetch_ubg(ubg_cache, book_num, chapter, verse)

    content = f"""[TR](https://www.blueletterbible.org/kjv/{book_code}/{chapter}/{verse}/t_conc_{verse_id})
> {tr_text}

[KJV](https://www.blueletterbible.org/kjv/{book_code}/{chapter}/{verse}/s_{verse_id})
> {kjv}

[TNP](https://biblia-online.pl/Biblia/PrzekladTorunski/{slug}/{chapter}/{verse})
> {tnp_text}

[UBG](https://biblia-online.pl/Biblia/UwspolczesnionaBibliaGdanska/{slug}/{chapter}/{verse})
> {ubg_text}
"""

    filename = f"{book_pl} {chapter},{verse}.md"
    (OUT_BIBLE / filename).write_text(content)

    print("Zapisano:", filename)


if __name__ == "__main__":
    main()
