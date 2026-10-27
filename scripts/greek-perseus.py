"""Read attested Perseus/Diogenes forms and LSJ meanings at preparation time."""
import itertools
import re
import tarfile
import unicodedata
import xml.etree.ElementTree as ET

ALPHABET = dict(zip("abgdezhqiklmncoprstufxywv", "αβγδεζηθικλμνξοπρστυφχψωϝ"))
MARKS = {"(": "\u0314", ")": "\u0313", "/": "\u0301", "=": "\u0342", "+": "\u0308", "|": "\u0345"}
ORDER = {character: index for index, character in enumerate(")(+=/|")}


def beta_key(text):
    """Canonicalize Beta Code without dropping breathing or iota subscript."""
    text = text.lower().replace("*", "").replace("\\", "/").replace("_", "").replace("^", "")
    prefix = ""
    while text and text[0] in MARKS:
        prefix += text[0]
        text = text[1:]
    result = []
    index = 0
    while index < len(text):
        letter = text[index]
        if letter not in ALPHABET:
            return None  # Do not guess damaged, abbreviated or special notation.
        index += 1
        marks = prefix if not result else ""
        while index < len(text) and text[index] in MARKS:
            marks += text[index]
            index += 1
        result.append(letter + "".join(sorted(set(marks), key=ORDER.get)))
    return "".join(result) or None


def unicode_beta(text):
    alphabet = {value: key for key, value in ALPHABET.items()}
    alphabet["ς"] = "s"
    marks = {value: key for key, value in MARKS.items()}
    marks["\u0300"] = "/"
    output = []
    for character in unicodedata.normalize("NFD", text.lower()):
        if character in alphabet:
            output.append(alphabet[character])
        elif character in marks:
            output.append(marks[character])
        elif character not in "\u0304\u0306":
            return None
    return beta_key("".join(output))


def lemma_key(text):
    match = re.fullmatch(r"(.+?)([0-9]*)", text)
    if not match:
        return None
    key = beta_key(match[1].replace("-", ""))
    return key + match[2] if key else None


def beta_unicode(text):
    if "-" in text:
        parts = [beta_unicode(part) for part in text.split("-")]
        return "-".join(parts) if all(parts) else None
    match = re.fullmatch(r"(.+?)([0-9]*)", text)
    key = beta_key(match[1]) if match else None
    if not key:
        return None
    result = "".join(ALPHABET.get(character, MARKS.get(character, "")) for character in key)
    result = unicodedata.normalize("NFC", result)
    if result.endswith("σ"):
        result = result[:-1] + "ς"
    return result + match[2]


def grammar_codes(analysis):
    for label, code in [("adverb", "ADV"), ("prep", "PREP"), ("conj", "CONJ"), ("particle", "PRT"), ("exclam", "INJ")]:
        if f"({label})" in analysis:
            return [code]
    parts = re.sub(r"\([^)]*\)", "", analysis).split()
    tenses = {"pres": "P", "imperf": "I", "fut": "F", "aor": "A", "perf": "R", "plup": "L", "futperf": "X"}
    moods = {"ind": "I", "subj": "S", "opt": "O", "imperat": "M", "inf": "N", "part": "P"}
    voices = {"act": ["A"], "mid": ["M"], "pass": ["P"], "mp": ["M", "P"]}
    tense = next((tenses[part] for part in parts if part in tenses), None)
    mood = next((moods[part] for part in parts if part in moods), None)
    voice = next((voices[part] for part in parts if part in voices), [])
    if not tense or not mood or not voice:
        # Nominal morphology lacks noun/adjective POS: never fabricate N-*.
        return []
    numbers = [value for name, value in {"sg": "S", "pl": "P", "dual": "D"}.items() if name in parts]
    endings = [""]
    if mood == "P":
        tags = set(itertools.chain.from_iterable(part.split("/") for part in parts))
        cases = [value for name, value in {"nom": "N", "gen": "G", "dat": "D", "acc": "A", "voc": "V"}.items() if name in tags]
        genders = [value for name, value in {"masc": "M", "fem": "F", "neut": "N"}.items() if name in tags]
        endings = ["-" + case + number + gender for case, number, gender in itertools.product(cases, numbers, genders)]
    elif mood != "N":
        persons = [value for name, value in {"1st": "1", "2nd": "2", "3rd": "3"}.items() if name in parts]
        endings = ["-" + person + number for person, number in itertools.product(persons, numbers)]
    return sorted({f"V-{tense}{value}{mood}{ending}" for value, ending in itertools.product(voice, endings)})


def lsj_glosses(element):
    """Read the first definition per sense, not italic fragments in examples."""
    result = []

    def visit(node):
        if node.tag in {"sense", "bibl", "cit", "quote", "foreign", "etym", "title", "author"}:
            return None
        if node.tag == "i":
            text = re.sub(r"\s+", " ", "".join(node.itertext())).strip(" ,;:.")
            if (re.search("[A-Za-z]", text) and not re.search("[\u0370-\u03ff\u1f00-\u1fff]", text)
                    and 2 < len(text) <= 160 and text not in {"of", "for", "of, for", "the", "and", "with"}):
                return text
        for child in node:
            text = visit(child)
            if text:
                return text
        return None

    for sense in element.iter("sense"):
        text = next((value for child in sense if (value := visit(child))), None)
        if text and text not in result:
            result.append(text)
    return result[:3]


def read_perseus(path, wanted):
    by_beta = {}
    by_accentless = {}
    for word in wanted:
        key = unicode_beta(word)
        if key:
            by_beta.setdefault(key, []).append(word)
            by_accentless.setdefault(key.replace("/", "").replace("=", ""), []).append(word)
    result = {}
    fallback = {}
    lemmas = set()
    with tarfile.open(path, "r:xz") as archive:
        # Read one whitelisted member, never extract archive paths to disk.
        with archive.extractfile("dependencies/data/greek-analyses.txt") as stream:
            for raw in stream:
                form, _, analyses = raw.decode("utf-8").partition("\t")
                form_key = beta_key(form)
                words = by_accentless.get(form_key.replace("/", "").replace("=", "")) if form_key else None
                if not words:
                    continue
                for analysis in re.findall(r"\{([^{}]+)\}", analyses):
                    columns = analysis.split("\t")
                    if len(columns) != 3:
                        continue
                    header, gloss, grammar = columns
                    fields = header.split(maxsplit=2)
                    if len(fields) != 3 or not gloss.strip():
                        continue
                    lemma = fields[2].rsplit(",", 1)[-1]
                    key = lemma_key(lemma)
                    label = beta_unicode(lemma)
                    if not key or not label:
                        continue
                    lemmas.add(key)
                    for word in words:
                        target = result if word in by_beta.get(form_key, []) else fallback
                        target.setdefault(word, set()).add((key, label, gloss.strip(), grammar))
        # Accent fallback covers enclitic accent variants. Keep all analyses;
        # never collapse rough/smooth breathing, diaeresis or iota subscript.
        result.update({word: entries for word, entries in fallback.items() if word not in result})
        definitions = {}
        with archive.extractfile("dependencies/data/grc.lsj.xml") as stream:
            for raw in stream:
                line = raw.decode("utf-8")
                match = re.search(r'\bkey="([^"]+)"', line)
                key = lemma_key(match[1]) if match else None
                if key not in lemmas:
                    continue
                try:
                    entry = ET.fromstring(line)
                except ET.ParseError:
                    continue  # The attested table's short definition is retained.
                glosses = lsj_glosses(entry)
                if glosses:
                    definitions[key] = " / ".join(glosses)
    return {word: {(definitions.get(key, gloss), code, label, "dictionary", "Perseus / Logeion")
                   for key, label, gloss, grammar in entries for code in (grammar_codes(grammar) or [None])}
            for word, entries in result.items()}
