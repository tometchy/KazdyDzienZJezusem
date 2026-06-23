# ToDo:
- Generate everything as a step in the Dockerfile build
- Clone the repo inside the container
- Endpoint to modify the file in repo and push it
- Host downloading changes in 1min loop and rebuilding on change
- Host docker-compose surviving server restarts
- Instead of original word the path should contain strong code and datus what's in the table and starts with language, for example /en/lexicon/g3745-noun-accusative-singular-feminine

# Textus Receptus

We are pleased to announce the immediate availability of the 1894 Scrivener
edition of the Greek New Testament in various machine-readable formats.

## What exactly is available?

- 1894 text
- Accented text
- Full grammatical apparatus
- Strong's tagging
- English glosses
- No commentary on manuscript evidence (this is a feature)
- JSON, XML, YAML; nested and flat

## Example

```json
{
  "book_greek": "Πρὸς Ἑβραίους",
  "book_name": "Hebrews",
  "book_name_osis": "Heb",
  "book_name_short": "HEB",
  "chapter": 1,
  "verse": 1,
  "greek_text": "Πολυμερῶς καὶ πολυτρόπως πάλαι ὁ Θεὸς λαλήσας τοῖς πατράσιν ἐν τοῖς προφήταις,",
  "words": [
    {
      "greek": "Πολυμερῶς",
      "transliteration": "polumerwv",
      "grammar": "ADV",
      "grammar_human": "Adverb",
      "strong": 4181,
      "dictionary_form": "πολυμερῶς",
      "definition": "in many parts"
    },
    {
      "greek": "καὶ",
      "transliteration": "kai",
      "grammar": "CONJ",
      "grammar_human": "Conjunction",
      "strong": 2532,
      "dictionary_form": "καί",
      "definition": "and, even, also, namely"
    }
  ]
}
```

## Under what terms is this data available?

All of the data files are provided free of charge without any limitation on
use. While not mandatory, we would appreciate if you gave us credit in your
project, and notify us when you launch your project so we can help promote it.

Any associated software is licensed to you under the terms of the GNU General
Public License Version 3 or greater.

## More information

More information can be found on the [project
website](https://honza.pokorny.ca/textus-receptus/).
