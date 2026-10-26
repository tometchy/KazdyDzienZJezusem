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
Greek words, inflected Polish glosses and morphology; omitted English glosses
and duplicate tag systems. Source Polish glosses are not machine-translated.
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
passage line has `reference`, `greek`, `polish`, and `grammar` arrays. Form-alias
lines have `form` and `glosses` fields. A source `-` gloss
means there is no independent Polish equivalent; the reader displays an em dash.
Unknown forms remain marked as unknown. Ambiguous forms expose the source
alternatives instead of silently choosing an inflection. Passage-level alignment
is preferred for Bible verses; Textus Receptus differs from the source editions.

To regenerate from verified, pinned upstream files (only at development time):

```sh
python3 -m pip install --target /tmp/kdj-lexicon-packages pyarrow 'numpy>=2'
PYTHONPATH=/tmp/kdj-lexicon-packages python3 scripts/prepare-greek-polish.py
```

No downloader, Python dependency, translation model or translation API is used
by the running application. `GreekDictionaryRepository` populates versioned Redis
hashes from the bundled file and serves all dictionary requests from those hashes.

Reference: Maciej Rapacz and Aleksander Smywiński-Pohl (2025),
[Low-Resource Interlinear Translation: Morphology-Enhanced Neural Models for Ancient Greek](https://aclanthology.org/2025.loreslm-1.11/),
Proceedings of LoResLM, pages 145–165.
