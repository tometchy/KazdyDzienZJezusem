# Static Greek-Polish interlinear data

`interlinear.jsonl.gz` is a deterministic, compressed JSON Lines conversion of
[Maciej Rapacz's Greek Interlinear Translations dataset](https://huggingface.co/datasets/mrapacz/greek-interlinear-translations),
revision `4c842b2622915d608482ece200a352779110ddd8`.

Creator: Maciej Rapacz. Polish text comes from Oblubienica; Greek text with
diacritics and the BibleHub morphology tags come from BibleHub. The source card
identifies their underlying critical editions as NA28 and NA27 respectively.

The source and this converted dataset are licensed under
[Creative Commons Attribution-NonCommercial-ShareAlike 4.0 International](https://creativecommons.org/licenses/by-nc-sa/4.0/).
This data license is separate from the application's code license. The data is
provided as-is, without warranties. Preserve attribution and the license when
redistributing it; commercial reuse is not covered by this license.

Changes: merged the published train, validation and test splits; sorted passages;
converted book numbers to this application's abbreviations; retained aligned
Greek words, inflected Polish and English glosses, and morphology. Source glosses
are not machine-translated. Grammar display codes use the existing GNT Ross
convention (for example `N-NSF`, `N-GSF`): attested GNT codes are preferred,
otherwise BibleHub noun, adjective, article, pronoun and verb tags are converted.
Unrecognized tags retain their source spelling rather than inventing a code.
Added 520 attested form variants from the existing `input-raw/gnt.flat.json`.
An alias requires matching Strong identity AND grammatical inflection, after
converting the Ross and BibleHub tag conventions. This includes, for example,
movable-nu variants. No lemma-only or English-definition fallback is used.

The file contains 7,940 passages and 137,323 word pairs. Its 18,433 Greek form
keys preserve breathing marks and iota subscripts, fold grave accents to acute
accents, and ignore letter case and final-sigma spelling. The source corpus has
already excluded unmatched words and shortened some verses to its training
token limit. It is not a complete lexicon for classical or patristic Greek.

The first JSON line contains source metadata and expected counts. Every remaining
passage line in schema 2 has `reference` and aligned `greek`, `polish`, `english`,
`grammar`, `codes`, and `pronunciation` arrays. Form-alias lines have `form` and
`glosses` fields, including each annotation. A source `-` or blank gloss means
there is no independent equivalent; the reader displays an em dash.
Unknown forms remain marked as unknown. Ambiguous forms expose the source
alternatives instead of silently choosing an inflection. Passage-level alignment
is preferred for Bible verses; Textus Receptus differs from the source editions.

Pronunciation is generated offline from each **inflected surface word**, not its
dictionary lemma or the GNT Beta-code field. It is an approximate Erasmian
classroom reading written for Polish readers, not audio, IPA, or a reconstruction
of historical pronunciation. It handles rough breathing, diphthongs, diaeresis,
nasal gamma, and silent iota subscripts. Vowel length and stress are not shown;
upsilon uses `y`, and theta/phi/chi use the conventional approximations `t/f/ch`.
See the Open University's explanations of
[breathing marks](https://www.open.edu/openlearn/mod/oucontent/view.php?id=157789&section=_unit3.1.2)
and [letter combinations](https://www.open.edu/openlearn/mod/oucontent/view.php?id=157789&section=_unit9.1.4).

To regenerate from verified, pinned upstream files (only at development time):

```sh
python3 -m pip install --target /tmp/kdj-lexicon-packages pyarrow 'numpy>=2'
PYTHONPATH=/tmp/kdj-lexicon-packages python3 scripts/prepare-greek-polish.py
python3 scripts/test-greek-reading-data.py
```

No downloader, Python dependency, translation model or translation API is used
by the running application. `GreekDictionaryRepository` populates versioned Redis
hashes from the bundled file and serves all dictionary requests from those hashes.

Reference: Maciej Rapacz and Aleksander Smywiński-Pohl (2025),
[Low-Resource Interlinear Translation: Morphology-Enhanced Neural Models for Ancient Greek](https://aclanthology.org/2025.loreslm-1.11/),
Proceedings of LoResLM, pages 145–165.

## Additional offline vocabulary

`supplement.jsonl.gz` is a separate dataset, not a modification of the licensed
Polish interlinear corpus above. It contributes 2,831 non-overlapping form keys,
bringing the Redis form index to 21,264 keys. Sources:

- [MACULA Greek Linguistic Datasets](https://github.com/Clear-Bible/macula-greek/),
  revision `8423afe47b9e8f24b7772e808af45c7159a6fe7e`, Nestle1904 TSV.
  Morphology: © 2022–2024 Biblica, Inc., linguistics by Clear Bible, Inc.,
  [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/).
  English uses the TSV `gloss` field from the Berean Interlinear Bible, placed
  in the public domain in 2023, not the separate Cherith `english` column.
- [English Wiktionary Ancient Greek via Kaikki](https://kaikki.org/dictionary/Ancient%20Greek/index.html)
  and [Polish Wiktionary Ancient Greek via Kaikki](https://kaikki.org/plwiktionary/j%C4%99zyk%20starogrecki/index.html),
  Wiktionary contributors, October 2026 dump snapshots extracted October 24–25.
  Reused and adapted under [CC BY-SA 4.0](https://creativecommons.org/licenses/by-sa/4.0/).
  Changes: selected glosses, resolved attested form tables, removed editorial
  vowel-length marks, indexed relevant surface spellings, and added pronunciation.
  Last-resort accent normalization retains breathing, diaeresis and iota
  subscript; ambiguous entries retain alternatives rather than choosing a lemma.
- Definitions already supplied in `input-raw/gnt.flat.json`, matched to its
  exact word spellings. Their original provenance and terms are unchanged by
  this conversion; the licenses above do not relicense that source.
- [Perseus Digital Library / Tufts Morpheus analyses](https://github.com/PerseusDL/morpheus)
  and [LSJ as edited for Logeion by Helma Dik](https://github.com/helmadik/LSJLogeion),
  distributed in [Peter J. Heslin's Diogenes prebuilt data](https://github.com/pjheslin/diogenes-prebuilt-data),
  revision `3514648bbd76757d93bae1faec4710763f90fca0`.
  Morpheus data: [CC BY-SA 3.0 US](https://creativecommons.org/licenses/by-sa/3.0/us/);
  LSJ/Logeion: [CC BY-SA 4.0](https://creativecommons.org/licenses/by-sa/4.0/).
  The selected and adapted extract is shared under CC BY-SA 4.0 with those
  original credits retained. No Diogenes application code is bundled.
  Changes: selected 343 previously missing surface spellings; converted Beta
  Code to Unicode; linked the table's attested analyses to LSJ definitions;
  retained the first English definition from up to three senses, falling back
  to the table's short definition; converted explicit verbal analyses to Ross
  codes and added surface-form pronunciation. Exact accents are preferred;
  last-resort accent matching retains all analyses and preserves breathing,
  diaeresis and iota subscript. Noun/adjective codes are not fabricated when
  the morphology table does not distinguish their parts of speech.

The supplement's first line records source URLs, pinned SHA-256 checksums,
revision, counts, and uncovered vocabulary. Upstream Wiktionary URLs change;
regeneration rejects an unexpected checksum rather than silently using new data.
Keep the pinned files in `artifacts/greek-supplement-source/` when regenerating.

All 140,628 GNT word occurrences (18,273 distinct normalized forms) have an
English gloss or dictionary meaning. Across analyzed shipped YAML/GNT vocabulary,
19,188 of 19,273 form types are covered (about 99.6%), versus 18,845 before
the Perseus/Logeion addition (16,866 with the original corpus alone).
The remaining 85 include patristic forms, unusual spellings and proper names.
This is not a universal lexicon and it does not translate unknown compounds.

`englishKind: "gloss"` identifies word-level English; `"dictionary"` identifies
a lemma meaning, rendered with `†` and an explanatory tooltip. The original
Polish corpus always wins. Only uninflected Polish adverbs, prepositions,
conjunctions, particles and interjections are added (nine form keys); declining
Polish lemma meanings are never substituted. Inflected Greek forms are linked
through source form tables, not generated by suffix guessing. Supplementary
grammar codes are omitted when the available tags do not identify a full code.

Other researched Polish resources include the freely shared
[Dickinson Greek core vocabulary](https://dcc.dickinson.edu/vocab/core-vocabulary)
with Polish translations and [Grepl / Sub Lupa](https://sublupa.pl/grepl-pl/).
The first is a small lemma list, not an inflected translation corpus. No bulk
redistribution permission was established for Grepl, so its entries are not
scraped or bundled. Neither justifies inventing Polish declensions.

To regenerate the supplement after adding Greek texts (development time only):

```sh
python3 scripts/prepare-greek-supplement.py
python3 -m unittest discover -s tests -p 'test_*.py' -v
```

Only the small generated gzip file is published with the application. The running
app imports it into Redis with the primary corpus; browser requests never query
MACULA, Wiktionary, Kaikki, Perseus, Logeion, or a translation API.
