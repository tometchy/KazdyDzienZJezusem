# Every Day With Jesus

## Repository data layout

Raw source publications live in `input-raw/`. Generate normalized application data with:

```bash
python3 convert-input-to-output.py
```

The converter writes `input/<translation>/<book>/<chapter>.yml` and
`input/Fathers/<author>/<work>/<chapter>.yml`. Each file is a simple YAML mapping;
there is deliberately no per-verse metadata:

```yaml
"1J5,12": "Kto ma Syna, ma to życie; kto nie ma Syna Bożego, nie ma tego życia."
"1J5,13": "To napisałem wam, którzy wierzycie w imię Syna Bożego, byście wiedzieli, że macie życie wieczne i abyście wierzyli w imię Syna Bożego."
```

Tags are versioned separately in `tags/`. The Markdown filename is the tag name.
A tag file has YAML front matter containing one fragment per line, followed by its
Markdown description:

```markdown
---
fragments:
  - "Mt1,18"
  - "Mt1,25"
  - "Fathers:Ignatius:Ef1,1"
---
# Birth of Jesus

Description of the tag.
```

Bible references use `<book><chapter>,<verse>`. Fathers references use
`Fathers:<author>:<work><chapter>,<section>`. At container startup, the application
parses these files, publishes the tag index to Redis, and uses the same index to
show linked tag names above passages and tag detail pages under `/Tagi/<name>`.

## Development

Run the automated checks with:

```bash
python3 -m unittest discover -s tests
dotnet test
```

The Compose stack contains the web application, Redis, and the Cloudflare tunnel.
