## Usage

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