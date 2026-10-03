from __future__ import annotations

import importlib.util
import io
import sys
import unittest
from contextlib import redirect_stdout
from pathlib import Path
from tempfile import TemporaryDirectory
from unittest.mock import patch


SCRIPT_PATH = Path(__file__).resolve().parents[1] / "convert-input-to-output.py"
MODULE_NAME = "convert_input_to_output_under_test"
MODULE_SPEC = importlib.util.spec_from_file_location(MODULE_NAME, SCRIPT_PATH)
if MODULE_SPEC is None or MODULE_SPEC.loader is None:
    raise RuntimeError(f"Unable to load converter module from {SCRIPT_PATH}")

converter = importlib.util.module_from_spec(MODULE_SPEC)
sys.modules[MODULE_NAME] = converter
MODULE_SPEC.loader.exec_module(converter)


class WriteTranslationTests(unittest.TestCase):
    def setUp(self) -> None:
        temporary_directory = TemporaryDirectory()
        self.addCleanup(temporary_directory.cleanup)
        self.input_directory = Path(temporary_directory.name) / "input"
        patcher = patch.object(converter, "OUTPUT_DIR", self.input_directory)
        patcher.start()
        self.addCleanup(patcher.stop)
        self.genesis = next(book for book in converter.BOOKS if book.abbreviation == "Rdz")

    def test_regeneration_writes_plain_verse_mapping(self) -> None:
        self.regenerate((1, 1, "In the beginning."), (1, 2, "The earth was empty."))
        self.assertEqual(
            '"Rdz1,1": "In the beginning."\n"Rdz1,2": "The earth was empty."\n',
            self.chapter_path().read_text(encoding="utf-8"),
        )

    def test_regeneration_updates_text_and_keeps_missing_verses(self) -> None:
        path = self.chapter_path()
        path.parent.mkdir(parents=True)
        path.write_text('"Rdz1,1": "Old."\n"Rdz1,2": "Keep me."\n', encoding="utf-8")
        self.regenerate((1, 1, "New."))
        self.assertEqual('"Rdz1,1": "New."\n"Rdz1,2": "Keep me."\n', path.read_text(encoding="utf-8"))

    def chapter_path(self) -> Path:
        return self.input_directory / "TNP" / "Rdz" / "1.yml"

    def regenerate(self, *verses: tuple[int, int, str]) -> None:
        source = [(self.genesis, chapter, verse, text) for chapter, verse, text in verses]
        with redirect_stdout(io.StringIO()):
            converter.write_translation("TNP", source)


class IgnatiusSourceTests(unittest.TestCase):
    def test_source_contains_all_seven_letters_and_sections(self) -> None:
        sections = list(converter.read_ignatius())

        self.assertEqual(7, len({work.abbreviation for work, _, _, _ in sections}))
        self.assertEqual(205, len(sections))
        self.assertTrue(any(chapter == 0 for _, chapter, _, _ in sections))
        self.assertTrue(all(text for _, _, _, text in sections))


if __name__ == "__main__":
    unittest.main()
