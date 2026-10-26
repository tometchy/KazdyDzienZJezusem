"""Offline checks for source separation, inflection safety and GNT coverage."""
import gzip
import importlib.util
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location("supplement", ROOT / "scripts/prepare-greek-supplement.py")
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class SupplementTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.primary = set()
        with gzip.open(ROOT / "input/GreekPolish/interlinear.jsonl.gz", "rt") as stream:
            next(stream)
            for line in stream:
                row = json.loads(line)
                cls.primary.update([row["form"]] if "form" in row else map(module.primary.form_key, row["greek"]))
        with gzip.open(ROOT / "input/GreekPolish/supplement.jsonl.gz", "rt") as stream:
            cls.metadata = json.loads(next(stream))
            cls.rows = {row["form"]: row["glosses"] for row in map(json.loads, stream)}

    def test_no_primary_translation_is_overridden(self):
        self.assertFalse(self.primary & self.rows.keys())
        self.assertEqual(self.metadata["forms"], len(self.rows))

    def test_every_bundled_GNT_word_has_an_English_gloss_or_labelled_definition(self):
        known = self.primary | self.rows.keys()
        verses = json.loads((ROOT / "input-raw/gnt.flat.json").read_text())
        missing = {word["greek"] for verse in verses for word in verse["words"]
                   if module.primary.form_key(module.primary.greek_word(word["greek"])) not in known}
        self.assertEqual(set(), missing)

    def test_supplement_never_presents_declining_Polish_lemmas_as_inflected_glosses(self):
        for word, entries in self.rows.items():
            for entry in entries:
                self.assertTrue(entry["english"], word)
                self.assertIn(entry["englishKind"], {"gloss", "dictionary"})
                self.assertEqual(module.primary.pronunciation(word), entry["pronunciation"])
                if entry["polish"] is not None:
                    self.assertIn(entry["code"], {"ADV", "PREP", "CONJ", "PRT", "INJ"})

    def test_dictionary_normalization_preserves_significant_diacritics(self):
        self.assertEqual(module.dictionary_key("ὄνομᾰ"), module.dictionary_key("ὄνομα"))
        self.assertNotEqual(module.accent_key("ὁ"), module.accent_key("ὀ"))
        self.assertNotEqual(module.accent_key("ᾗ"), module.accent_key("ἡ"))
        self.assertEqual(["α\u0313ρχη\u0301", "βιβλίον"], module.TOKEN.findall("α\u0313ρχη\u0301· βιβλίον;"))

    def test_form_of_entries_are_not_mistaken_for_definitions(self):
        entry = {"senses": [{"form_of": [{"word": "λόγος"}], "glosses": ["genitive of logos"]},
                            {"glosses": ["word"]}]}
        self.assertEqual(["word"], module.definitions(entry))


if __name__ == "__main__":
    unittest.main()
