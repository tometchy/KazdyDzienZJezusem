# Repository audit and parity baseline

The initial audit used `a25f2c4`. On 2026-11-19 we checked the fetched [master revision](https://github.com/tometchy/KazdyDzienZJezusem/commit/fe4322096769fe01b2e23fb6efa7594cfd6f13b5). The intervening revisions added planning documents and edited an editorial note. The reader, workflows and corpus used by the audit have identical source trees at those revisions. We recounted source files, Bible references and `poc.prepare.records()` in memory at the new revision. No target benchmark or deployment is part of this audit.

## Current visual baseline

On 19 November the prescribed comparison built the reader at `fe43220` and verified its isolated Redis indexes. Of 57 Chromium scenarios, 32 passed and 25 failed. In 23 cases the rendered image differed from the committed baseline. For example, the Fathers index now includes additional Greek reading controls. Review the accepted appearance before recording M01 baselines. [Committed baselines](../../screenshots/baseline).

The other two failures exercise 1 Thessalonians 4:16 on desktop and mobile. The desktop Playwright trace contains the word, its visible morphology layer and the N-DSF explanation. Its `data-greek-original` preserves the source alpha with oxia (U+1F71), while the test selector uses alpha with tonos (U+03AC). They normalize to the same NFC string but differ in an exact CSS attribute match. In M01, locate the token by reference and position or compare its normalized spelling, then keep the original display-text and morphology assertions. [Source verse](../../input/TR/1Tes/4.yml) · [Current scenario](../../ProductionVisualTests/tests/greek.navigation.visual.spec.ts) · [Rendering code](../../KazdyDzienZJezusem/wwwroot/js/greek-interlinear.js).

## Actual application and features

The production application is ASP.NET Core MVC with Razor, Bootstrap, Unpoly 3.14.3, local YAML Bible data and Redis-backed dictionary, tag index and editor sessions. [Program.cs](../../KazdyDzienZJezusem/Program.cs), [project file](../../KazdyDzienZJezusem/KazdyDzienZJezusem.csproj), [layout](../../KazdyDzienZJezusem/Views/Shared/_Layout.cshtml) and [README](../../README.md) establish the runtime boundaries. This application is not currently an offline reader or an OAuth client.

In this table, “offline” describes the target native application with its public snapshot installed. Production's HTTP backend is still needed today. “Public” means no account required. All target behavior tests run in PL and EN; source texts are never translated as UI strings.

| Current route/behavior and evidence | Target owner and parity test | Account / target offline |
| --- | --- | --- |
| `/` redirects to `/Biblia`; `/Biblia`, `/Biblia/{translation}`, `/Biblia/{translation}/{book}`, `/Biblia/{translation}/{book}/{chapter}` enumerate actual available sources/books/chapters; missing content is 404. [BibleController](../../KazdyDzienZJezusem/Controllers/BibleController.cs), [BibleRepository](../../KazdyDzienZJezusem/Services/BibleRepository.cs) | Shared JS route/reference rules and reader templates; compare every available book/chapter inventory and representative invalid paths | Public / yes |
| Chapter previous/next, verse-number links, chapter anchor, breadcrumb source switch and retained verse fragment. [Chapter](../../KazdyDzienZJezusem/Views/Bible/Chapter.cshtml), [breadcrumb](../../KazdyDzienZJezusem/Views/Bible/_TranslationBreadcrumb.cshtml), [site.js](../../KazdyDzienZJezusem/wwwroot/js/site.js) | Unpoly fragments, independent history/scroll per tab; test direct URL, refresh, source switch and back/forward without full reload | Public / yes |
| `/Biblia/Werset/{book}/{chapter}/{verse}?sourceTranslation=...` compares all four sources; unavailable text is an explicit message, not a fabricated verse; chosen source controls return navigation. [Verse](../../KazdyDzienZJezusem/Views/Bible/Verse.cshtml) | One canonical comparison route per locale and reference; source query is navigation state, excluded from canonical; full/partial coverage tests | Public / yes |
| `/Ojcowie`, `/{author}`, `/{author}/{work}`, `/{author}/{work}/{chapter}` with chapter **zero**, verse anchors and previous/next. [FathersController](../../KazdyDzienZJezusem/Controllers/FathersController.cs), [FathersRepository](../../KazdyDzienZJezusem/Services/FathersRepository.cs) | Same shared reader with work identity; test seven introductions and transitions from 0 to 1 | Public / yes |
| `/Tagi`, `/Tag/{slug}`, Markdown descriptions, expanded ranges, unavailable references, Father passages and translation picker. [TagRepository](../../KazdyDzienZJezusem/Services/TagRepository.cs), [TagPassageReader](../../KazdyDzienZJezusem/Services/TagPassageReader.cs), [tag view](../../KazdyDzienZJezusem/Views/Tags/Details.cshtml) | `tags`, ranges and expanded relation tables; strict whole-range availability, default TNP then UBG, UBG fallback for absent translated ranges; per-tag picker preference, sanitized Markdown | Public / yes |
| `/api/tags` catalog and `/api/tags/fragment` lookup; adding a tag beside Bible/Father/comparison passages updates only its list. [TagsController](../../KazdyDzienZJezusem/Controllers/TagsController.cs), [TagFileEditor](../../KazdyDzienZJezusem/Services/TagFileEditor.cs), [FragmentValidator](../../KazdyDzienZJezusem/Services/FragmentValidator.cs) | Public SQL contracts for reads; native JS REST sync for writes. Preserve valid-name rules, existing descriptions/ranges and idempotent duplicate additions | Public reads; effective GitHub write for shared edits / queued changes only offline |
| Hidden `/login` takes a PAT in a field called `login`; it does not validate GitHub identity at login. `/logout` and `/api/editor/session` support HttpOnly session + CSRF. [LoginController](../../KazdyDzienZJezusem/Controllers/LoginController.cs), [EditorSessions](../../KazdyDzienZJezusem/Services/EditorSessions.cs) | Deliberately replaced by native GitHub App authorization; Pages has no login/editor. PAT/session-cookie compatibility is not retained. Test anonymous, authorized and revoked roles | Native login needs network |
| Five independent Greek layers: contextual Polish, Strong+morphology Polish, English, grammar and pronunciation; preferences in localStorage today. Greek is annotated in chapter, comparison, Father and tag views, with unknown/ambiguous words, dictionary-marker explanation, help and word-detail modals. [greek-interlinear.js](../../KazdyDzienZJezusem/wwwroot/js/greek-interlinear.js), [greek-grammar.js](../../KazdyDzienZJezusem/wwwroot/js/greek-grammar.js) | Shared JS domain logic plus raw source-preserving SQLite; same order and restoration of unannotated text, focus/keyboard/tooltip parity; persist defaults independently of private drafts and keep active controls tab-local | Public / yes |
| `POST /api/greek-interlinear` accepts text, context, reference and optional morphology; validates bounded matching context, adds Strong analysis when requested, returns 400/503 on failure. [controller](../../KazdyDzienZJezusem/Controllers/GreekInterlinearController.cs), [service](../../KazdyDzienZJezusem/Services/GreekInterlinearService.cs) | Named local `getInterlinear` / morphology contracts, same bounds and meaningful local failures; no public runtime API required | Public / yes |
| `/Teksty` (“Literature”) exists but contains only its heading; `/Error` is a diagnostic error page. [HomeController](../../KazdyDzienZJezusem/Controllers/HomeController.cs), [Teksty view](../../KazdyDzienZJezusem/Views/Home/Teksty.cshtml) | Keep route compatibility and honest empty state; localized error/not-found screens. Do not claim an existing literature catalog | Public / yes |
| The standalone [parallel PDF reader](../../docs/compare.html) takes two HTTPS PDF URLs, converts GitHub blob links to raw links, and keeps the selected page, side-by-side/stacked layout and swapped order in localStorage. It has no MVC route or link from the empty Literature screen. | Retain access to the tool. A fixture with two short PDFs exercises page clamp at 1, next/previous, swap, small screen and reload. Test embedding and a direct-open alternative on WebViews without an inline PDF renderer. | Public / external PDF bytes require network unless already available locally |
| `/POC` is a separate benchmark UI, FTS search, strategy/reset controls and downloadable reports. [POC README](../../poc/README.md), [frontend](../../poc/frontend/main.js) | Search becomes a normal shared reader feature; benchmark UI is retired only after evidence is archived. Exported diagnostic reports are not user notes | Public / yes for native FTS |
| Private Markdown Notes, App download tab, PL/EN UI and installed-app updater do **not** exist in production | New capabilities, each requiring its own acceptance tests; never label them existing parity | See auth/release plans |

Tag editing currently performs permission preflight, fetch, semantic append, commit and authenticated push in a dedicated runtime clone. It retries concurrent updates, publishes only confirmed state and repairs a delayed Redis index; details are in [TagGitStore](../../KazdyDzienZJezusem/Services/TagGitStore.cs), [TagEditingService](../../KazdyDzienZJezusem/Services/TagEditingService.cs) and [GitHubWriteAccess](../../KazdyDzienZJezusem/Services/GitHubWriteAccess.cs). The JS REST replacement must preserve these semantic protections, not carry over Git CLI, PATs, Redis or server sessions.

## Measured data inventory

| Dataset | Books/works | Chapter YAML files | Records in current POC export |
| --- | ---: | ---: | ---: |
| TNP | 47 | 589 | 14,196 |
| UBG | 66 | 1,189 | 31,102 |
| KJV | 66 | 1,189 | 31,102 |
| TR | 27 | 260 | 7,957 |
| Fathers / Ignatius | 7 | 98 | 205 |
| Interlinear | 7,940 passage records | Not YAML | 7,940 |
| Dictionary search documents | Surface-form export, not normalized production keys | Not YAML | 22,815 |
| **Total** | | | **115,317** |

The union of actual Bible references is **31,102**; source-specific verse rows total **84,357**. Their display text totals **11,316,694 UTF-8 bytes** before HTML escaping. `input/` has 3,328 files / 15,069,480 bytes; `input-raw/` has 12 files / 245,851,108 bytes. [tags/narodzenie-jezusa.md](../../tags/narodzenie-jezusa.md) is the only tag file: 113 bytes, two ranges (`Mt1,18-25`, `Łk2,1-20`), 28 distinct expanded verses. All seven Father works have chapter zero.

Editorial content outside the reader matters during cleanup. Git tracks four files in `Notes/` **inside this public project**, 23 in `Topics/`, two in `Info/`, one in `Commentarii/` and 38 in `Index/` at this revision. The new personal private GitHub repository named `Notes` is a separate repository. Never auto-import these folders into a user account. Preserve their bytes and links. [regenerate-topics.py](../../scripts/regenerate-topics.py) still consumes `Topics/` and `Index/Biblia/` and writes `Index/Topics/`. Exclude these editorial and derived files from the snapshot and golden reference unless an individually licensed reader feature explicitly consumes them.

The source TNP EPUB contains exactly the same 47-book selection, including wisdom books, Megillot and minor prophets. Its omitted UBG books are `1Krl, 1Krn, 1Sm, 2Krl, 2Krn, 2Sm, Dn, Ez, Ezd, Iz, Joz, Jr, Kpł, Lb, Ne, Pwt, Rdz, Sdz, Wj`. This is source coverage, not permission to synthesize missing books.

The primary [Greek data](../../input/GreekPolish/README.md) has 7,940 passage rows, 137,323 aligned word pairs and 520 explicit alias rows; production derives 18,433 normalized primary form keys plus 2,831 supplement keys = 21,264. `input-raw/gnt.flat.json` independently has 7,957 verses and **140,628 word occurrences** with `greek`, `strong`, `dictionary_form`, `grammar`, `grammar_human`, `definition`, `transliteration`. These belong to different source editions and cannot be equated position by position. Preserve all fields, even those the current UI truncates or does not display.

`poc/prepare.py` deliberately flattens passage arrays and bounds dictionary alternatives to twelve; `poc/build_sqlite.py` stores only normalized `docs_fts(source,ref,pl,gr,en,lemma)` plus `meta`. It omits public tags and rich grammatical alignment. **That FTS snapshot cannot be the canonical source of the migrated reader.** The prompt's 33.36 MB database and Pixel timing report are historical evidence: no checked-in raw `.sqlite3` or named `kdzj-tauri-postmarketos-1794824516500.json` report was found to independently remeasure them. Do not present the data-file size or 67.295 ms warm DB preparation as freshly measured application startup.

## TNP footnotes and Jesus quotations in the source edition

[The checked-in TNP EPUB](../../input-raw/Biblia_przeklad_Torunski.epub) contains information that the current YAML cannot recover. The archive's `tnp.css` identifies `.jq` as a Jesus quotation and assigns it bold weight. Only text within the edition's marked ranges qualifies for this emphasis. `.ca` denotes an inline comment marker, and the verse's `.ct` contains the publisher's explanations. The source marker generally has no hyperlink. `.rf` holds separate scripture cross-references and `.st` carries source annotations such as Strong numbers. [The converter's `read_tnp`](../../convert-input-to-output.py) excludes `ca`, `ct`, `rf` and `st`, flattens the remaining descendants including `jq`, strips soft hyphens, normalizes whitespace and removes the verse-number prefix. The resulting [John 3 YAML](../../input/TNP/J/3.yml) stores one string per verse.

On 2026-11-19 a read-only census parsed the XHTML book members selected by the converter, scoped to nonzero verse elements matching `v{chapter}.{verse}`, and counted actual descendant class markers. Across **14,196 verses**, **11,908** have a nonempty explanation block. There are **31,198 inline `.ca` occurrences**, of which **27,337 consist only of asterisks** (one through nine). The other **3,861** include labels such as `CW` and `|INF`. **2,067 `.jq` ranges in 2,054 verses** identify the source's Jesus quotations. These measurements count source occurrences. A reviewed marker-to-body inventory is needed to determine the number of distinct note targets for both the golden ZIP and static pages.

| Source fixture | Expected reader behavior and data check |
| --- | --- |
| `John.html#v3.16` (J 3:16) | Two markers, `*` and `**`, inside one `.jq` point to two different explanations in one `.ct`. The quotation has a nested `.df` marking added text. Both star links open the corresponding original note. Preserve the inherited bold through the inner style. |
| `John.html#v1.38` (J 1:38) | Only Jesus' short question inside `.jq` appears bold. The narrator's lead-in and the disciples' reply stay in their source styles. The later star belongs to the reply. |
| `Ruth.html#v2.4` (Rt 2:4) | There are two `**` markers for one explanatory body and a separate `\|PAR` marker. Preserve each occurrence's position and link repeated markers to the same approved target. |
| `Ruth.html#v1.10` and `John.html#v1.2` | Rt 1:10 has two inline stars of different lengths. A mention of `*****` inside the second explanation refers to another verse and creates no third local note. J 1:2 has an empty `.ct` and requires no star or footnote page. |

These are public reading features, available offline in Tauri and as ordinary links in static HTML, including within a comparison or tag passage. Polish editorial notes retain their language in the English interface. Approve rights for both the verse and the publisher's full note text before packing it into SQL, a public reference ZIP or static detail pages. Record the existing electronic-edition grant and confirm its coverage for the intended packaging and source-preserving presentation. An unresolved mapping or grant is a named publication gate for the affected edition. See the [source-run and footnote contracts](data-and-sync.md).

## Greek semantics requiring explicit contract tests

[GreekText](../../KazdyDzienZJezusem/Services/GreekInterlinearService.cs) defines distinct operations:

- `FormKey`: lowercase, NFD, grave-to-acute, NFC, final-sigma fold. Breathing and iota subscript remain meaningful.
- `AlignmentKey`: removes nonspacing marks only for passage alignment. Longest-common-subsequence alignment requires at least `min(3, wordCount)` matches and 65% coverage, with deterministic tie-breaking. Pronunciation cannot be reused from a differently spelled aligned token without exact-form lookup.
- POC search normalization removes **all** combining mark categories, folds `ł` to `l`, and final sigma. This belongs only in search columns; original displayed Greek must remain byte-equivalent under the approved text normalization policy.

[GreekDictionaryRepository](../../KazdyDzienZJezusem/Services/GreekDictionaryRepository.cs) validates aligned array lengths, changes a missing `-` gloss to an em dash for presentation, never overwrites primary forms with supplement entries, and keeps alternatives. [GreekMorphologyService](../../KazdyDzienZJezusem/Services/GreekMorphologyService.cs) requires an attested Strong identity **and** exact grammar; ambiguous form/grammar-to-Strong crosswalks are excluded. It bounds compatible forms to 32, tries the actual form first, ranks candidate Polish glosses deterministically and never substitutes a declining lemma meaning for an inflection. Existing 240-character definition truncation is presentation policy, not a reason to truncate canonical data.

Port fixtures for accents/breathings/iota subscript, repeated words, movable nu, misleading reference, short verse threshold, unknown form, ambiguous gloss, mismatched grammar and the distinction between gloss and dictionary meaning. Current regression evidence lives in [GreekInterlinearTests](../../KazdyDzienZJezusem.Tests/GreekInterlinearTests.cs), [GreekMorphologyTests](../../KazdyDzienZJezusem.Tests/GreekMorphologyTests.cs), [dictionary tests](../../KazdyDzienZJezusem.Tests/GreekDictionaryRedisTests.cs) and [grammar tests](../../tests/greek_grammar.test.cjs).

## Provenance and distribution gates

These are evidence-based release questions, not a legal determination. A code license never relicenses source texts. Preserve separate credits and source fingerprints. Obtain owner review before redistributing a new artifact where the recorded grant does not clearly cover it.

| Source | Evidence in checkout | Planned gate |
| --- | --- | --- |
| Application code | Root [LICENSE](../../LICENSE) is GPLv3 text | Audit dependency notices and store-distribution compatibility; do not assume app-store terms satisfy all obligations |
| TNP EPUB | `intro.html` inside `input-raw/Biblia_przeklad_Torunski.epub`: seventh corrected edition, 2022-06-01, ISBN 978-83-963342-2-0; grants electronic copying/distribution without financial gain and without changing text. OPF title is merely `bpt`, author `Unknown` | Retain unchanged text and attribution; review converter transformations, offline bundle, public ZIP and store packaging against that grant; no paid reuse assumption |
| UBG EPUB | `OEBPS/Text/copyrights.xhtml`: ebook v1.5, ISBN 978-83-63837-92-1; © 2017 Fundacja Wrota Nadziei, electronic edition © 2017–2024 Logos Media, all rights reserved. OPF describes 2017 text, date 2019-01-29; filename `UBG_2025.epub` is not a license/version declaration | Obtain evidence of permission for public ZIP/SQLite, full-text static HTML and installer redistribution; current visible attribution alone is insufficient |
| KJV | `input-raw/verses-1769.json`; converter and reader identify KJV, but no dedicated provenance/license file was located | Record exact upstream source, edition and applicable territorial redistribution permissions before packaging |
| TR, Strong and bundled GNT definitions | `input-raw/gnt.flat.json` and sibling exports; no dedicated license grant located | Trace original text/annotations/definitions separately; do not presume ancient text makes modern annotations unrestricted |
| Primary Greek-Polish | [README](../../input/GreekPolish/README.md): Maciej Rapacz revision `4c842b2622915d608482ece200a352779110ddd8`, CC BY-NC-SA 4.0; underlying Oblubienica/BibleHub/NA27/NA28 provenance recorded | Preserve NC/SA notices and modifications; confirm distribution scope for bundled/store artifacts, never relabel under GPL |
| Supplement | Same README: MACULA CC BY 4.0, Berean gloss public-domain declaration, Wiktionary CC BY-SA 4.0, Morpheus CC BY-SA 3.0 US, LSJ/Logeion CC BY-SA 4.0; bundled GNT retains unresolved original terms | Per-entry provenance/license, all source notices, no unrestricted blanket license; no Grepl scraping |
| Ignatius | [license](../../input-raw/ignatius-seven-letters.LICENSE.md): Kirsopp Lake 1912 / Perseus / First1KGreek CC BY-SA 4.0, pinned upstream revision | Retain notices and transformation record; fix stale `output/Fathers` / `input` paths when reorganizing documentation later |
| Tags | One versioned Markdown file; no distinct content grant | Confirm contributor/content terms; keep front matter, Markdown and provenance intact |

For the first data PR record the decision for each channel below in the source manifest, with the grant, territory, version, required credits and reviewer. “Hold” requires more evidence before this **new** distribution. “Conditional” requires meeting the cited grant and checking underlying materials. This matrix describes the proposed release gate.

| Source | Full verse/entry HTML on Pages | Public search index / full reference ZIP | Offline download / stores |
| --- | --- | --- | --- |
| TNP | Conditional on preserving the authorized text and attribution. The existing Razor notice credits permission from Świadome Chrześcijaństwo. | Confirm the noncommercial grant covers full machine-readable distribution and the proposed reference contents. | Confirm the noncommercial grant covers each channel and store terms. |
| UBG, KJV, TR, GNT/Strong | Hold for source-specific proof and applicable territories. | Hold. Use reviewed per-field digests in the ZIP only where legally appropriate. | Hold until permission covers bundling and redistribution. |
| Primary Greek-Polish and supplement | Conditional per underlying entry, provenance, attribution and license. Hold unresolved GNT records. | Same entry-level gate for the public projection and ZIP. | Check NC/SA and store terms per component, with separate notices. |
| Ignatius TEI and derived passages | Conditional on credit, change notice and retained CC BY-SA terms. | Carry notices and provenance through the ZIP and search projection. | Preserve license text and assess the selected distribution terms. |
| Editorial tags | Confirm publication rights for Markdown descriptions and embedded quotations. | Confirm coverage for retained content and ranges. | Confirm redistribution before bundling. |

Audited SHA-256 fingerprints: TNP EPUB `48fa9788f81a8c5b926f092d74a404cc06d13857ff6e6296ea9b7207d9ab077d`; UBG EPUB `a9ff9f6797add39755161efee40e63e694d25722aafbffc45a3cb277c22d3aed`; KJV JSON `43fbd2fd6a7aebaf62c0c828a85072336143c1fe8233a4856a12db1d5e5df470`; GNT JSON `e61655e16e3c2d4078d4d0ae2fe063c3b9f90b109ede894a08d5f825b67f0b0b`.

Unclear rights block the affected new distribution, not documentation. A per-record digest baseline avoids republishing protected text in the ZIP; it does **not** grant permission to put that same text in Pages or installers. If rights cannot be established, retain the existing service and report the parity/cutover blocker; never silently drop a translation.

## Existing tests, CI and experiments

- [ProductionVisualTests](../../ProductionVisualTests/) is Playwright Test with TypeScript today, not CodeceptJS. [screenshots.yml](../../.github/workflows/screenshots.yml) is manual only. The new JS-only CodeceptJS plan must progressively port the scenarios, preserving current baselines until approved replacements exist.
- [poc-sqlite.yml](../../.github/workflows/poc-sqlite.yml) runs on path-filtered PRs and manual dispatch. It checks broad counts/selected searches and Chromium OPFS/SAH Pool smoke, but is not an independent 100% golden reference comparison.
- [poc-pages.yml](../../.github/workflows/poc-pages.yml) is manual, hardcodes `/KazdyDzienZJezusem/POC/`, and publishes the benchmark with a root redirect. It generates no per-verse SEO pages.
- [tauri-postmarketos.yml](../../.github/workflows/tauri-postmarketos.yml) builds on ARM hosted Ubuntu inside Alpine 3.24, runs a native SQLite self-test, and uploads a musl ELF archive with 14-day artifact retention. This is neither Android APK, Flatpak, signed updater release, nor a physical Pixel UI test.
- Production adds COOP and COEP headers under `/POC` in [Program.cs](../../KazdyDzienZJezusem/Program.cs). The standard SQLite WASM `opfs` VFS needs cross-origin isolation. A Pages build cannot assume that ASP.NET header configuration. Probe capabilities inside the actual deployed worker. The preferred `opfs-sahpool` VFS can work without those headers, but still needs working synchronous access handles and explicit coordination across tabs. Verify the memory fallback on the same Pages origin. [SQLite persistent storage options](https://sqlite.org/wasm/doc/trunk/persistence.md).
- [sqlite-worker.js](../../poc/frontend/sqlite-worker.js) already serializes storage with Web Locks and calls `pauseVfs()` after closing handles; [sahpool-utils.js](../../poc/frontend/sahpool-utils.js) handles absent files. Reuse this evidence, but version-aware snapshot coordination and failure/recovery tests still need design.
- [Tauri config](../../poc/frontend/src-tauri/tauri.conf.json) uses benchmark identifier `pl.kazdydzienzjezusem.benchmark`; native commands are benchmark/report commands. It proves native FTS can run, not production DB contracts, IDB persistence, OAuth or updater support.
- Python converter/supplement tests, xUnit repository/HTML/Redis/Git tests, JS grammar tests and screenshot coverage are useful existing evidence. Keep equivalent assertions in the new architecture; no POC or backend removal until parity gates pass.

The prompt's Pixel result (4/4, cold DB preparation 758.715 ms; warm 67.643/63.642/67.295 ms; warm SQL p50 0.122 ms, SQLite 3.50.2) remains explicitly **reported historical evidence**, not independently reproduced here. Missing OPFS/SAH in that WebKitGTK does not establish anything about IndexedDB durability.

## Recent pull requests checked against actual state

On 2026-11-19 the [public PR API](https://api.github.com/repos/tometchy/KazdyDzienZJezusem/pulls?state=all) returned #20 as the newest PR. We checked the merge timestamp, base branch and changed-file list for each of #13–20 against the implementation. All eight were merged to `master`.

| PR | Current status / actual scope |
| --- | --- |
| [#13](https://github.com/tometchy/KazdyDzienZJezusem/pull/13/files) | Bundled worker asset URL resolution and build/smoke checks for the missing manifest |
| [#14](https://github.com/tometchy/KazdyDzienZJezusem/pull/14) | Merged to master; SQLite builder/exporter, worker and POC CI, not the full reader migration |
| [#15](https://github.com/tometchy/KazdyDzienZJezusem/pull/15/files) | One-click cold and three warm A/B/C runs, aggregate JSON, worker and browser smoke coverage |
| [#16](https://github.com/tometchy/KazdyDzienZJezusem/pull/16) | Merged; worker/full benchmark/browser-smoke multi-tab changes |
| [#17](https://github.com/tometchy/KazdyDzienZJezusem/pull/17) | Merged; SAH Pool missing-file utility and tests |
| [#18](https://github.com/tometchy/KazdyDzienZJezusem/pull/18) | Merged; Tauri benchmark, native musl workflow, Pages workflow, URL configuration and Pixel installer |
| [#19](https://github.com/tometchy/KazdyDzienZJezusem/pull/19) | Merged; native benchmark report handling/tests |
| [#20](https://github.com/tometchy/KazdyDzienZJezusem/pull/20) | Merged on November 16; Strong/morphology service, controller, grammar/interlinear JS, layout and tests. Its body still describes draft work; current merged metadata and code take precedence |

The README also retains obsolete `setup.sh`, Quartz/prebuilt-HTML and output-folder descriptions. Treat running code and current workflow files as authoritative; cleaning unrelated documentation is outside this planning change.
