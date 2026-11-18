# Static SEO pages and complete data verification

This design is based on the [measured repository audit](repository-audit.md) and [canonical schema](data-and-sync.md). No generator, golden ZIP or timing experiment is implemented in this documentation change. The full-data <=60-second gate and the Pages size/deployment gate are acceptance requirements whose measurements remain pending.

## URLs and rendering without JavaScript

Generate actual `index.html` files beneath each route. Use the same pure JS view renderer in Actions and the running app; a build-time data adapter supplies the same view model as browser/native SQL adapters. The output is usable HTML with text, links, headings, attribution, `lang`, title, description, canonical and reciprocal PL/EN links before any JS or SQLite download. Scripts enhance fragment navigation with Unpoly. Search needs JS; reading an indexed public page does not.

| Page family | Files and canonical policy |
| --- | --- |
| Start, help, App downloads | `/<locale>/index.html`, `/<locale>/Help/index.html`, `/<locale>/App/index.html` |
| Source/books/chapters | `/<locale>/Biblia/`, `/<locale>/Biblia/{translation}/`, `/<locale>/Biblia/{translation}/{book}/`, `/<locale>/Biblia/{translation}/{book}/{chapter}/` |
| Verse comparison | `/<locale>/Biblia/Werset/{book}/{chapter}/{verse}/index.html`, one comparison of all available sources per reference and locale |
| Fathers | `/<locale>/Ojcowie/{author}/{work}/{chapter}/`, plus catalog levels; chapter zero is valid |
| Tags | `/<locale>/Tagi/`, `/<locale>/Tag/{slug}/`; actual descriptions and linked passages, sanitized |
| Dictionary | `/<locale>/Dictionary/` and stable entry-ID pages for licensed entries; displayed labels are Greek forms/lemmas, never unstable form strings as filesystem identifiers |
| Invalid route | Real `404.html` and 404 status; no blanket SPA fallback or hash router |

Use the deployed public `pagesBase` from `app-config.json` everywhere, including assets, canonical, sitemap and legacy links. Example for a project site: `/KazdyDzienZJezusem/en/Biblia/Werset/J/1/1/`. Test a custom-domain root and a project subpath; neither may assume `/assets` is at domain root. Percent-encode Unicode book names by path segment, preserve case and trailing-slash convention, and never produce both encoded and decoded aliases in the sitemap.

There are **31,102 logical comparison references**, hence **62,204 PL+EN verse pages**, not one page for each of 84,357 source-specific verse rows. Both localized pages are self-canonical, link to one another with reciprocal `hreflang=pl/en`, and offer a stable `x-default` landing policy. This retains translated navigation, metadata and explanatory labels while leaving scripture untouched. It does duplicate the unchanged source text twice; that cost is explicit below. No extra pages are generated per annotation-layer combination, source-selection query, scroll position or modal. Query variants point to the locale's clean canonical URL.

Google supports alternate-language annotations even when only surrounding templates are translated, but may still consider untranslated main content duplicate. A canonical is a preference, not a guarantee of indexing. Keep same-language canonicals consistent with sitemaps and internal links; do not claim that `hreflang`, `lang` or `robots.txt` forces indexing. [Localized versions](https://developers.google.com/search/docs/specialty/international/localized-versions), [canonical guidance](https://developers.google.com/search/docs/crawling-indexing/consolidate-duplicate-urls).

Generate small **unprefixed legacy alias files** for existing public routes, each with a visible link and immediate HTML refresh to its PL destination plus a canonical. GitHub Pages does not provide arbitrary server-side 301 rules, so describe these honestly as static redirects. Preserve `#verse` and supported query context in enhancement JS; a no-JS alias must still reach the actual text. Keep legacy comparison and chapter aliases in the byte budget, exclude aliases from sitemaps. The old production hostname must remain under owner control and point to the replacement static site at cutover; a new Pages project URL alone cannot preserve links on an abandoned hostname. No Worker or redirect backend is proposed.

After bootstrap, Unpoly replaces `#main-content` and the relevant modal/region without full document navigation; route/head metadata changes follow the same manifest. Each tab has independent URL, scroll, selection, filters and history. Locale switch preserves the logical reference and local draft while changing the locale path and UI. Native deep links resolve this logical route locally, independently of remote Pages availability.

## Pages feasibility: evidence, budget and gate

The current checkout has 3,227 source chapter YAML files, 206 source-book pages, four source indexes, 98 Father chapters, seven works and one author, one tag, and 31,102 verse references. The basic inventory before dictionary/help/App pages is therefore approximately **69,300 localized public pages**, plus approximately **34,650 legacy aliases** if every existing route is retained. A precise route manifest in the generator will replace this rounded figure. Do not count the same old verse route four times.

The measured 11,316,694 bytes of verse text become 22,633,388 bytes when included in both comparison locales, before markup and HTML escaping. Chapter pages repeat the same source text, so reserve another approximately 22.6 MB plus markup. The existing FTS file's historical 33.36 MB is neither a measurement of the future relational schema nor the whole website. Images/fonts, dictionary pages, aliases, compressed SQLite, sitemaps and HTML all count.

GitHub currently limits a published Pages site to 1 GB, times out a Pages deployment after ten minutes, and gives a soft 100 GB monthly bandwidth allowance. Its source-repository 1 GB recommendation is separate from the published-site limit; custom Actions publishing avoids the soft ten-builds-per-hour limit. These are service limits, not proof that an arbitrary 69,000-page site fits. [Official Pages limits](https://docs.github.com/en/pages/getting-started-with-github-pages/github-pages-limits).

Use conservative decimal bytes in the initial plan:

```text
Nverse = 62,204
Ssite = sum(actual UTF-8 bytes of every published file)
      = Nverse * weightedMeanVerseHtmlBytes
        + Schapters + Sindexes + Sfathers + Stags + Sdictionary
        + Saliases + Sassets + Sdatabase + Ssitemaps
Internal release target: Ssite <= 800,000,000 bytes (headroom below 1 GB)
If non-verse files reserve 150,000,000 bytes:
  weightedMeanVerseHtmlBytes <= 650,000,000 / 62,204 = about 10,449 bytes
```

The 150 MB reserve is an **assumption**, to be replaced by actual artifact inventory. At 8,000 bytes per localized verse page, the verse portion is 497,632,000 bytes; at 12,000 it is 746,448,000 bytes, already consuming almost all headroom. A 400-byte unprefixed comparison alias costs another 12,440,800 bytes; 400 bytes is also only an illustrative target. Dictionary entry pages can materially change the result. Do not inflate every page with embedded fonts, large data JSON, all Greek analyses or duplicated CSS.

**Decision:** per-verse PL+EN static HTML is plausible with small templates and shared assets; it is not yet demonstrated within Pages limits. Retain it as the intended design behind a mandatory measurement gate. The low raw text size supports proceeding to the experiment, not declaring it passed.

The future 1,000-reference experiment must:

1. Select deterministic, stratified **real** references across every book, OT/NT, long/short text, missing translations, Greek, Unicode and tagged verses. Record selection seed and strata weights; render **both locales** using the proposed real renderer, not blank/synthetic pages.
2. Measure per-page mean/p50/p95/max UTF-8 bytes, total bytes, escaped-text overhead, generated-file count and wall-clock time on a specified hosted runner. Fit weighted estimates for all 31,102 references; render all chapter/catalog/Father/tag pages and estimate licensed dictionary entries independently.
3. Record predicted total including legacy aliases/assets/full snapshot, then generate the full artifact in the next gated PR. A sample cannot establish actual Pages deployment duration. Measure generation, packaging/upload and deployment separately; ten minutes is the Pages deployment ceiling, not a claim that every Actions step shares that limit.
4. Require full artifact <=800 MB, successful full publish rehearsal below ten minutes, all expected paths present and no-JS direct-link checks. Publish measured JSON/Markdown evidence, not only a screenshot of the first page.
5. If over budget, first share/minify templates/assets, remove redundant per-source/per-option verse copies and reduce unnecessary dictionary aliases. Recompute. If full bilingual required content still exceeds limits, block Pages cutover and ask the owner to choose an explicit scope/hosting decision; keep the current service during planning. Never silently replace verse HTML with JS-only content or invent a runtime proxy.

Download demand is a separate risk: a 10 MB compressed DB downloaded 10,000 times is roughly 100 GB before HTML/assets. That is an illustrative calculation, not the current compressed size. Use immutable URLs and browser caching, publish downloadable packages/snapshots on Releases where appropriate, measure transfer size and demand, and do not promise an unlimited free CDN. The web reader must remain usable from static HTML if the DB download fails.

## Sitemaps and SEO checks

A single sitemap supports at most 50,000 URLs or 50 MB uncompressed. Split early at 40,000 URLs and enforce both limits in bytes, with a top-level sitemap index. Prefer separate PL/EN verse maps (31,102 URLs each), additional catalog/chapter/Father/tag/dictionary maps and a shared index. Use only absolute canonical URLs; derive `lastmod` from actual content/metadata changes rather than every build. [Google sitemap guidance](https://developers.google.com/search/docs/crawling-indexing/sitemaps/build-sitemap).

Generate a sorted route manifest keyed by `(locale, routeKind, logicalId)`. Independently derive expected verse keys from the approved golden reference, map them using the specified URL convention, and compare exact sets: no missing, extra, duplicate, case-colliding or incorrectly encoded routes. Check every generated HTML file exists and its embedded reference/content hash agrees with the view model; do not merely trust the generator's own manifest. Check 100% of canonical and alternate-link targets, headings and metadata presence with a lightweight HTML parser; sample browser rendering separately.

CodeceptJS browser tests must disable JS, block database/network fetches and directly request representative start/source/book/chapter/verse/Father/tag/dictionary pages in PL and EN. Assert visible real text, correct source language (`pl`, `en`, `grc`), attribution, meaningful title/description and usable navigation. With JS enabled, verify identical initial content, Unpoly partial transitions, independent three-tab history, refresh, old aliases, unknown URL 404 and both Pages bases. Verify `robots.txt` at the actual origin root: a project subdirectory cannot control the account-level `/robots.txt`; sitemap submission remains available. No-JS tests and complete route inventory supplement, rather than replace, full data verification.

## Independent golden ZIP contract

Proposed artifact: `tests/data/golden-content-v1.zip`. Proposed comparator: `scripts/check-content-integrity.py`, standard-library Python (`zipfile`, `sqlite3`, `json`, `hashlib`) only. These are future paths, not files created now. ZIP is versioned in Git only after rights review. It contains no app binaries, screenshots, auth data or private notes.

The archive is created in a **separate reference-review PR** using an independently reviewed exporter/source audit, not the production SQLite generator under test. Freeze source hashes, resolve ambiguous mappings, review added/removed/changed rows and approve expected content. Ordinary tests cannot rewrite it. A legitimate source update needs reviewed reference changes with readable differences, source/license evidence and reasons; lowering expected counts to hide a missing verse is prohibited.

Archive members use one JSONL file per canonical table, plus `manifest.json` and `navigation.jsonl`. This is slightly more explicit than grouping different shapes into one file and permits exact field/key validation:

| Input | ZIP members / literal SQLite target and key | Fields that must remain verifiable |
| --- | --- | --- |
| `input/{TNP,UBG,KJV,TR}/**/*.yml` plus approved source metadata | `verses.jsonl` → `verses(source_id,book_id,chapter,verse)`; `sources.jsonl` → `sources(source_id)` | Exact display text, language, source locator, source checksum/edition, provenance/license references; missing verse remains absent |
| Existing book list / source coverage | `books.jsonl`, `book_names.jsonl`, `source_books.jsonl` → `books(book_id)`, `book_names(book_id,locale,alias)`, `source_books(source_id,book_id)`; `navigation.jsonl` derived expectations | Stable IDs, original abbreviations, order, labels, alias mapping, chapter inventories and per-source verse ranges |
| `input/Fathers/**` + TEI provenance | `fathers_works.jsonl`, `fathers_passages.jsonl` → `fathers_works(work_id)`, `fathers_passages(work_id,chapter,ordinal)` | Author/work identity, Greek text, section order, introductions at chapter 0, source/version/license |
| Both `input/GreekPolish/*.jsonl.gz` and separate GNT annotation source | `interlinear_tokens.jsonl` → `interlinear_tokens(source_id,book_id,chapter,verse,position)`; `token_annotations.jsonl` → `token_annotations(source_id,book_id,chapter,verse,position,layer,locale,ordinal)` | Surface token, source edition and position; every Polish/English gloss, raw grammar, converted code, Strong, lemma, pronunciation, kind/source/alternatives; raw source payload where the model does not yet parse every field |
| Primary/alias/supplement forms | `dictionary_entries.jsonl`, `dictionary_forms.jsonl`, `form_senses.jsonl` → `dictionary_entries(entry_id)`, `dictionary_forms(form_id)`, `form_senses(form_id,ordinal)` | Exact surface/normalized lookup key, all senses and ambiguity order, gloss vs definition, Strong/grammar relation, language, provenance and license; no POC twelve-alternative truncation |
| Source grammar plus UI explanations | `grammar_codes.jsonl` → `grammar_codes(code)` | Original code, dialect/convention, parsed attributes, supported explanation keys and unknown-code preservation |
| `tags/*.md` | `tags.jsonl`, `tag_ranges.jsonl`, `tag_verses.jsonl`, `tag_fathers.jsonl` → `tags(tag_id)`, `tag_ranges(tag_id,ordinal)`, `tag_verses(tag_id,book_id,chapter,verse)`, `tag_fathers(tag_id,work_id,chapter,ordinal)` | Slug/name, original Markdown/front matter, raw reference/range and complete expansion; duplicate-range semantics and valid targets |
| Source grants/notices | `licenses.jsonl` → `licenses(license_id)` | Attribution, URL, license identifier/version, restrictions, redistribution decision and source association |
| Independently specified search projection | `search_documents.jsonl` → `search_documents(doc_id)` and external-content `docs_fts` index | Domain kind/source key, normalized search columns and exact relation to unmodified display records; no private payloads |
| Audited build contract | `meta.jsonl` → `meta(key,value)` | Public schema, generator and normalization versions, corpus and source-manifest digests; compare deterministic expected values rather than trusting arbitrary self-reported metadata |

Unused raw files such as `JWBiblia.epub` and alternative GNT serializations are provenance inputs, not invented extra reader translations or six copies of one corpus. The manifest records their role and hash where relevant. A separate baseline per source edition prevents confusing TR's 140,628 tokens with the interlinear's 137,323 aligned tokens.

`manifest.json` records `referenceVersion`, `publicSchemaVersion`, audited source commit/hashes, source edition/date/license decisions, each member's SHA-256/count/ordered key and field list, normalization version, duplicate/missing policies and reviewer evidence. Technical generated values such as build timestamp are excluded from content comparison or pinned explicitly; content-version identifiers are checked against the source manifest, not treated as free exceptions.

Serialization policy: UTF-8, LF, canonical field order, no insignificant JSON whitespace, stable numeric representation and deterministic sorting by typed primary-key tuples using Unicode code-point ordering for strings (never locale collation). Preserve original text's diacritics, punctuation and significant whitespace. Only source-specific, audited converter rules may normalize line endings or TEI layout whitespace. Do not run NFKC or accent stripping over display text. Preserve absent/null/empty distinctions. Disallow duplicate logical keys; retain legitimate repeated tokens/alternative senses with explicit position/ordinal. Reference hashes cover keys **and** every approved value.

If publication rights forbid full text in the public reference ZIP, record an explicit per-source exception containing complete ordered keys, field identifiers and SHA-256 digests of canonical UTF-8 values. Compare every value's digest using authorized CI inputs; a global count or one whole-file hash is not enough to diagnose mapping loss. Hash-only baseline publication does not solve lack of rights to publish the actual app snapshot. No protected excerpts in public failure reports.

## Full comparison algorithm and <=60-second gate

Inputs are the already-built candidate SQLite file, approved ZIP and independently derived route manifest. Start a monotonic timer before opening/decompressing inputs. Include all comparison work and reports within the budget; generation and download are separate measured steps.

1. Verify ZIP member hashes/count declarations, input identities, manifest/schema compatibility and exact expected table/field set. Fail missing members/artifacts immediately. Open candidate SQL read-only for value checks; run `PRAGMA quick_check` and `PRAGMA foreign_key_check` (must return no violations).
2. For each table, stream the complete expected sorted JSONL and `SELECT <all canonical columns> FROM <fixed table> ORDER BY <fixed key>` through a merge comparison. Use checked-in fixed table/column allowlists, not SQL from arbitrary ZIP metadata. Compare keys and canonical values (or approved per-record digests); detect missing, extra, changed and duplicate rows. Composite key comparison uses the same typed ordering on both sides. Compare each table fully, including empty expected relations.
3. Verify source/translation/book/chapter/verse relationships, legal source coverage, exact tag range expansion, token/annotation alignment cardinality, provenance FKs and derivation of every search document from the correct domain record. Recompute Greek search normalization independently for the full projection. Do not equate accent-folded FTS strings with display-text integrity.
4. Run FTS5's integrity command including external-content consistency on a disposable validation copy/connection, because the command uses `INSERT` even though it is checking an index. The shipped snapshot remains immutable. For external-content `docs_fts`, use `INSERT INTO docs_fts(docs_fts, rank) VALUES('integrity-check', 1)` and require success. Time creating any required disposable copy within this check; optionally validate a writable prepublication candidate before sealing it rather than copying. Plain `SELECT count(*)` on external-content FTS cannot prove its index is complete. [SQLite FTS5 integrity-check](https://www.sqlite.org/fts5.html#the_integrity_check_command).
5. Derive complete expected verse-route sets from reference keys; compare to actual files/parsed route IDs as above. Emit machine-readable totals and bounded readable differences, wall-clock time, SQLite version, source/version hashes and runner identity. Exit nonzero for any difference **or elapsed time >60 seconds**; a process-level watchdog must terminate a hang. Do not convert a timeout to a skipped check.

Provisional time allocation on a hosted Ubuntu runner, with prepared inputs: ZIP/manifest + database opening 5 s; quick/FK checks 8 s; complete sorted-row comparison 25 s; search projection/FTS checks 12 s; route set/report 5 s; headroom 5 s. **These are targets, not benchmark results.** Measure a full realistic snapshot in the schema PR. If over budget, use indexed ordered scans, batched decoding or precomputed independently approved per-row digests; never sample fewer rows. Keep expensive full `PRAGMA integrity_check` as an additional nightly/release diagnostic only if needed; it does not replace the required fast checks.

The data job runs on **every PR** and **every push/merge to master**, without path filters that silently skip it. Pages/release jobs depend on its success for the **exact snapshot hash** they publish. Missing reference, inaccessible authorized input, absent SQLite, failed timeout or skipped check prevents publishing. Untrusted PRs receive no secrets; when a corpus legally requires private build inputs, report that the gate cannot pass for that run and require an approved trusted validation path before release. Never send private Notes through Actions.

| Injected regression fixture | Required diagnostic |
| --- | --- |
| Remove a verse | Missing exact `(source,book,chapter,verse)` |
| Change one text character | Changed key/field and safe expected/actual digest |
| Swap references while keeping row count | Changed mappings for both keys |
| Drop a tag/range or Father section | Missing relation and affected readable reference |
| Attach Greek form/Strong/grammar to wrong token | Wrong source/reference/position and annotation field |
| Strip breathing/iota subscript | Display/form-key mismatch even if FTS hit counts match |
| Remove an indexed posting but keep content rows | FTS integrity failure |
| Drop one HTML or language alternate | Missing locale/reference route |
| Add source without rights or reviewed reference | Manifest/license/reference gate failure |
| Reviewed legitimate source change | Pass only after reviewed baseline and corresponding source manifest change |

Example future check summary, deliberately without an invented measurement:

```text
Full public-content check: PASS / FAIL
Coverage: 100% of approved verses, Father sections, tokens, senses and tag relations
Missing: 0 | Extra: 0 | Changed: 0 | Duplicate keys: 0
Static verse routes: 62,204 / 62,204 (current audited corpus)
Elapsed: <measured seconds> / 60 s
Reference: <ZIP SHA-256> | Snapshot: <SQLite SHA-256> | Source: <commit>
```

Attach JSON/Markdown diagnostics with total differences and at most twenty safe examples to the check summary; full digests/keys can be an artifact. No full copyrighted source excerpts, tokens or private content. Data checks do not replace CodeceptJS, no-JS SEO checks, screenshot review or real-device storage tests.
