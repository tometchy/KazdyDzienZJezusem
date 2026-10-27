#!/usr/bin/env python3
"""Prepare offline supplements; never translate a lemma into an inflected gloss."""
import argparse
import csv
import gzip
import hashlib
import importlib.util
import json
from pathlib import Path
import re
import unicodedata
import urllib.request

spec = importlib.util.spec_from_file_location("primary", Path(__file__).with_name("prepare-greek-polish.py"))
primary = importlib.util.module_from_spec(spec)
spec.loader.exec_module(primary)
spec = importlib.util.spec_from_file_location("perseus", Path(__file__).with_name("greek-perseus.py"))
perseus = importlib.util.module_from_spec(spec)
spec.loader.exec_module(perseus)

MACULA_REVISION = "8423afe47b9e8f24b7772e808af45c7159a6fe7e"
SOURCES = {
    "perseus-diogenes-3514648.tar.xz": (
        "https://raw.githubusercontent.com/pjheslin/diogenes-prebuilt-data/3514648bbd76757d93bae1faec4710763f90fca0/prebuilt-data.tar.xz",
        "08e4d817bd2f99f95673ec44b142061e8cc140a73e672bc3b5b9ae6c134cc1a4"),
    "macula-n1904.tsv": (
        f"https://raw.githubusercontent.com/Clear-Bible/macula-greek/{MACULA_REVISION}/Nestle1904/tsv/macula-greek-Nestle1904.tsv",
        "c7cf33b8273c0f549e3582d1610603c86210c18e1f245bb80a69281622385fa5"),
    "kaikki-en-grc.jsonl.gz": (
        "https://kaikki.org/dictionary/Ancient%20Greek/kaikki.org-dictionary-AncientGreek.jsonl.gz",
        "ad127c5656f742947e1a35d8aeefb4fe5b01552a839ef585248d79e1c8e02d93"),
    "kaikki-pl-grc.jsonl": (
        "https://kaikki.org/plwiktionary/j%C4%99zyk%20starogrecki/kaikki.org-dictionary-j%C4%99zykstarogrecki.jsonl",
        "41159682c0a48c984bc78905a657d8b7521b83b7573815c9f65bf180c4149faf"),
}
GREEK_LETTERS = "".join(chr(point) for start, stop in [(0x370, 0x400), (0x1f00, 0x2000)]
                       for point in range(start, stop) if unicodedata.category(chr(point)).startswith("L"))
TOKEN = re.compile(f"[{GREEK_LETTERS}][{GREEK_LETTERS}\u0300-\u036f]*")


def dictionary_key(word):
    # Wiktionary marks vowel length editorially. Preserve breathing, accent,
    # diaeresis, and iota subscript when linking an attested form.
    return primary.form_key("".join(c for c in unicodedata.normalize("NFD", word)
                                    if c not in "\u0304\u0306"))


def accent_key(word):
    return "".join(c for c in unicodedata.normalize("NFD", dictionary_key(word))
                   if c not in "\u0300\u0301\u0342")


def valid_word(word):
    return bool(word) and all(("\u0370" <= c <= "\u03ff" or "\u1f00" <= c <= "\u1fff"
                              or unicodedata.category(c).startswith("M"))
                             and unicodedata.category(c)[0] in "LM" for c in word)


def morphology(pos, tags):
    cases = {"nominative": "N", "genitive": "G", "dative": "D", "accusative": "A", "vocative": "V"}
    numbers = {"singular": "S", "plural": "P", "dual": "D"}
    genders = {"masculine": "M", "feminine": "F", "neuter": "N"}
    case = [value for name, value in cases.items() if name in tags]
    number = [value for name, value in numbers.items() if name in tags]
    gender = [value for name, value in genders.items() if name in tags]
    kind = {"noun": "N", "name": "N", "adj": "A", "pron": "P", "article": "T"}.get(pos)
    if kind and len(case) == len(number) == len(gender) == 1:
        return f"{kind}-{case[0]}{number[0]}{gender[0]}"
    return {"adv": "ADV", "prep": "PREP", "conj": "CONJ", "particle": "PRT", "intj": "INJ"}.get(pos)


def definitions(entry):
    result = []
    for sense in entry.get("senses", []):
        if sense.get("form_of") or sense.get("alt_of") or "form-of" in sense.get("tags", []):
            continue
        for gloss in sense.get("glosses", []):
            gloss = re.sub(r"\s+", " ", gloss).strip()
            if gloss and len(gloss) <= 160 and gloss not in result:
                result.append(gloss)
    return result[:3]


def read_wiktionary(path, wanted):
    result = {}
    opener = gzip.open if path.suffix == ".gz" else open
    with opener(path, "rt", encoding="utf-8") as source:
        for line in source:
            entry = json.loads(line)
            glosses = definitions(entry)
            if not glosses:
                continue
            lemma = unicodedata.normalize("NFC", "".join(c for c in unicodedata.normalize("NFD", entry["word"])
                                                        if c not in "\u0304\u0306"))
            # The table is an attestation of a form, not a generated guess.
            forms = [(entry["word"], entry.get("tags", []))]
            forms += [(row["form"], row.get("tags", []) + entry.get("tags", []))
                      for row in entry.get("forms", [])]
            for form, tags in forms:
                if not valid_word(form) or any(tag in tags for tag in ["romanization", "error-unrecognized-form"]):
                    continue
                key = accent_key(form)
                if key not in wanted:
                    continue
                value = (dictionary_key(form), lemma, " / ".join(glosses), morphology(entry["pos"], tags))
                result.setdefault(key, set()).add(value)
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--cache", type=Path, default=Path("artifacts/greek-supplement-source"))
    parser.add_argument("--output", type=Path, default=Path("input/GreekPolish/supplement.jsonl.gz"))
    args = parser.parse_args()
    args.cache.mkdir(parents=True, exist_ok=True)
    for filename, (url, checksum) in SOURCES.items():
        path = args.cache / filename
        if not path.exists():
            urllib.request.urlretrieve(url, path)
        if hashlib.sha256(path.read_bytes()).hexdigest() != checksum:
            raise ValueError(f"Snapshot changed: {filename}. Review and pin a new checksum before regenerating.")

    # Keep runtime memory bounded: include attested vocabulary in the shipped
    # Greek texts. Regenerate whenever adding new Greek literature.
    wanted = set()
    for path in sorted(Path("input").rglob("*.yml")):
        wanted.update(primary.form_key(word) for word in TOKEN.findall(path.read_text(encoding="utf-8")) if valid_word(word))
    # Include the original GNT spellings, including passage punctuation variants.
    for verse in json.loads(Path("input-raw/gnt.flat.json").read_text(encoding="utf-8")):
        wanted.update(primary.form_key(primary.greek_word(word["greek"])) for word in verse["words"])
    primary_forms = set()
    with gzip.open("input/GreekPolish/interlinear.jsonl.gz", "rt", encoding="utf-8") as source:
        primary_metadata = json.loads(next(source))
        for line in source:
            row = json.loads(line)
            primary_forms.update([row["form"]] if "form" in row else map(primary.form_key, row["greek"]))
    entries = {}
    # Use real word-level, inflected English first. Existing Polish corpus wins.
    with (args.cache / "macula-n1904.tsv").open(encoding="utf-8") as source:
        for row in csv.DictReader(source, delimiter="\t"):
            for word in {row["text"], row["normalized"]}:
                key = primary.form_key(primary.greek_word(word))
                if not key or key in primary_forms:
                    continue
                gloss = row["gloss"].strip() or "—"
                item = (gloss, row["morph"] or None, row["lemma"], "gloss", "MACULA")
                entries.setdefault(key, set()).add(item)
    gnt_path = Path("input-raw/gnt.flat.json")
    if hashlib.sha256(gnt_path.read_bytes()).hexdigest() != primary_metadata["morphologySha256"]:
        raise ValueError("The GNT source changed; regenerate and review the primary corpus first.")
    # The repository's original GNT supplies definitions for its exact word
    # spellings. Label these as dictionary meanings, never as word translations.
    gnt_fallback = {}
    for verse in json.loads(gnt_path.read_text(encoding="utf-8")):
        for word in verse["words"]:
            key = primary.form_key(primary.greek_word(word["greek"]))
            gloss = word.get("definition", "").strip()
            if key and key not in primary_forms and key not in entries and gloss:
                gnt_fallback.setdefault(key, set()).add((gloss, word["grammar"], word["dictionary_form"], "dictionary", "Bundled GNT"))
    entries.update(gnt_fallback)
    remaining = wanted - primary_forms - entries.keys()
    english = read_wiktionary(args.cache / "kaikki-en-grc.jsonl.gz", {accent_key(word) for word in remaining})
    polish = read_wiktionary(args.cache / "kaikki-pl-grc.jsonl", {accent_key(word) for word in wanted - primary_forms})
    for word in sorted(remaining):
        candidates = english.get(accent_key(word), set())
        exact = {entry for entry in candidates if entry[0] == dictionary_key(word)}
        for _, lemma, gloss, code in sorted(exact or candidates, key=lambda item: tuple(value or "" for value in item)):
            entries.setdefault(word, set()).add((gloss, code, lemma, "dictionary", "Wiktionary"))
    entries.update(perseus.read_perseus(args.cache / "perseus-diogenes-3514648.tar.xz",
                                       wanted - primary_forms - entries.keys()))
    rows = []
    for word, values in sorted(entries.items()):
        glosses = []
        for english_gloss, code, lemma, kind, source in sorted(values, key=lambda item: tuple(value or "" for value in item)):
            # Polish dictionary lemmas are NOT inflected translations. Only
            # uninflected parts of speech may supplement the Polish layer.
            polish_glosses = {gloss for _, pl_lemma, gloss, pl_code in polish.get(accent_key(word), set())
                             if dictionary_key(pl_lemma) == dictionary_key(lemma)
                             and pl_code == code and code in {"ADV", "PREP", "CONJ", "PRT", "INJ"}}
            glosses.append({"polish": " / ".join(sorted(polish_glosses)) or None,
                            "grammar": code or "", "english": english_gloss,
                            "code": code, "pronunciation": primary.pronunciation(word),
                            "englishKind": kind, "lemma": lemma, "source": source})
        rows.append({"form": word, "glosses": glosses})
    metadata = {"schema": 1, "forms": len(rows), "maculaRevision": MACULA_REVISION,
                "gntSha256": primary_metadata["morphologySha256"],
                "sources": {name: {"url": url, "sha256": checksum} for name, (url, checksum) in SOURCES.items()},
                "vocabulary": len(wanted), "primaryCovered": len(wanted & primary_forms),
                "covered": len(wanted & (primary_forms | entries.keys())),
                "missing": sorted(wanted - primary_forms - entries.keys())}
    args.output.parent.mkdir(parents=True, exist_ok=True)
    with args.output.open("wb") as raw, gzip.GzipFile(filename="", fileobj=raw, mode="wb", mtime=0) as target:
        for row in [metadata, *rows]:
            target.write((json.dumps(row, ensure_ascii=False, separators=(",", ":")) + "\n").encode())
    print(json.dumps({k: v for k, v in metadata.items() if k not in {"sources", "missing"}}, ensure_ascii=False))
    print(f"Missing forms ({len(metadata['missing'])}): {' '.join(metadata['missing'][:35])}")


if __name__ == "__main__":
    main()
