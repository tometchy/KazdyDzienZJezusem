"""Check offline Perseus conversion without downloading the upstream archive."""
import importlib.util
import io
from pathlib import Path
import tarfile
import tempfile
import unittest
import unicodedata
import xml.etree.ElementTree as ET

spec = importlib.util.spec_from_file_location("perseus", Path(__file__).resolve().parents[1] / "scripts/greek-perseus.py")
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class PerseusTests(unittest.TestCase):
    def test_beta_preserves_breathing_subscript_and_diaeresis(self):
        self.assertEqual(module.beta_key("a)nazwpurh/santes"), module.unicode_beta("ἀναζωπυρήσαντες"))
        self.assertEqual(module.beta_key("*)/A"), module.unicode_beta("Ἄ"))
        self.assertEqual(module.unicode_beta("λόγος"), module.unicode_beta(unicodedata.normalize("NFD", "ΛΌΓΟΣ")))
        self.assertEqual(module.unicode_beta("λὸγος"), module.unicode_beta("λόγος"))
        for left, right in [("ὄρος", "ὅρος"), ("ᾗ", "ἡ"), ("ϊ", "ι")]:
            self.assertNotEqual(module.unicode_beta(left), module.unicode_beta(right))
        self.assertEqual("ἀναζωπυρέω", module.beta_unicode("a)nazwpure/w"))
        self.assertEqual("παρά-ἐμπλέκω", module.beta_unicode("para/-e)mple/kw"))
        self.assertIsNone(module.beta_key("!lo/gos"))

    def test_codes_follow_source_analysis_not_guessed_nominal_pos(self):
        self.assertEqual(["V-AAI-2P"], module.grammar_codes("aor ind act 2nd pl (attic epic ionic)"))
        self.assertEqual(["V-AAP-NPM", "V-AAP-VPM"], module.grammar_codes("aor part act masc nom/voc pl"))
        self.assertEqual(["V-PMN", "V-PPN"], module.grammar_codes("pres inf mp"))
        self.assertEqual([], module.grammar_codes("masc/fem acc sg"))
        self.assertEqual([], module.grammar_codes("aor act 2nd pl"))

    def test_lsj_uses_definitions_not_italic_fragments_of_examples(self):
        entry = ET.fromstring('<div2><etym><i>wrong etymology</i></etym>'
                             '<sense><bibl><i>wrong citation</i></bibl><i>make even,</i>'
                             '<foreign>σπουδή</foreign><i>does</i> not <i>allow</i></sense>'
                             '<sense>generally, <i>get ready, complete,</i></sense></div2>')
        self.assertEqual(["make even", "get ready, complete"], module.lsj_glosses(entry))

    def test_exact_analysis_wins_and_accent_fallback_keeps_all_lemmas(self):
        analyses = ('lo/gos\t{1 9 lo/gos\tword\tmasc nom sg}\n'
                    'logo/s\t{2 9 logo/s\tother meaning\tmasc nom sg}\n'
                    'poluaga/phton\t{3 9 poluaga/phtos\tmuch-beloved\tneut nom/voc/acc sg}\n')
        with tempfile.TemporaryDirectory(prefix="kdj-perseus-") as directory:
            archive_path = Path(directory) / "source.tar.xz"
            with tarfile.open(archive_path, "w:xz") as archive:
                for name, content in [("greek-analyses.txt", analyses), ("grc.lsj.xml", "")]:
                    data = content.encode()
                    entry = tarfile.TarInfo("dependencies/data/" + name)
                    entry.size = len(data)
                    archive.addfile(entry, io.BytesIO(data))
            result = module.read_perseus(archive_path, {"λόγος", "λογός", "λογοσ", "πολυαγάπητόν", "ἀγνωστότατον"})
        self.assertEqual({"word"}, {entry[0] for entry in result["λόγος"]})
        self.assertEqual({"other meaning"}, {entry[0] for entry in result["λογός"]})
        self.assertEqual({"word", "other meaning"}, {entry[0] for entry in result["λογοσ"]})
        self.assertEqual({"much-beloved"}, {entry[0] for entry in result["πολυαγάπητόν"]})
        self.assertNotIn("ἀγνωστότατον", result)


if __name__ == "__main__":
    unittest.main()
