## Usage

Raw source publications live in `input-raw/`. Generate the application input before building:

```bash
python3 convert-input-to-output.py
```

The converter writes plain YAML mappings to `input/`:

```yaml
"1J5,12": "Kto ma Syna, ma to życie; kto nie ma Syna Bożego, nie ma tego życia."
"1J5,13": "To napisałem wam, którzy wierzycie w imię Syna Bożego, byście wiedzieli, że macie życie wieczne i abyście wierzyli w imię Syna Bożego."
```

Tags are maintained separately in the versioned `tags/` directory. The Markdown filename is the tag slug. Each file starts with Obsidian-style YAML front matter whose `fragments` array has one reference per line, followed by the tag description:

```markdown
---
fragments:
  - Bible:1J5,12
  - Fathers:Ignatius:Ef1,1
---

# Example tag

Tag description.
```

Bible references use `Bible:<book><chapter>,<verse>` and Church Father references use `Fathers:<author>:<work><chapter>,<section>`. During container startup the parsed tag definitions and reverse fragment index are loaded into Redis. They are also held in the application cache so chapter rendering does not perform a Redis round trip for every verse.

The web project packages `input/**/*.yml` and `tags/**/*.md` into the published application. Rebuild or republish after changing either directory.

Run the converter regression tests with:

```bash
python3 -m unittest discover -s tests -p 'test_*.py' -v
```

## Page snapshot tests

The xUnit tests verify the complete HTML of the Bible index, the
`/Biblia/TNP/1Kor/1` chapter page, a representative chapter from every
translation, and verse-comparison pages. The comparison coverage includes both
a verse available in every translation and one available in only some
translations. The same test methods and the same `.verified.html` files are used
in both modes.

The default mode starts the application in-process and replaces its Bible file
system with an in-memory stub, so no separately running application is needed:

```bash
dotnet test --project KazdyDzienZJezusem.Tests/KazdyDzienZJezusem.Tests.csproj
```

For E2E mode, start the current application from the IDE with its HTTP profile,
then point the tests at that address:

```bash
KDJ_E2E_BASE_URL=http://localhost:5267 \
  dotnet test --project KazdyDzienZJezusem.Tests/KazdyDzienZJezusem.Tests.csproj
```

The container published by `compose.yaml` is exposed on port `8081`, so the
equivalent command for an already running container is:

```bash
KDJ_E2E_BASE_URL=http://localhost:8081 \
  dotnet test --project KazdyDzienZJezusem.Tests/KazdyDzienZJezusem.Tests.csproj
```

When a page change is intentional, first run the deterministic stubbed mode,
review the generated `*.received.html` diff, and accept it as the new Verify
snapshot. E2E mode should only compare the running application with those
already accepted snapshots.

Install everything and start the stack:

```bash
./scripts/install-all.sh
```

Main setup script:

```bash
./setup.sh
```

Regenerate only topic markdowns, without Quartz or stack changes:

```bash
./setup.sh --topics-only
```

The Docker image now prebuilds the NT and topic HTML layers during `podman build`.
Runtime startup only copies the baked HTML into the mounted `IndexHtml/` directory and serves it.

Cloudflare Tunnel:

1. Create a tunnel in Cloudflare Zero Trust dashboard.
2. Copy the tunnel token into `.env.cloudflare` from `.env.cloudflare.example`.
3. In the tunnel dashboard, publish a hostname for `kazdydzienzjezusem.pl` or a subdomain of it and point the service to `http://kazdy-dzien:8080`.
4. Run:

```bash
./scripts/install-all.sh
```

ToDo:
- Word file on disk as metadata, but in yaml info addtional name for linking original wording
- fakty, wnioski, tezy, argumenty/kontrargumenty, komentarze Biblii, references
- Testy snapshotowe
- Redis port lokalnie i odpalanie z IDE
Najlepszym i powszechnie zalecanym sposobem w najnowszym .NET jest użycie szybkiej biblioteki Markdig oraz bezpieczne wyświetlenie wygenerowanego kodu w widoku Razor za pomocą @Html.Raw().1. Instalacja bibliotekiDodaj pakiet NuGet Markdig do swojego projektu ASP.NET Core MVC:bashdotnet add package Markdig
Używaj kodu z rozwagą.2. Konwersja w Kontrolerze lub ModeluPrzekształć tekst Markdown na ciąg HTML w kodzie C#:csharpusing Markdig;

public IActionResult Details()
{
string markdownText = "# Cześć\nTo jest **gruby** tekst.";
string htmlResult = Markdown.ToHtml(markdownText);

    // Przekaż htmlResult do widoku (np. przez ViewBag lub ViewModel)
    ViewBag.HtmlContent = htmlResult;
    return View();
}
Używaj kodu z rozwagą.3. Wyświetlenie w widoku Razor (.cshtml)Użyj metody Html.Raw, aby plik Razor nie uciekał (nie kodował) tagów HTML:html<div>
@Html.Raw(ViewBag.HtmlContent)
</div>
Używaj kodu z rozwagą.Zaawansowane opcje (np. wtyczki)Jeśli potrzebujesz obsługi tabel, emoji czy automatycznych linków, skonfiguruj potok (pipeline) w Markdig:csharpvar pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
string htmlResult = Markdown.ToHtml(markdownText, pipeline);
Używaj kodu z rozwagą.For tips on how to get the markdown conversion and rendering setup just right:14:34Markdown to HTML with C# .NETWyświetlenia: 1,8 tys. · 2 lata temuYouTube · Keep it simple, stupid.Jeśli chcesz, abym pomógł Ci dalej, napisz:Czy wczytujesz Markdown z pliku, czy z bazy danych?Czy potrzebujesz dodatkowych funkcji, takich jak czyszczenie kodu HTML (sanitization) ze względów bezpieczeństwa?
