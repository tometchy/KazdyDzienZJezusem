#!/usr/bin/env python3
"""Prepare the pinned, static Polish interlinear corpus. No runtime downloads."""

import argparse
import gzip
import hashlib
import json
import re
from pathlib import Path
import unicodedata
import urllib.request

REVISION = "4c842b2622915d608482ece200a352779110ddd8"
SOURCE = "https://huggingface.co/datasets/mrapacz/greek-interlinear-translations"
FILES = {
    "train": "dd3f71ab4e672ea9821ac9dfb3a87225e0199262b8f4f84d959bca19ee11f9ba",
    "validation": "bb093fe1a3c0ac82346039ea0ed712709a4d0361d2d318c91bb9409eb4a4a5e3",
    "test": "679eab1312ac9f9d7a27ede685be5f12880c445f1a6912b3101d988046e7aabf",
}
BOOKS = "Mt Mk Łk J Dz Rz 1Kor 2Kor Ga Ef Flp Kol 1Tes 2Tes 1Tm 2Tm Tt Flm Hbr Jk 1P 2P 1J 2J 3J Jud Ob".split()


def form_key(text):
    # Match GreekText.FormKey: preserve breathing/iota subscript, fold grave to acute.
    return unicodedata.normalize("NFC", unicodedata.normalize("NFD", text.lower()).replace("\u0300", "\u0301")).replace("ς", "σ")


def alignment_key(text):
    return "".join(c for c in unicodedata.normalize("NFD", form_key(text)) if unicodedata.category(c) != "Mn")


def greek_word(text):
    return "".join(c for c in text if unicodedata.category(c)[0] in "LM"
                   and ("\u0370" <= c <= "\u03ff" or "\u1f00" <= c <= "\u1fff" or unicodedata.category(c)[0] == "M"))


def ross_morphology(tag):
    parts = tag.split("-")
    if len(parts) == 2 and parts[0] in {"N", "A", "T"} and re.fullmatch(r"[NGDAV][SP][MFN]", parts[1]):
        case, number, gender = parts[1]
        return f"{dict(N='N', A='Adj', T='Art')[parts[0]]}-{case}{gender}{number}"
    if len(parts) == 3 and parts[0] == "V":
        verb = parts[1].lstrip("12")
        if len(verb) != 3:
            return None
        tense, voice, mood = verb
        voice = {"D": "M", "N": "P"}.get(voice, voice)
        if voice not in "AMP" or mood not in "ISOMNP":
            return None
        ending = parts[2]
        if re.fullmatch(r"[NGDAV][SP][MFN]", ending):
            ending = ending[0] + ending[2] + ending[1]
        return f"V-{tense}{mood}{voice}-{ending}"
    return None


def biblehub_morphologies(tag):
    # Only unambiguous grammatical descriptions participate in form aliasing.
    if re.fullmatch(r"(?:N|Adj|Art)-[NGDAV][MFN][SP]", tag):
        return [tag]
    match = re.fullmatch(r"V-([PIFARLX])([ISOMNP])([AMP](?:/[AMP])?)-(\w+)", tag)
    if match:
        return [f"V-{match[1]}{match[2]}{voice}-{match[4]}" for voice in match[3].split("/")]
    return []


def pronunciation(text):
    """Approximate Polish spelling for conventional Erasmian classroom reading.

    Accents, vowel length and silent iota subscripts are not represented.
    Upsilon is approximated by Polish y; aspirates use the usual t/f/ch.
    """
    letters = []
    for character in unicodedata.normalize("NFD", text.lower()):
        if unicodedata.category(character).startswith("M"):
            if letters:
                letters[-1][1].add(character)
        elif unicodedata.category(character).startswith("L"):
            letters.append((character, set()))
    alphabet = dict(zip("αβγδεζηθικλμνξοπρσςτυφχψωϲ", [
        "a", "b", "g", "d", "e", "dz", "e", "t", "i", "k", "l", "m", "n",
        "ks", "o", "p", "r", "s", "s", "t", "y", "f", "ch", "ps", "o", "s"]))
    diphthongs = {"αι": "aj", "ει": "ej", "οι": "oj", "υι": "yj", "ου": "u",
                  "αυ": "au", "ευ": "eu", "ηυ": "eu"}
    output = []
    if letters and letters[0][0] in "αεηιουω" and (
        "\u0314" in letters[0][1] or len(letters) > 1
        and letters[0][0] + letters[1][0] in diphthongs and "\u0314" in letters[1][1]):
        output.append("h")
    index = 0
    while index < len(letters):
        letter, marks = letters[index]
        following = letters[index + 1][0] if index + 1 < len(letters) else ""
        pair = letter + following
        if pair in diphthongs and "\u0308" not in letters[index + 1][1]:
            output.append(diphthongs[pair])
            index += 2
        else:
            output.append("n" if letter == "γ" and following in {"γ", "κ", "χ", "ξ"} else alphabet.get(letter, letter))
            index += 1
    return "".join(output)


def code_index(gnt_path):
    index = {}
    for verse in json.loads(gnt_path.read_text(encoding="utf-8")):
        for word in verse["words"]:
            morphology = ross_morphology(word["grammar"])
            if morphology:
                index.setdefault((alignment_key(greek_word(word["greek"])), morphology), set()).add(word["grammar"])
    return index


def display_code(greek, tag, codes):
    attested = set()
    for morphology in biblehub_morphologies(tag):
        attested.update(codes.get((alignment_key(greek), morphology), set()))
    if attested:
        return " / ".join(sorted(attested))
    nominal = re.fullmatch(r"(N|Adj|Art)-([NGDAV])([MFN])([SP])", tag)
    if nominal:
        return f"{dict(N='N', Adj='A', Art='T')[nominal[1]]}-{nominal[2]}{nominal[4]}{nominal[3]}"
    verb = re.fullmatch(r"V-([PIFARLX])([ISOMNP])([AMP](?:/[AMP])?)(?:-(\w+))?", tag)
    if verb:
        ending = verb[4]
        if ending and re.fullmatch(r"[NGDAV][MFN][SP]", ending):
            ending = ending[0] + ending[2] + ending[1]
        return " / ".join(f"V-{verb[1]}{voice}{verb[2]}" + (f"-{ending}" if ending else "")
                          for voice in verb[3].split("/"))
    pronoun = re.fullmatch(r"(PPro|DPro|RelPro|IPro|RefPro)-([NGDAV])([MFN]?)([123]?)([SP])", tag)
    if pronoun:
        kind = dict(PPro="P", DPro="D", RelPro="R", IPro="I", RefPro="F")[pronoun[1]]
        ending = f"{pronoun[4]}{pronoun[2]}{pronoun[5]}" if pronoun[4] in {"1", "2"} else f"{pronoun[2]}{pronoun[5]}{pronoun[3]}"
        return f"{kind}-{ending}"
    return {"Conj": "CONJ", "Prep": "PREP", "Adv": "ADV", "Prt": "PRT", "Intj": "INJ"}.get(tag, tag)


def morphology_aliases(passages, gnt_path, direct_forms):
    """Connect attested variants through BOTH Strong identity and inflection."""
    by_form = {}
    attestations = []
    for verse in json.loads(gnt_path.read_text(encoding="utf-8")):
        for word in verse["words"]:
            morphology = ross_morphology(word["grammar"])
            greek = greek_word(word["greek"])
            if not morphology or not greek:
                continue
            identity = (word["strong"], morphology)
            by_form.setdefault((alignment_key(greek), morphology), set()).add(identity)
            attestations.append((greek, identity))
    glosses = {}
    for passage in passages:
        for greek, polish, grammar, english in zip(passage["greek"], passage["polish"], passage["grammar"], passage["english"]):
            for morphology in biblehub_morphologies(grammar):
                identities = by_form.get((alignment_key(greek), morphology), set())
                if len(identities) == 1:
                    glosses.setdefault(next(iter(identities)), set()).add((polish, grammar, english))
    aliases = {}
    for greek, identity in attestations:
        key = form_key(greek)
        if key not in direct_forms and identity in glosses:
            aliases.setdefault(key, set()).update(glosses[identity])
    return [{"form": key, "glosses": [{"polish": polish, "grammar": grammar, "english": english}
                                      for polish, grammar, english in sorted(values)]}
            for key, values in sorted(aliases.items())]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--cache", type=Path, default=Path("artifacts/greek-polish-source"))
    parser.add_argument("--output", type=Path, default=Path("input/GreekPolish/interlinear.jsonl.gz"))
    parser.add_argument("--gnt", type=Path, default=Path("input-raw/gnt.flat.json"))
    args = parser.parse_args()
    import pyarrow.parquet as parquet  # Build-time tool only; pip install pyarrow.

    args.cache.mkdir(parents=True, exist_ok=True)
    passages = []
    references = set()
    forms = set()
    for split, checksum in FILES.items():
        filename = f"{split}-00000-of-00001.parquet"
        path = args.cache / filename
        if not path.exists():
            urllib.request.urlretrieve(f"{SOURCE}/resolve/{REVISION}/data/{filename}", path)
        if hashlib.sha256(path.read_bytes()).hexdigest() != checksum:
            raise ValueError(f"Checksum mismatch for {filename}")
        for row in parquet.read_table(path).to_pylist():
            greek, polish, grammar, english = row["source_diacritics"], row["target_pl"], row["tag_biblehub"], row["target_en"]
            reference = f"{BOOKS[row['book'] - 1]}{row['chapter']},{row['verse']}"
            if not (len(greek) == len(polish) == len(grammar) == len(english)) or not greek:
                raise ValueError(f"Unaligned source arrays at {reference}")
            if reference in references:
                raise ValueError(f"Duplicate passage: {reference}")
            references.add(reference)
            forms.update(form_key(word) for word in greek)
            passages.append({"reference": reference, "greek": greek, "polish": polish, "grammar": grammar,
                             "english": [gloss.strip() or "—" for gloss in english]})
    if len(passages) != 7940:
        raise ValueError(f"Unexpected corpus size: {len(passages)}")
    def passage_order(item):
        match = re.fullmatch(r"(.+?)(\d+),(\d+)", item["reference"])
        return BOOKS.index(match[1]), int(match[2]), int(match[3])
    passages.sort(key=passage_order)
    aliases = morphology_aliases(passages, args.gnt, forms)
    codes = code_index(args.gnt)
    for passage in passages:
        passage["codes"] = [display_code(word, grammar, codes) for word, grammar in zip(passage["greek"], passage["grammar"])]
        passage["pronunciation"] = [pronunciation(word) for word in passage["greek"]]
    for alias in aliases:
        for gloss in alias["glosses"]:
            gloss["code"] = display_code(alias["form"], gloss["grammar"], codes)
            gloss["pronunciation"] = pronunciation(alias["form"])
    metadata = {"schema": 2, "source": SOURCE, "revision": REVISION,
                "pronunciation": "Approximate Polish Erasmian classroom reading; no stress or vowel length",
                "creator": "Maciej Rapacz", "license": "CC-BY-NC-SA-4.0",
                "passages": len(passages), "forms": len(forms) + len(aliases), "aliases": len(aliases),
                "morphologySource": str(args.gnt), "morphologySha256": hashlib.sha256(args.gnt.read_bytes()).hexdigest(),
                "words": sum(len(item["greek"]) for item in passages)}
    args.output.parent.mkdir(parents=True, exist_ok=True)
    with args.output.open("wb") as raw, gzip.GzipFile(filename="", fileobj=raw, mode="wb", mtime=0) as compressed:
        for item in [metadata, *passages, *aliases]:
            compressed.write((json.dumps(item, ensure_ascii=False, separators=(",", ":")) + "\n").encode())
    print(json.dumps(metadata, ensure_ascii=False))
    print(f"Created {args.output} ({args.output.stat().st_size} bytes)")


if __name__ == "__main__":
    main()
