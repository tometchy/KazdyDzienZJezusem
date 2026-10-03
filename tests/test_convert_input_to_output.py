from __future__ import annotations

import importlib.util
import sys
import unittest
from pathlib import Path
from tempfile import TemporaryDirectory
from unittest.mock import patch

SCRIPT_PATH = Path(__file__).resolve().parents[1] / "convert-input-to-output.py"
SPEC = importlib.util.spec_from_file_location("converter_under_test", SCRIPT_PATH)
assert SPEC and SPEC.loader
converter = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = converter
SPEC.loader.exec_module(converter)


class WriteTranslationTests(unittest.TestCase):
    def test_writes_flat_text_mapping_without_embedded_tags(self) -> None:
        with TemporaryDirectory() as directory, patch.object(
            converter, "OUTPUT_DIR", Path(directory) / "input"
        ):
            genesis = next(book for book in converter.BOOKS if book.abbreviation == "Rdz")
            converter.write_translation(
                "TNP",
                [(genesis, 1, 1, "In the beginning"), (genesis, 1, 2, "Second verse")],
            )
            chapter = (Path(directory) / "input" / "TNP" / "Rdz" / "1.yml").read_text()

        self.assertEqual(
            '"Rdz1,1": "In the beginning"\n"Rdz1,2": "Second verse"\n',
            chapter,
        )
        self.assertNotIn("tags:", chapter)
        self.assertNotIn("text:", chapter)


class IgnatiusSourceTests(unittest.TestCase):
    def test_source_contains_all_seven_letters_and_sections(self) -> None:
        sections = list(converter.read_ignatius())
        self.assertEqual(7, len({work.abbreviation for work, _, _, _ in sections}))
        self.assertEqual(205, len(sections))
        self.assertTrue(any(chapter == 0 for _, chapter, _, _ in sections))
        self.assertTrue(all(text for _, _, _, text in sections))


if __name__ == "__main__":
    unittest.main()
