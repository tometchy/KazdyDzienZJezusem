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
        self.output_directory = Path(temporary_directory.name) / "output"

        output_directory_patch = patch.object(
            converter,
            "OUTPUT_DIR",
            self.output_directory,
        )
        output_directory_patch.start()
        self.addCleanup(output_directory_patch.stop)

        self.genesis = next(
            book for book in converter.BOOKS if book.abbreviation == "Rdz"
        )

    def test_regeneration_updates_only_text_values_and_preserves_manual_tags_formatting(
        self,
    ) -> None:
        existing_chapter = """\
"Rdz1,1":
  text: "Old first translation."
  tags: [ "creation", "manually added" ]

"Rdz1,2":
  text: "Old second translation."
  tags: ["second tag"]
"""
        chapter_path = self.write_chapter(1, existing_chapter)

        self.regenerate(
            (1, 1, "New first translation."),
            (1, 2, "New second translation."),
        )

        expected_chapter = """\
"Rdz1,1":
  text: "New first translation."
  tags: [ "creation", "manually added" ]

"Rdz1,2":
  text: "New second translation."
  tags: ["second tag"]
"""
        self.assertEqual(expected_chapter, chapter_path.read_text(encoding="utf-8"))

    def test_regeneration_adds_a_new_verse_with_empty_tags(self) -> None:
        existing_chapter = """\
"Rdz1,1":
  text: "Existing translation."
  tags: ["manual tag"]
"""
        chapter_path = self.write_chapter(1, existing_chapter)

        self.regenerate(
            (1, 1, "Updated existing translation."),
            (1, 2, "Brand-new translation."),
        )

        expected_chapter = """\
"Rdz1,1":
  text: "Updated existing translation."
  tags: ["manual tag"]
"Rdz1,2":
  text: "Brand-new translation."
  tags: []
"""
        self.assertEqual(expected_chapter, chapter_path.read_text(encoding="utf-8"))

    def test_regeneration_preserves_crlf_line_endings(self) -> None:
        existing_chapter = (
            b'"Rdz1,1":\r\n'
            b'  text: "Old translation."  \t\r\n'
            b'  tags: ["manual tag"]\r\n'
        )
        chapter_path = self.write_chapter_bytes(1, existing_chapter)

        self.regenerate((1, 1, "New translation."))

        expected_chapter = (
            b'"Rdz1,1":\r\n'
            b'  text: "New translation."  \t\r\n'
            b'  tags: ["manual tag"]\r\n'
        )
        self.assertEqual(expected_chapter, chapter_path.read_bytes())

    def test_regeneration_does_not_reserialize_an_unchanged_text_value(self) -> None:
        existing_chapter = (
            b'"Rdz1,1":\n'
            b'  text: "Already\\u0020current."\n'
            b'  tags: [ "manual tag" ]\n'
        )
        chapter_path = self.write_chapter_bytes(1, existing_chapter)

        self.regenerate((1, 1, "Already current."))

        self.assertEqual(existing_chapter, chapter_path.read_bytes())

    def test_regeneration_keeps_an_existing_verse_missing_from_the_new_source(
        self,
    ) -> None:
        existing_chapter = """\
"Rdz1,1":
  text: "Old first translation."
  tags: ["updated verse tag"]
"Rdz1,2":
  text: "Translation absent from the new source."
  tags: [ "keep", "manual metadata" ]
"""
        chapter_path = self.write_chapter(1, existing_chapter)

        self.regenerate((1, 1, "New first translation."))

        expected_chapter = """\
"Rdz1,1":
  text: "New first translation."
  tags: ["updated verse tag"]
"Rdz1,2":
  text: "Translation absent from the new source."
  tags: [ "keep", "manual metadata" ]
"""
        self.assertEqual(expected_chapter, chapter_path.read_text(encoding="utf-8"))

    def test_regeneration_keeps_an_existing_chapter_missing_from_the_new_source(
        self,
    ) -> None:
        self.write_chapter(
            1,
            """\
"Rdz1,1":
  text: "Old first chapter translation."
  tags: []
""",
        )
        existing_second_chapter = """\
"Rdz2,1":
  text: "Translation absent from the new source."
  tags: [ "keep this chapter", "manual metadata" ]
"""
        second_chapter_path = self.write_chapter(2, existing_second_chapter)

        self.regenerate((1, 1, "New first chapter translation."))

        self.assertTrue(second_chapter_path.is_file())
        self.assertEqual(
            existing_second_chapter,
            second_chapter_path.read_text(encoding="utf-8"),
        )

    def write_chapter(self, chapter: int, contents: str) -> Path:
        return self.write_chapter_bytes(chapter, contents.encode("utf-8"))

    def write_chapter_bytes(self, chapter: int, contents: bytes) -> Path:
        chapter_path = (
            self.output_directory / "TNP" / self.genesis.abbreviation / f"{chapter}.yml"
        )
        chapter_path.parent.mkdir(parents=True, exist_ok=True)
        chapter_path.write_bytes(contents)
        return chapter_path

    def regenerate(self, *verses: tuple[int, int, str]) -> None:
        source_verses = [
            (self.genesis, chapter, verse, text)
            for chapter, verse, text in verses
        ]
        with redirect_stdout(io.StringIO()):
            converter.write_translation("TNP", source_verses)


if __name__ == "__main__":
    unittest.main()
