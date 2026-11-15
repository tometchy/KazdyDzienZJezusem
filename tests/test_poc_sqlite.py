"""Regression tests for the browser corpus and portable SQLite snapshot.

These fixtures are deliberately small: full real-data checks run during the
build against the repository input/ tree.
"""
import gzip
import hashlib
import json
import sqlite3
import sys
import unittest
from pathlib import Path
from tempfile import TemporaryDirectory

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "poc"))
import prepare
import build_sqlite


def write_gzip(path, rows):
    path.parent.mkdir(parents=True, exist_ok=True)
    with gzip.open(path, "wt", encoding="utf-8") as stream:
        for row in rows:
            stream.write(json.dumps(row, ensure_ascii=False) + "\n")


class PrebuiltSqliteTests(unittest.TestCase):
    def setUp(self):
        temporary = TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        source = self.root / "input"
        for kind, book, text in (
            ("TNP", "J", "Na początku było Słowo"),
            ("UBG", "J", "Na początku było Słowo"),
            ("UBG", "Rdz", "Na początku Bóg stworzył"),
            ("KJV", "J", "In the beginning was the Word"),
            ("TR", "J", "Ἐν ἀρχῇ ἦν ὁ λόγος"),
            ("Fathers", "Ignatius/Ef", "Ἡ ἀγάπη"),
        ):
            path = source / kind / book / "1.yml"
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(json.dumps("J1,1") + ": " + json.dumps(text, ensure_ascii=False) + "\n",
                            encoding="utf-8")
        write_gzip(source / "GreekPolish/interlinear.jsonl.gz", [
            {"schema": 2, "passages": 1, "forms": 2},
            {"reference": "J1,1", "greek": ["λόγος", "ἀγάπη"], "polish": ["słowo", "miłość"],
             "english": ["word", "love"]},
            {"form": "λόγος", "glosses": [{"polish": "słowo", "english": "word",
                                         "lemma": "λόγος"}]},
        ])
        write_gzip(source / "GreekPolish/supplement.jsonl.gz", [
            {"schema": 1, "forms": 1},
            {"form": "θεός", "glosses": [{"polish": None, "english": "God",
                                        "lemma": "θεός"}]},
        ])
        self.out = self.root / "generated"
        self.out.mkdir()

    def generate(self):
        from collections import Counter
        import io
        target = self.out / "corpus.jsonl.gz"
        counts = Counter()
        with target.open("wb") as raw:
            with gzip.GzipFile(fileobj=raw, mode="wb", filename="", mtime=0) as zipped:
                with io.TextIOWrapper(zipped, encoding="utf-8") as output:
                    for index, row in enumerate(prepare.records(self.root), 1):
                        counts[row["source"]] += 1
                        output.write(json.dumps({**row, "id": index}, ensure_ascii=False) + "\n")
        manifest = {
            "records": sum(counts.values()), "sources": dict(sorted(counts.items())),
            "compressed_bytes": target.stat().st_size,
            "gzip_sha256": hashlib.sha256(target.read_bytes()).hexdigest(),
            "synthetic": False, **prepare.book_coverage(self.root)
        }
        (self.out / "corpus-manifest.json").write_text(json.dumps(manifest), encoding="utf-8")
        return manifest

    def test_primary_dictionary_is_indexed_as_slownik(self):
        records = list(prepare.records(self.root))
        dictionary = [r for r in records if r["source"] == "Slownik"]
        self.assertGreaterEqual(len(dictionary), 3)
        self.assertTrue(any(r["gr"] == "λόγος" and "słowo" in r["pl"]
                            for r in dictionary))
        self.assertEqual({"Rdz"}, set(prepare.book_coverage(self.root)["tnp_missing_vs_ubg"]))

    def test_builds_valid_snapshot_with_same_fts_matches(self):
        original = self.generate()
        build_sqlite.build(self.out)
        manifest = json.loads((self.out / "corpus-manifest.json").read_text())
        self.assertEqual(original["records"], manifest["records"])
        self.assertEqual(original["sources"], manifest["sqlite_sources"])
        db_path = self.out / "corpus-fts5.sqlite3"
        zipped = self.out / "corpus-fts5.sqlite3.gz"
        self.assertEqual(gzip.decompress(zipped.read_bytes()), db_path.read_bytes())
        self.assertEqual(manifest["sqlite_sha256"],
                         hashlib.sha256(db_path.read_bytes()).hexdigest())
        with sqlite3.connect(db_path) as db:
            self.assertEqual(db.execute("PRAGMA integrity_check").fetchone()[0], "ok")
            self.assertEqual(db.execute("PRAGMA journal_mode").fetchone()[0], "delete")
            self.assertEqual(db.execute("SELECT COUNT(*) FROM docs_fts").fetchone()[0],
                             manifest["records"])
            self.assertGreater(db.execute(
                "SELECT COUNT(*) FROM docs_fts WHERE docs_fts MATCH ?",
                ("source:slownik AND gr:λογοσ",)).fetchone()[0], 0)
            self.assertEqual(db.execute(
                "SELECT value FROM meta WHERE key='corpus_sha256'").fetchone()[0],
                manifest["gzip_sha256"])

    def test_greek_accent_and_final_sigma_normalization(self):
        self.assertEqual(build_sqlite.normalize("ΛΌΓΟΣ"), "λογοσ")
        self.assertEqual(build_sqlite.normalize("λογος"), "λογοσ")
        self.assertEqual(build_sqlite.normalize("ἀγάπη"), "αγαπη")
        self.assertEqual(build_sqlite.normalize("Łaska"), "laska")

    def test_corrupted_corpus_is_rejected(self):
        self.generate()
        path = self.out / "corpus.jsonl.gz"
        path.write_bytes(path.read_bytes() + b"corruption")
        with self.assertRaisesRegex(ValueError, "hash mismatch"):
            build_sqlite.build(self.out)


if __name__ == "__main__":
    unittest.main()
