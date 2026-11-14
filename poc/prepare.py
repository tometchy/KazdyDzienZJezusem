#!/usr/bin/env python3
"""Generate real, searchable POC records from the repository's input/ files.

Runs only during the Docker image build. No fake/demo records are included.
Standard library only; no network access or Git credentials.
"""
import argparse
import gzip
import hashlib
import json
from collections import Counter
from pathlib import Path

SOURCES = ("TNP", "UBG", "KJV", "TR", "Fathers")


def yaml_lines(path):
    """The repo converter writes one JSON-compatible YAML pair per line."""
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


def records(repo):
    root = repo / "input"
    if not root.is_dir():
        raise FileNotFoundError(root)

    for source in SOURCES:
        for file in sorted((root / source).rglob("*.yml")):
            for reference, body in yaml_lines(file):
                text = str(body)
                yield {
                    "source": "Ojcowie" if source == "Fathers" else source,
                    "ref": (str(file.parent.relative_to(root / source)) + "/" + reference)
                    if source == "Fathers" else reference,
                    "pl": text if source in ("TNP", "UBG") else "",
                    "gr": text if source in ("TR", "Fathers") else "",
                    "en": text if source == "KJV" else "",
                    "lemma": "",
                }

    interlinear = root / "GreekPolish" / "interlinear.jsonl.gz"
    if interlinear.is_file():
        with gzip.open(interlinear, "rt", encoding="utf-8") as stream:
            next(stream)  # metadata
            for line in stream:
                record = json.loads(line)
                if "reference" not in record:
                    continue
                yield {
                    "source": "Interlinear",
                    "ref": record["reference"],
                    "pl": " ".join(record.get("polish", [])),
                    "gr": " ".join(record.get("greek", [])),
                    "en": " ".join(record.get("english", [])),
                    "lemma": "",
                }

    supplement = root / "GreekPolish" / "supplement.jsonl.gz"
    if supplement.is_file():
        with gzip.open(supplement, "rt", encoding="utf-8") as stream:
            next(stream)
            for line in stream:
                record = json.loads(line)
                glosses = record.get("glosses", [])
                yield {
                    "source": "Slownik",
                    "ref": record.get("form", ""),
                    "pl": " ".join(x.get("polish") or "" for x in glosses),
                    "gr": record.get("form", ""),
                    "en": " ".join(x.get("english") or "" for x in glosses),
                    "lemma": " ".join(x.get("lemma") or "" for x in glosses),
                }


def main():
    cli = argparse.ArgumentParser()
    cli.add_argument("--repo", type=Path, default=Path("."))
    cli.add_argument("--out", type=Path, required=True)
    args = cli.parse_args()
    args.out.mkdir(parents=True, exist_ok=True)
    target = args.out / "corpus.jsonl.gz"
    counts = Counter()
    with gzip.open(target, "wt", encoding="utf-8", compresslevel=6) as output:
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
    }
    (args.out / "corpus-manifest.json").write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8"
    )
    print("POC corpus:", manifest["records"], "documents,", target.stat().st_size, "compressed bytes")


if __name__ == "__main__":
    main()
