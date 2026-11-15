#!/usr/bin/env python3
"""Export the real repository corpus for both browser-built and prebuilt FTS5.

Build-time only. Preserve the source content and its provenance; no artificial
records are introduced. The browser and Python builder share the same JSONL.
"""
import argparse
import gzip
import hashlib
import io
import json
from collections import Counter
from pathlib import Path

SOURCES = ("TNP", "UBG", "KJV", "TR", "Fathers")


def yaml_lines(path):
    """The repository converter writes one JSON-compatible YAML pair per line."""
    with path.open(encoding="utf-8") as stream:
        for number, line in enumerate(stream, 1):
            line = line.strip()
            if not line or line.startswith("#"):
                continue
            try:
                data = json.loads("{" + line.rstrip(",") + "}")
                if len(data) != 1:
                    raise ValueError("Expected one key")
                yield next(iter(data.items()))
            except (ValueError, json.JSONDecodeError) as exc:
                raise ValueError(f"{path}:{number}: invalid converter output: {exc}") from exc


def dictionary_record(form, glosses):
    """Index inflected Greek *forms*, not just the supplementary dictionary."""
    if not form:
        raise ValueError("Empty Greek dictionary form")
    valid = lambda v: v not in (None, "", "-", "—")
    return {
        "source": "Slownik", "ref": form, "pl": " ".join(
            str(x["polish"]) for x in glosses if valid(x.get("polish"))),
        "gr": form,
        "en": " ".join(str(x["english"]) for x in glosses if valid(x.get("english"))),
        "lemma": " ".join(str(x["lemma"]) for x in glosses if valid(x.get("lemma"))),
    }


def records(repo):
    root = repo / "input"
    if not root.is_dir():
        raise FileNotFoundError(root)

    for source in SOURCES:
        for file in sorted((root / source).rglob("*.yml")):
            for reference, body in yaml_lines(file):
                value = str(body)
                yield {
                    "source": "Ojcowie" if source == "Fathers" else source,
                    "ref": (str(file.parent.relative_to(root / source)) + "/" + reference)
                    if source == "Fathers" else reference,
                    "pl": value if source in ("TNP", "UBG") else "",
                    "gr": value if source in ("TR", "Fathers") else "",
                    "en": value if source == "KJV" else "",
                    "lemma": "",
                }

    # The primary interlinear JSONL includes 7,940 passage records AND thousands
    # of inflected Greek forms. The old POC discarded those forms and therefore
    # indexed only the 2,831 supplementary dictionary entries.
    interlinear = root / "GreekPolish" / "interlinear.jsonl.gz"
    primary_forms = {}
    if interlinear.is_file():
        with gzip.open(interlinear, "rt", encoding="utf-8") as stream:
            metadata = json.loads(next(stream))
            if metadata.get("schema") != 2:
                raise ValueError("Unexpected interlinear schema")
            for line in stream:
                record = json.loads(line)
                if "form" in record:
                    key = record["form"]
                    # Alias rows may follow passages for the same spelling.
                    # Combine their glosses; deduplication happens on export.
                    primary_forms.setdefault(key, []).extend(record["glosses"])
                elif "reference" in record:
                    greek = record.get("greek", [])
                    polish = record.get("polish", [])
                    english = record.get("english", [])
                    if not (len(greek) == len(polish) == len(english)):
                        raise ValueError(f"Unaligned interlinear passage {record['reference']}")
                    yield {
                        "source": "Interlinear", "ref": record["reference"],
                        "pl": " ".join(polish), "gr": " ".join(greek),
                        "en": " ".join(english), "lemma": "",
                    }
                    # Most form entries in the main dataset are derived from
                    # passage alignments, not separate "form" JSONL records.
                    for form, pl, en in zip(greek, polish, english):
                        key = form
                        primary_forms.setdefault(key, []).append(
                            {"polish": pl, "english": en})
                else:
                    raise ValueError("Unknown interlinear record type")
        # Preserve source spelling in the reference and use a bounded, stable
        # selection of glosses per form for this *search* POC.
        for form, glosses in sorted(primary_forms.items()):
            distinct = []
            seen = set()
            for entry in glosses:
                signature = (entry.get("polish"), entry.get("english"), entry.get("lemma"))
                if signature not in seen:
                    seen.add(signature)
                    distinct.append(entry)
                if len(distinct) == 12:
                    break
            yield dictionary_record(form, distinct)

    supplement = root / "GreekPolish" / "supplement.jsonl.gz"
    if supplement.is_file():
        with gzip.open(supplement, "rt", encoding="utf-8") as stream:
            metadata = json.loads(next(stream))
            if metadata.get("schema") != 1:
                raise ValueError("Unexpected dictionary supplement schema")
            for line in stream:
                record = json.loads(line)
                if "form" not in record or "glosses" not in record:
                    raise ValueError("Unexpected supplementary dictionary row")
                yield dictionary_record(record["form"], record["glosses"])


def book_coverage(repo):
    root = repo / "input"
    books = {
        source: sorted({path.parent.name for path in (root / source).rglob("*.yml")})
        for source in ("TNP", "UBG", "KJV", "TR")
    }
    return {
        "books_by_source": {source: len(names) for source, names in books.items()},
        "tnp_missing_vs_ubg": sorted(set(books["UBG"]) - set(books["TNP"])),
    }


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo", type=Path, default=Path("."))
    parser.add_argument("--out", type=Path, required=True)
    args = parser.parse_args()
    args.out.mkdir(parents=True, exist_ok=True)
    target = args.out / "corpus.jsonl.gz"
    counts = Counter()
    # gzip header mtime=0 so the same source creates reproducible bytes.
    with target.open("wb") as raw:
        with gzip.GzipFile(fileobj=raw, mode="wb", filename="", mtime=0, compresslevel=6) as compressed:
            with io.TextIOWrapper(compressed, encoding="utf-8") as output:
                for number, row in enumerate(records(args.repo), 1):
                    row["id"] = number
                    counts[row["source"]] += 1
                    output.write(json.dumps(row, ensure_ascii=False, separators=(",", ":")) + "\n")
    if not sum(counts.values()):
        raise RuntimeError("POC corpus is empty")
    manifest = {
        "records": sum(counts.values()),
        "sources": dict(sorted(counts.items())),
        "compressed_bytes": target.stat().st_size,
        "gzip_sha256": hashlib.sha256(target.read_bytes()).hexdigest(),
        "synthetic": False,
        **book_coverage(args.repo),
    }
    (args.out / "corpus-manifest.json").write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8"
    )
    print("POC corpus:", manifest["records"], "documents,",
          target.stat().st_size, "compressed bytes")


if __name__ == "__main__":
    main()
