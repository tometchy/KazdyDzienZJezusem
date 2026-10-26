#!/usr/bin/env python3
"""Regression checks for the static reading-aid generator."""
import importlib.util
from pathlib import Path
import unittest

spec = importlib.util.spec_from_file_location("reading", Path(__file__).with_name("prepare-greek-polish.py"))
reading = importlib.util.module_from_spec(spec)
spec.loader.exec_module(reading)


class ReadingDataTests(unittest.TestCase):
    def test_pronunciation_uses_the_inflected_surface_form(self):
        self.assertEqual(reading.pronunciation("λόγος"), "logos")
        self.assertEqual(reading.pronunciation("λόγου"), "logu")
        self.assertEqual(reading.pronunciation("γενέσεως"), "geneseos")

    def test_breathings_diphthongs_diaeresis_and_nasal_gamma(self):
        for greek, expected in [("ὁ", "ho"), ("ὅρος", "horos"), ("ὄρος", "oros"),
                                ("οὗτος", "hutos"), ("ἄγγελος", "angelos"), ("ἄνθρωπος", "antropos"),
                                ("αἷμα", "hajma"), ("Μωϋσῆς", "moyses"), ("ψυχή", "psyche")]:
            with self.subTest(greek=greek):
                self.assertEqual(reading.pronunciation(greek), expected)
        self.assertEqual(reading.pronunciation("α\u0313ρχη\u0301"), reading.pronunciation("ἀρχή"))

    def test_requested_Ross_codes_and_attested_second_aorist_are_preserved(self):
        self.assertEqual(reading.display_code("βίβλος", "N-NFS", {}), "N-NSF")
        self.assertEqual(reading.display_code("γενέσεως", "N-GFS", {}), "N-GSF")
        self.assertEqual(reading.display_code("ἐγέννησε", "V-AIA-3S", {}), "V-AAI-3S")
        index = {(reading.alignment_key("εἶδον"), "V-AIA-1S"): {"V-2AAI-1S"}}
        self.assertEqual(reading.display_code("εἶδον", "V-AIA-1S", index), "V-2AAI-1S")


if __name__ == "__main__":
    unittest.main()
