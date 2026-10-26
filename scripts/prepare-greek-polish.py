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
        for greek, polish, grammar in zip(passage["greek"], passage["polish"], passage["grammar"]):
            for morphology in biblehub_morphologies(grammar):
                identities = by_form.get((alignment_key(greek), morphology), set())
                if len(identities) == 1:
                    glosses.setdefault(next(iter(identities)), set()).add((polish, grammar))
    aliases = {}
    for greek, identity in attestations:
        key = form_key(greek)
        if key not in direct_forms and identity in glosses:
            aliases.setdefault(key, set()).update(glosses[identity])
    return [{"form": key, "glosses": [{"polish": polish, "grammar": grammar} for polish, grammar in sorted(values)]}
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
            greek, polish, grammar = row["source_diacritics"], row["target_pl"], row["tag_biblehub"]
            reference = f"{BOOKS[row['book'] - 1]}{row['chapter']},{row['verse']}"
            if len(greek) != len(polish) or len(greek) != len(grammar) or not greek:
                raise ValueError(f"Unaligned source arrays at {reference}")
            if reference in references:
                raise ValueError(f"Duplicate passage: {reference}")
            references.add(reference)
            forms.update(form_key(word) for word in greek)
            passages.append({"reference": reference, "greek": greek, "polish": polish, "grammar": grammar})
    if len(passages) != 7940:
        raise ValueError(f"Unexpected corpus size: {len(passages)}")
    def passage_order(item):
        match = re.fullmatch(r"(.+?)(\d+),(\d+)", item["reference"])
        return BOOKS.index(match[1]), int(match[2]), int(match[3])
    passages.sort(key=passage_order)
    aliases = morphology_aliases(passages, args.gnt, forms)
    metadata = {"schema": 1, "source": SOURCE, "revision": REVISION,
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
