#!/usr/bin/env python3
"""Build a portable SQLite FTS5 snapshot from the exact browser corpus.

Requires only Python's standard-library sqlite3 with FTS5 enabled.
The SQLite file is in DELETE journal mode, not WAL, before distribution.
"""
import argparse
import gzip
import hashlib
import io
import json
import sqlite3
import unicodedata
from collections import Counter
from pathlib import Path


def normalize(value):
    value = unicodedata.normalize("NFD", str(value or "")).lower()
    value = value.replace("ς", "σ").replace("ł", "l")
    return "".join(char for char in value if unicodedata.category(char) not in ("Mn", "Mc", "Me"))


SCHEMA = """CREATE VIRTUAL TABLE docs_fts USING fts5(
    source, ref UNINDEXED, pl, gr, en, lemma,
    tokenize='unicode61 remove_diacritics 2'
)"""


def build(source_dir):
    manifest_path = source_dir / "corpus-manifest.json"
    corpus = source_dir / "corpus.jsonl.gz"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    if hashlib.sha256(corpus.read_bytes()).hexdigest() != manifest["gzip_sha256"]:
        raise ValueError("Corpus hash mismatch")
    if manifest.get("synthetic") or manifest["records"] < 1:
        raise ValueError("Cannot publish an empty or synthetic corpus")

    database = source_dir / "corpus-fts5.sqlite3"
    database.unlink(missing_ok=True)
    counts = Counter()
    connection = sqlite3.connect(database)
    try:
        connection.execute("PRAGMA page_size=4096")
        connection.execute("PRAGMA journal_mode=DELETE")
        connection.execute("CREATE TABLE meta(key TEXT PRIMARY KEY, value TEXT NOT NULL)")
        connection.execute(SCHEMA)
        with gzip.open(corpus, "rt", encoding="utf-8") as rows:
            with connection:
                for line in rows:
                    record = json.loads(line)
                    source = record["source"]
                    counts[source] += 1
                    connection.execute(
                        "INSERT INTO docs_fts(source,ref,pl,gr,en,lemma) VALUES(?,?,?,?,?,?)",
                        (normalize(source), str(record["ref"]), normalize(record["pl"]),
                         normalize(record["gr"]), normalize(record["en"]), normalize(record["lemma"])),
                    )
                connection.executemany(
                    "INSERT INTO meta(key,value) VALUES(?,?)",
                    (("corpus_sha256", manifest["gzip_sha256"]),
                     ("schema_version", "2"),
                     ("records", str(manifest["records"]))),
                )
        if dict(counts) != manifest["sources"]:
            raise ValueError(f"Source count mismatch: {counts} != {manifest['sources']}")
        if connection.execute("SELECT COUNT(*) FROM docs_fts").fetchone()[0] != manifest["records"]:
            raise ValueError("Database document count mismatch")
        # Check FTS structure as well as the SQLite b-tree.
        connection.execute("INSERT INTO docs_fts(docs_fts) VALUES('integrity-check')")
        connection.execute("INSERT INTO docs_fts(docs_fts) VALUES('optimize')")
        connection.commit()
        connection.execute("VACUUM")
        if connection.execute("PRAGMA integrity_check").fetchone()[0] != "ok":
            raise ValueError("SQLite integrity check failed")
        if connection.execute("PRAGMA journal_mode").fetchone()[0] != "delete":
            raise ValueError("The portable SQLite snapshot must not use WAL")
    finally:
        connection.close()

    compressed_path = source_dir / "corpus-fts5.sqlite3.gz"
    with database.open("rb") as source, compressed_path.open("wb") as output:
        with gzip.GzipFile(fileobj=output, mode="wb", filename="", mtime=0, compresslevel=6) as zipped:
            for chunk in iter(lambda: source.read(1024 * 1024), b""):
                zipped.write(chunk)

    manifest.update({
        "sqlite_schema_version": 2,
        "sqlite_build_libversion": sqlite3.sqlite_version,
        "sqlite_bytes": database.stat().st_size,
        "sqlite_sha256": hashlib.sha256(database.read_bytes()).hexdigest(),
        "sqlite_gzip_bytes": compressed_path.stat().st_size,
        "sqlite_gzip_sha256": hashlib.sha256(compressed_path.read_bytes()).hexdigest(),
        "sqlite_sources": dict(sorted(counts.items())),
    })
    manifest_path.write_text(json.dumps(manifest, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print("Prebuilt SQLite:", manifest["records"], "rows,",
          manifest["sqlite_bytes"], "bytes raw,",
          manifest["sqlite_gzip_bytes"], "bytes gzip")


def main():
    cli = argparse.ArgumentParser()
    cli.add_argument("--out", type=Path, required=True)
    args = cli.parse_args()
    build(args.out)


if __name__ == "__main__":
    main()
