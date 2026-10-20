## Usage

Generate the file-backed Bible data before building the application:

```bash
python3 convert-input-to-output.py
```

The generated `input/` directory remains at the repository root. The web
project links `input/**/*.yml` as content and copies it to `Bible/` beside the
application DLL during both IDE builds and `dotnet publish`. The container
therefore receives the same text data inside the image without a volume. Redis
is used for the independently maintained tag index. Rebuild or republish the
application after regenerating the YAML files.

Each converted verse is a direct YAML key/value pair:

```yaml
"1J5,12": "Whoever has the Son has life; whoever does not have the Son of God does not have life."
```

Raw source files live in `input-raw/`; generated, application-ready texts live in `input/`. Topic metadata is maintained independently in versioned Markdown files under `tags/`. A tag file uses Obsidian-style front matter with one fragment per list item, followed by its Markdown description:

```markdown
---
fragments:
  - Mt1,18-25
  - Łk2,1-20
---

# Birth of Jesus

Description of the tag.
```

Bible references use `<book><chapter>,<verse>` or a verse range. Church Fathers references are prefixed with the author, for example `Ignatius/Ef1,1`. The application parses these files, loads tag definitions and the fragment index into Redis, and displays linked tags above matching passages.

## Editing tags

Open `/login` manually; no navigation links lead to it. Enter a GitHub personal
access token into the field labeled `login`. Use a fine-grained token restricted
to this repository, with **Contents: Read and write** and the automatically
included Metadata access. The account must have repository write access and
the target branch must permit direct pushes. Branch protections are respected;
the application never force-pushes. Login stores a session without contacting
GitHub; every edit performs its own permission preflight and authenticated push.

After login, expand **Add tag** beside a verse or Church Fathers passage. Select
an existing tag from the suggestions or type a new name, then choose **Save**.
Tags identify passages independently of the translation; the comparison page
also supports editing. The affected tag list updates without navigating away.
New names accept letters, numbers, spaces, hyphens and underscores (80 characters
maximum). Existing descriptions and reference ranges are preserved. Adding a
reference that is already included, even through a range, is a no-op.

The browser receives only a random `HttpOnly`, `SameSite=Strict` session cookie.
Production cookies are also `Secure`: use the HTTPS Cloudflare URL for login,
not the HTTP host port. The token is encrypted with ASP.NET Data Protection
before being stored in Redis. Both Redis and the key ring have named volumes in
Compose so application rebuilds preserve sessions. Keep the key volume private
and back it up together with Redis; deleting it invalidates existing sessions.
Sessions expire after eight hours and can be revoked with **Log out** on `/login`.
Without Redis (local development/tests), sessions are held in memory and end on
restart. Tokens never go into browser storage, URLs, Git configuration or commits.

### Private repository credentials

Create a separate fine-grained GitHub token restricted to this repository with
**Contents: Read-only** (and the included Metadata access). Store just the token
in a file **outside the checkout**, not in this repository or in the Dockerfile.
Set its absolute path in the existing `.env` file, preserving the Cloudflare
setting:

```dotenv
KDJ_GITHUB_READ_TOKEN_FILE=/absolute/path/to/private-directory/github-read-token
```

The same variable can be exported in the environment. `.env.example` documents
the settings but contains no credentials. Do not paste the token itself into
`KDJ_GITHUB_READ_TOKEN_FILE`.

Compose mounts the file as a read-only secret for both the build and the running
application. The token is never a build argument, image environment variable,
remote URL or saved Git setting. Keep the parent directory accessible only to
your account (`0700`). With rootless Podman and the installed podman-compose
1.0.6, the file must be readable by the container's non-root `app` user: a `0444`
file inside that private directory works without exposing it to other host
users. This provider ignores secret `uid`/`mode` overrides. Do not place the file
in a shared directory. Rotate it in place, or recreate the container after
replacing the file, so its bind mount sees the new contents.

Continue updating with the existing single command:

```bash
./podman-up.sh
```

The script reads the path from `.env` (using python-dotenv, already required by
podman-compose) or the environment, runs a native Podman build with a secret
mount and caching disabled, and only then recreates the stack. A failed clone
or build leaves the running containers untouched. No real token is printed.
The build intentionally clones the latest remote `master`; it does not clone
uncommitted local tag edits.

On a modern Compose provider, `podman compose up --build -d` also supports this
file-backed secret and `build.no_cache`. With podman-compose 1.0.6 use the script:
that version ignores `build.no_cache` and can continue after a failed build.
This change does not alter GtdSystem's scheduling or add deployment retries.

The image contains Git and a disposable private clone in `/repository`, but no
credential file. Runtime synchronization uses the mounted readonly token to
fetch the current branch at startup, every 30 seconds, and before every edit.
An unreadable or invalid configured token fails closed; it never falls back to
a user's write credential. The write token is obtained only from the server-side
session identified by the HttpOnly cookie and is used for permission checks and
pushes. It is not supplied to the image build.

The application never runs code from the clone. Edits affect
only a validated `tags/*.md` file and follow this order:

1. Validate the session, CSRF token, tag name and existing passage.
2. Check GitHub repository write permission using the supplied token. This is a
   preliminary check, not a guarantee of token scope or branch-rule acceptance.
3. Fetch the branch using the readonly token, apply the addition, commit and
   push using the user's write token.
   Concurrent remote changes cause a fresh fetch and semantic retry, up to three
   attempts. Unconfirmed local commits are never published to the index.
4. Atomically replace the Redis catalog and refresh the affected UI tag list.

A successful push is already durable; rebuilding only incorporates the data in
the next image. If Redis fails after a push, the current process serves the
confirmed update from memory, the UI reports the index delay, and background
synchronization repairs Redis. Retrying an interrupted request is safe. A new
container never overwrites Redis with its older baked tag catalog. If GitHub is
unavailable, readers retain the last available catalog and edits report an error.

`compose.yaml` configures `TagEditing__RepositoryPath`, `TagEditing__Repository`,
`TagEditing__Branch`, `TagEditing__ReadTokenFile`, and `EditorSessions__KeyPath`.
Run a **single application
replica** with this configuration: its Git operations are serialized in-process.
Horizontal deployment would require distributed coordination for index updates.
Never point `RepositoryPath` at a developer checkout: synchronization deliberately
discards unpushed changes in this dedicated runtime clone. Outside Compose, an
empty repository path disables editing and the network synchronization worker.
The clone must also carry the local Git setting `kdj.editorClone=true`, set by
the image build. An ordinary developer checkout is rejected before any reset.

The backend tests use temporary local Git remotes and an isolated Redis process
(prepared by `scripts/setup-visual-tests.sh`), without GitHub credentials or real
remote writes. Browser tests cover login, editing controls and partial UI updates;
their successful write response is mocked. Actual GitHub push authorization must
be exercised with an appropriately scoped token after deployment.

The legacy `scripts/git-sync.sh` still calls a root `setup.sh` that is absent in
this checkout. If you use that script for automatic image rebuilds, repair its
deployment command separately. Tag synchronization and durability do not depend
on it: the running application already follows the remote tag files.

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

## Screenshot regression tests

The Playwright suite in `ProductionVisualTests/` builds the current checkout and
starts the application with an isolated Redis process. Application startup
recreates the tag index from the current checkout's versioned `tags/*.md`.
Editing synchronization is disabled in this isolated suite, so it never fetches
or writes to GitHub.
No container, production Redis connection or exported `dump.rdb` is needed.

Prepare dependencies once, then run the same command on a laptop or in Codex Web:

```bash
bash scripts/setup-visual-tests.sh --with-deps
bash scripts/test-screenshots.sh
```

Approved PNGs live in root-level `screenshots/baseline/`; failures produce actual
and diff images in `screenshots/results/` and an HTML report in
`screenshots/report/`. The GitHub Actions workflow is retained for manual runs only;
automatic pull-request runs are disabled.
See [screenshots/README.md](screenshots/README.md) for environment preparation,
Redis lifecycle, snapshot approval, external URL mode and the required merge check.

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
