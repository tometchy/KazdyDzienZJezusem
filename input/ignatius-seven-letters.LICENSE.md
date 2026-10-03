# Ignatius of Antioch Greek text

`ignatius-seven-letters.xml` contains the seven-letter Greek edition of Ignatius
of Antioch edited by Kirsopp Lake for *The Apostolic Fathers*, volume 1 (1912).

The XML transcription was created by the Perseus Digital Library and is
distributed by the Open Greek and Latin First1KGreek project under the
[Creative Commons Attribution-ShareAlike 4.0 International licence](https://creativecommons.org/licenses/by-sa/4.0/).
The same licence applies to the derived files under `output/Fathers/Ignatius`.

Source:
<https://github.com/OpenGreekAndLatin/First1KGreek/blob/a4ec7f8338dc553f76443375e52bb503cc884542/data/tlg1443/tlg001/tlg1443.tlg001.1st1K-grc1.xml>

Local changes: `convert-input-to-output.py` divides the TEI source into letters,
chapters, and numbered sections, normalizes whitespace, and omits critical
apparatus notes from the reader output. The original TEI file is retained
unchanged in `input`.
