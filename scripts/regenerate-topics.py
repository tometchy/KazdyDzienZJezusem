#!/usr/bin/env python3

from pathlib import Path
import re
import shutil
import sys


def extract_ubg_quote(verse_file: Path):
    try:
        text = verse_file.read_text(encoding="utf-8")
    except FileNotFoundError:
        return None

    normalized = text.replace("\r\n", "\n").replace("\r", "\n")
    match = re.search(r"(?ms)^\[UBG\].*?\n>\s?(.*?)(?:\n\[|\Z)", normalized)
    if not match:
        return None

    quote = match.group(1).strip()
    return quote or None


def process_topic_file(source_file: Path, bible_dir: Path) -> str:
    normalized = source_file.read_text(encoding="utf-8").replace("\r\n", "\n").replace("\r", "\n")
    lines = normalized.split("\n")
    output_lines: list[str] = []

    for line in lines:
        output_lines.append(line)
        for match in re.finditer(r"\[\[(.+?)\]\]", line):
            target = match.group(1).split("|", 1)[0].strip()
            if target.startswith("Biblia/"):
                target = target[len("Biblia/"):]

            quote = extract_ubg_quote(bible_dir / f"{target}.md")
            if quote:
                output_lines.append(f"> {quote}")

    return "\n".join(output_lines)


def main() -> int:
    repo_dir = Path(__file__).resolve().parent.parent
    source_topics_dir = repo_dir / "Topics"
    bible_dir = repo_dir / "Index" / "Biblia"
    output_topics_dir = repo_dir / "Index" / "Topics"

    if not source_topics_dir.is_dir():
        print(f"Missing source topics directory: {source_topics_dir}")
        return 1

    if output_topics_dir.exists():
        shutil.rmtree(output_topics_dir)

    output_topics_dir.mkdir(parents=True, exist_ok=True)

    generated_links: list[str] = []
    for source_file in sorted(source_topics_dir.rglob("*.md")):
        relative_path = source_file.relative_to(source_topics_dir)
        destination_file = output_topics_dir / relative_path
        destination_file.parent.mkdir(parents=True, exist_ok=True)
        destination_file.write_text(process_topic_file(source_file, bible_dir), encoding="utf-8")
        print(f"Saved topic: {relative_path.as_posix()}")
        generated_links.append(relative_path.as_posix())

    index_lines = ["# Topics", ""]
    for topic_link in generated_links:
        topic_title = Path(topic_link).stem
        topic_target = Path(topic_link).with_suffix("").as_posix()
        index_lines.append(f"- [[Topics/{topic_target}|{topic_title}]]")

    (output_topics_dir / "index.md").write_text("\n".join(index_lines) + "\n", encoding="utf-8")
    print("Topics regenerated.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
