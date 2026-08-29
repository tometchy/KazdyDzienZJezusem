from __future__ import annotations

import json
import re
import xml.etree.ElementTree as ElementTree
from collections import defaultdict
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable, Iterator
from zipfile import ZipFile


BASE_DIR = Path(__file__).resolve().parent
INPUT_DIR = BASE_DIR / "input"
OUTPUT_DIR = BASE_DIR / "output"


@dataclass(frozen=True)
class Book:
    abbreviation: str
    kjv_name: str
    osis_name: str
    tnp_file_stem: str


# The order and directory names follow the TNP list supplied with the project.
BOOKS = (
    Book("Rdz", "Genesis", "Gen", "Gen"),
    Book("Wj", "Exodus", "Exod", "Exod"),
    Book("Kpł", "Leviticus", "Lev", "Lev"),
    Book("Lb", "Numbers", "Num", "Num"),
    Book("Pwt", "Deuteronomy", "Deut", "Deut"),
    Book("Joz", "Joshua", "Josh", "Josh"),
    Book("Sdz", "Judges", "Judg", "Judg"),
    Book("Rt", "Ruth", "Ruth", "Ruth"),
    Book("1Sm", "1 Samuel", "1Sam", "1Sam"),
    Book("2Sm", "2 Samuel", "2Sam", "2Sam"),
    Book("1Krl", "1 Kings", "1Kgs", "1Kgs"),
    Book("2Krl", "2 Kings", "2Kgs", "2Kgs"),
    Book("1Krn", "1 Chronicles", "1Chr", "1Chr"),
    Book("2Krn", "2 Chronicles", "2Chr", "2Chr"),
    Book("Ezd", "Ezra", "Ezra", "Ezra"),
    Book("Ne", "Nehemiah", "Neh", "Neh"),
    Book("Est", "Esther", "Esth", "Esth"),
    Book("Hi", "Job", "Job", "Job"),
    Book("Ps", "Psalms", "Ps", "Ps"),
    Book("Prz", "Proverbs", "Prov", "Prov"),
    Book("Kaz", "Ecclesiastes", "Eccl", "Eccl"),
    Book("Pnp", "Solomon's Song", "Song", "Song"),
    Book("Iz", "Isaiah", "Isa", "Isa"),
    Book("Jr", "Jeremiah", "Jer", "Jer"),
    Book("Lm", "Lamentations", "Lam", "Lam"),
    Book("Ez", "Ezekiel", "Ezek", "Ezek"),
    Book("Dn", "Daniel", "Dan", "Dan"),
    Book("Oz", "Hosea", "Hos", "Hos"),
    Book("Jl", "Joel", "Joel", "Joel"),
    Book("Am", "Amos", "Amos", "Amos"),
    Book("Ab", "Obadiah", "Obad", "Obad"),
    Book("Jon", "Jonah", "Jonah", "Jonah"),
    Book("Mi", "Micah", "Mic", "Mic"),
    Book("Na", "Nahum", "Nah", "Nah"),
    Book("Ha", "Habakkuk", "Hab", "Hab"),
    Book("So", "Zephaniah", "Zeph", "Zeph"),
    Book("Ag", "Haggai", "Hag", "Hag"),
    Book("Za", "Zechariah", "Zech", "Zech"),
    Book("Ml", "Malachi", "Mal", "Mal"),
    Book("Mt", "Matthew", "Matt", "Matt"),
    Book("Mk", "Mark", "Mark", "Mark"),
    Book("Łk", "Luke", "Luke", "Luke"),
    Book("J", "John", "John", "John"),
    Book("Dz", "Acts", "Acts", "Acts"),
    Book("Rz", "Romans", "Rom", "Rom"),
    Book("1Kor", "1 Corinthians", "1Cor", "1Cor"),
    Book("2Kor", "2 Corinthians", "2Cor", "2Cor"),
    Book("Ga", "Galatians", "Gal", "Gal"),
    Book("Ef", "Ephesians", "Eph", "Eph"),
    Book("Flp", "Philippians", "Phil", "Phil"),
    Book("Kol", "Colossians", "Col", "Col"),
    Book("1Tes", "1 Thessalonians", "1Thess", "1Thess"),
    Book("2Tes", "2 Thessalonians", "2Thess", "2Thess"),
    Book("1Tm", "1 Timothy", "1Tim", "1Tim"),
    Book("2Tm", "2 Timothy", "2Tim", "2Tim"),
    Book("Tt", "Titus", "Titus", "Titus"),
    Book("Flm", "Philemon", "Phlm", "Phlm"),
    Book("Hbr", "Hebrews", "Heb", "Heb"),
    Book("Jk", "James", "Jas", "Jas"),
    Book("1P", "1 Peter", "1Pet", "1Pet"),
    Book("2P", "2 Peter", "2Pet", "2Pet"),
    Book("1J", "1 John", "1John", "1John"),
    Book("2J", "2 John", "2John", "2John"),
    Book("3J", "3 John", "3John", "3John"),
    Book("Jud", "Jude", "Jude", "Jude"),
    Book("Ob", "Revelation", "Rev", "Rev"),
)

BOOK_BY_KJV_NAME = {book.kjv_name: book for book in BOOKS}
BOOK_BY_OSIS_NAME = {book.osis_name: book for book in BOOKS}
BOOK_BY_NUMBER = {number: book for number, book in enumerate(BOOKS, start=1)}

Verse = tuple[Book, int, int, str]
Chapters = dict[Book, dict[int, dict[int, str]]]

TNP_VERSE_ID = re.compile(r"^v(\d+)\.(\d+)$")
UBG_VERSE_ID = re.compile(r"^BG-(\d+)_(\d+)$")
KJV_REFERENCE = re.compile(r"^(.+?) (\d+):(\d+)$")


def normalize_text(text: str) -> str:
    text = text.replace("\xad", "")
    return re.sub(r"\s+", " ", text).strip()


def local_tag_name(element: ElementTree.Element) -> str:
    return element.tag.rsplit("}", 1)[-1]


def extract_text(
    element: ElementTree.Element,
    *,
    excluded_classes: frozenset[str] = frozenset(),
    excluded_tags: frozenset[str] = frozenset(),
) -> str:
    parts: list[str] = []

    def visit(node: ElementTree.Element) -> None:
        if node.text:
            parts.append(node.text)

        for child in node:
            classes = frozenset(child.attrib.get("class", "").split())
            if (
                local_tag_name(child) not in excluded_tags
                and classes.isdisjoint(excluded_classes)
            ):
                visit(child)

            if child.tail:
                parts.append(child.tail)

    visit(element)
    return normalize_text("".join(parts))


def parse_epub_document(data: bytes) -> ElementTree.Element:
    # XHTML only defines &nbsp; through an external DTD, which ElementTree does
    # not load. Its numeric form is self-contained and equivalent.
    data = data.replace(b"&nbsp;", b"&#160;")
    return ElementTree.fromstring(data)


def iter_json_array(path: Path) -> Iterator[object]:
    """Read a top-level JSON array without loading the entire file into RAM."""
    decoder = json.JSONDecoder()
    buffer = ""
    position = 0
    started = False
    expect_value = True

    with path.open("r", encoding="utf-8-sig") as source:
        while True:
            if position:
                buffer = buffer[position:]
                position = 0

            chunk = source.read(64 * 1024)
            end_of_file = chunk == ""
            buffer += chunk

            while True:
                while position < len(buffer) and buffer[position].isspace():
                    position += 1

                if not started:
                    if position == len(buffer):
                        break
                    if buffer[position] != "[":
                        raise ValueError(f"Expected a JSON array in {path}")
                    started = True
                    position += 1
                    continue

                while position < len(buffer) and buffer[position].isspace():
                    position += 1

                if position == len(buffer):
                    break

                if expect_value:
                    if buffer[position] == "]":
                        return

                    try:
                        item, position = decoder.raw_decode(buffer, position)
                    except json.JSONDecodeError as error:
                        if end_of_file:
                            raise ValueError(f"Invalid JSON in {path}: {error}") from error
                        break

                    yield item
                    expect_value = False
                    continue

                if buffer[position] == ",":
                    position += 1
                    expect_value = True
                    continue
                if buffer[position] == "]":
                    return

                raise ValueError(f"Expected ',' or ']' in {path}")

            if end_of_file:
                raise ValueError(f"Unexpected end of JSON data in {path}")


def read_tr() -> Iterator[Verse]:
    path = INPUT_DIR / "gnt.flat.json"

    for raw_item in iter_json_array(path):
        if not isinstance(raw_item, dict):
            raise ValueError(f"Expected an object in {path}, got {type(raw_item).__name__}")

        osis_name = str(raw_item["book_name_osis"])
        try:
            book = BOOK_BY_OSIS_NAME[osis_name]
        except KeyError as error:
            raise ValueError(f"Unknown TR book name: {osis_name}") from error

        yield (
            book,
            int(raw_item["chapter"]),
            int(raw_item["verse"]),
            normalize_text(str(raw_item["greek_text"])),
        )


def read_tnp() -> Iterator[Verse]:
    path = INPUT_DIR / "Biblia_przeklad_Torunski.epub"
    excluded_classes = frozenset({"ca", "ct", "rf", "st"})

    with ZipFile(path) as archive:
        archive_names = frozenset(archive.namelist())
        missing_books = [
            book.abbreviation
            for book in BOOKS
            if f"{book.tnp_file_stem}.html" not in archive_names
        ]
        if missing_books:
            print(f"TNP source EPUB has no text for: {', '.join(missing_books)}")

        for book in BOOKS:
            document_name = f"{book.tnp_file_stem}.html"
            if document_name not in archive_names:
                continue

            root = parse_epub_document(archive.read(document_name))
            for node in root.iter():
                match = TNP_VERSE_ID.fullmatch(node.attrib.get("id", ""))
                if match is None:
                    continue

                chapter, verse = (int(value) for value in match.groups())
                if verse == 0:
                    continue

                text = extract_text(node, excluded_classes=excluded_classes)
                text = re.sub(rf"^{verse}\.\s*", "", text)
                yield book, chapter, verse, text


def read_ubg() -> Iterator[Verse]:
    path = INPUT_DIR / "UBG_2025.epub"

    with ZipFile(path) as archive:
        for book_number, book in BOOK_BY_NUMBER.items():
            document_name = f"OEBPS/Text/PL-{book_number:02d}.xhtml"
            try:
                data = archive.read(document_name)
            except KeyError as error:
                raise ValueError(f"Missing UBG book document: {document_name}") from error

            root = parse_epub_document(data)
            for node in root.iter():
                match = UBG_VERSE_ID.fullmatch(node.attrib.get("id", ""))
                if match is None:
                    continue

                chapter, verse = (int(value) for value in match.groups())
                text = extract_text(node, excluded_tags=frozenset({"sup"}))
                yield book, chapter, verse, text


def read_kjv() -> Iterator[Verse]:
    path = INPUT_DIR / "verses-1769.json"
    with path.open("r", encoding="utf-8") as source:
        data = json.load(source)

    if not isinstance(data, dict):
        raise ValueError(f"Expected a JSON object in {path}")

    for reference, raw_text in data.items():
        match = KJV_REFERENCE.fullmatch(reference)
        if match is None:
            raise ValueError(f"Invalid KJV reference: {reference}")

        book_name, chapter, verse = match.groups()
        try:
            book = BOOK_BY_KJV_NAME[book_name]
        except KeyError as error:
            raise ValueError(f"Unknown KJV book name: {book_name}") from error

        text = re.sub(r"^#\s*", "", normalize_text(str(raw_text)))
        yield book, int(chapter), int(verse), text


def collect_chapters(translation: str, verses: Iterable[Verse]) -> Chapters:
    chapters: Chapters = defaultdict(lambda: defaultdict(dict))

    for book, chapter, verse, text in verses:
        if chapter < 1 or verse < 1:
            raise ValueError(
                f"Invalid {translation} reference: {book.abbreviation} {chapter}:{verse}"
            )
        if not text:
            raise ValueError(
                f"Empty {translation} verse: {book.abbreviation} {chapter}:{verse}"
            )
        if verse in chapters[book][chapter]:
            raise ValueError(
                f"Duplicate {translation} verse: {book.abbreviation} {chapter}:{verse}"
            )

        chapters[book][chapter][verse] = text

    return chapters


def yaml_string(value: str) -> str:
    # JSON double-quoted strings are valid YAML and handle escaping reliably.
    return json.dumps(value, ensure_ascii=False)


def render_verse(
    book: Book,
    chapter: int,
    verse: int,
    text: str,
    *,
    newline: str = "\n",
) -> str:
    return "".join(
        (
            f"{yaml_string(f'{book.abbreviation}{chapter},{verse}')}:{newline}",
            f"  text: {yaml_string(text)}{newline}",
            f"  tags: []{newline}",
        )
    )


def render_chapter(
    book: Book,
    chapter: int,
    chapter_verses: dict[int, str],
) -> str:
    return "".join(
        render_verse(book, chapter, verse, text)
        for verse, text in sorted(chapter_verses.items())
    )


def update_chapter_texts(
    output_path: Path,
    book: Book,
    chapter: int,
    chapter_verses: dict[int, str],
) -> None:
    if not output_path.is_file():
        output_path.write_text(
            render_chapter(book, chapter, chapter_verses),
            encoding="utf-8",
        )
        return

    original_content = output_path.read_bytes().decode("utf-8")
    lines = original_content.splitlines(keepends=True)

    # Migrate files generated with the old one-line schema. They cannot contain
    # editable tags, so there is no user-authored metadata to preserve.
    first_content_line = next((line for line in lines if line.strip()), "")
    first_line_body = first_content_line.rstrip("\r\n").rstrip()
    if not first_line_body or (
        not first_line_body[0].isspace() and not first_line_body.endswith(":")
    ):
        updated_content = render_chapter(book, chapter, chapter_verses)
        if updated_content != original_content:
            output_path.write_bytes(updated_content.encode("utf-8"))
        return

    key_prefix = f"{book.abbreviation}{chapter},"
    current_verse: int | None = None
    current_text_line: int | None = None
    current_text: str | None = None
    current_has_tags = False
    text_lines_by_verse: dict[int, int] = {}
    existing_texts_by_verse: dict[int, str] = {}

    def finish_verse() -> None:
        if current_verse is None:
            return
        if current_text_line is None or current_text is None:
            raise ValueError(
                f"Missing text for verse {current_verse} in {output_path}"
            )
        if not current_has_tags:
            raise ValueError(
                f"Missing tags for verse {current_verse} in {output_path}"
            )
        text_lines_by_verse[current_verse] = current_text_line
        existing_texts_by_verse[current_verse] = current_text

    for line_index, line in enumerate(lines):
        line_number = line_index + 1
        line_body = line.rstrip("\r\n")
        if not line_body.strip():
            continue

        if not line_body[0].isspace():
            finish_verse()
            serialized_key = line_body.rstrip()
            if not serialized_key.endswith(":"):
                raise ValueError(f"Invalid verse key in {output_path}:{line_number}")

            try:
                key = json.loads(serialized_key[:-1])
                if not isinstance(key, str) or not key.startswith(key_prefix):
                    raise ValueError
                verse = int(key[len(key_prefix) :])
                if verse < 1 or verse in text_lines_by_verse:
                    raise ValueError
            except (json.JSONDecodeError, ValueError) as error:
                raise ValueError(
                    f"Invalid verse key in {output_path}:{line_number}"
                ) from error

            current_verse = verse
            current_text_line = None
            current_text = None
            current_has_tags = False
            continue

        if current_verse is None:
            raise ValueError(
                f"Verse field without a key in {output_path}:{line_number}"
            )

        field_name, separator, serialized_value = line_body.strip().partition(":")
        if not separator or field_name not in {"text", "tags"}:
            continue

        if field_name == "tags":
            if current_has_tags:
                raise ValueError(
                    f"Duplicate tags for verse {current_verse} in {output_path}"
                )
            try:
                tags = json.loads(serialized_value.strip())
            except json.JSONDecodeError as error:
                raise ValueError(
                    f"Invalid tags for verse {current_verse} in {output_path}"
                ) from error
            if not isinstance(tags, list) or not all(
                isinstance(tag, str) for tag in tags
            ):
                raise ValueError(
                    f"Invalid tags for verse {current_verse} in {output_path}"
                )
            current_has_tags = True
            continue

        if current_text_line is not None:
            raise ValueError(
                f"Duplicate text for verse {current_verse} in {output_path}"
            )

        try:
            existing_text = json.loads(serialized_value.strip())
        except json.JSONDecodeError as error:
            raise ValueError(
                f"Invalid text for verse {current_verse} in {output_path}"
            ) from error
        if not isinstance(existing_text, str):
            raise ValueError(
                f"Invalid text for verse {current_verse} in {output_path}"
            )

        current_text_line = line_index
        current_text = existing_text

    finish_verse()

    for verse, text in chapter_verses.items():
        line_index = text_lines_by_verse.get(verse)
        if line_index is None or existing_texts_by_verse[verse] == text:
            continue

        line = lines[line_index]
        line_body = line.rstrip("\r\n")
        line_ending = line[len(line_body) :]
        separator_index = line_body.index(":")
        value_start = separator_index + 1
        while value_start < len(line_body) and line_body[value_start] in " \t":
            value_start += 1
        _, value_end = json.JSONDecoder().raw_decode(line_body[value_start:])
        value_suffix = line_body[value_start + value_end :]
        lines[line_index] = (
            f"{line_body[:value_start]}{yaml_string(text)}{value_suffix}{line_ending}"
        )

    missing_verses = sorted(set(chapter_verses) - set(text_lines_by_verse))
    if missing_verses:
        newline = "\r\n" if any(line.endswith("\r\n") for line in lines) else "\n"
        if lines and not lines[-1].endswith(("\n", "\r")):
            lines[-1] += newline
        lines.extend(
            render_verse(
                book,
                chapter,
                verse,
                chapter_verses[verse],
                newline=newline,
            )
            for verse in missing_verses
        )

    updated_content = "".join(lines)
    if updated_content != original_content:
        output_path.write_bytes(updated_content.encode("utf-8"))


def write_translation(translation: str, verses: Iterable[Verse]) -> None:
    chapters = collect_chapters(translation, verses)
    translation_dir = OUTPUT_DIR / translation
    translation_dir.mkdir(parents=True, exist_ok=True)

    chapter_count = 0
    verse_count = 0

    for book in BOOKS:
        book_chapters = chapters.get(book)
        if not book_chapters:
            continue

        book_dir = translation_dir / book.abbreviation
        book_dir.mkdir(parents=True, exist_ok=True)

        for chapter, chapter_verses in sorted(book_chapters.items()):
            output_path = book_dir / f"{chapter}.yml"
            update_chapter_texts(output_path, book, chapter, chapter_verses)
            chapter_count += 1
            verse_count += len(chapter_verses)

    print(
        f"{translation}: {len(chapters)} books, {chapter_count} chapters, "
        f"{verse_count} verses"
    )


def main() -> None:
    required_inputs = (
        INPUT_DIR / "UBG_2025.epub",
        INPUT_DIR / "Biblia_przeklad_Torunski.epub",
        INPUT_DIR / "gnt.flat.json",
        INPUT_DIR / "verses-1769.json",
    )
    missing_inputs = [path for path in required_inputs if not path.is_file()]
    if missing_inputs:
        formatted_paths = ", ".join(str(path) for path in missing_inputs)
        raise FileNotFoundError(f"Missing input files: {formatted_paths}")

    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    write_translation("UBG", read_ubg())
    write_translation("TNP", read_tnp())
    write_translation("TR", read_tr())
    write_translation("KJV", read_kjv())
    print(f"Conversion complete: {OUTPUT_DIR}")


if __name__ == "__main__":
    main()
