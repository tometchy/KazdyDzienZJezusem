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
SPEC = importlib.util.spec_from_file_location("converter_under_test", SCRIPT_PATH)
assert SPEC and SPEC.loader
converter = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = converter
SPEC.loader.exec_module(converter)

class ConverterTests(unittest.TestCase):
    def test_writes_flat_verse_mapping_to_input(self):
        with TemporaryDirectory() as directory, patch.object(converter, "OUTPUT_DIR", Path(directory)):
            book = next(book for book in converter.BOOKS if book.abbreviation == "1J")
            with redirect_stdout(io.StringIO()):
                converter.write_translation("TNP", [(book, 5, 12, "Kto ma Syna, ma to życie;")])
            result = (Path(directory) / "TNP" / "1J" / "5.yml").read_text()
            self.assertEqual('"1J5,12": "Kto ma Syna, ma to życie;"\n', result)

    def test_source_contains_all_seven_ignatius_letters(self):
        sections = list(converter.read_ignatius())
        self.assertEqual(7, len({work.abbreviation for work, _, _, _ in sections}))
        self.assertEqual(205, len(sections))

if __name__ == "__main__":
    unittest.main()
