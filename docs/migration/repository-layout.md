# Target repository layout

This is a future layout, not a move performed by the planning change. Keep sources
in their current locations until their independent data PR is reviewed. A new folder
must separate a real environment, responsibility or technology replacement point.

```text
app/
  index.html, package.json, vite.config.js
  src/
    main.js, routes.js, data.js
    browser-db.js, tauri-db.js
    github-api.js, github-auth.js, notes-sync.js, outbox.js, config.js
    i18n/                 pl.json, en.json and small formatter
    views/                shared pure render functions and document wrapper
    styles/               shared CSS and bundled fonts
  src-tauri/              native public SQLite, HTTP/credential and OS integration
  store-listings/         reviewed Flathub, Play, Apple text and PL/EN screenshots
data/
  build/                 deterministic build-only converters and snapshot builder
tests/
  specs/                 CodeceptJS JavaScript executable specifications
  dsl/                   domain actions shared by all protocol drivers
  drivers/               browser, native, device and GitHub protocol drivers
  stubs/                 isolated GitHub responses and fault injection
  fixtures/              small synthetic data for behavioral tests
  data/                  independently approved golden ZIP and manifest
  visual/baseline/        reviewed locale/platform images
  visual/reports/         ignored actual/diff/report outputs
input-raw/               existing source bytes and rights evidence
input/                   existing generated readable texts during transition
tags/                    public editorial Markdown remains here
.github/workflows/       verification, packages, release and Pages
docs/migration/          decisions, evidence, sequence and go/no-go gates
```

No interfaces folder, repository-class hierarchy or folder per button. `routes.js`
exists because static generation and runtime use precisely the same route mapping.
`views/` exists because the same rendering must run in Node and WebViews; DOM event
binding stays in runtime modules. Examples and tests use `.js`, not TypeScript.
CodeceptJS configuration may use a small CommonJS `.js` package boundary if required
by the selected release; application modules remain ES modules. Rust never writes Outbox.

| Existing path | Future owner/location | Move or deletion gate |
| --- | --- | --- |
| `input-raw/` | Retain source location; optional later `data/sources/` rename | No duplication; preserve exact bytes, attribution and Git history |
| `input/` | Transitional inputs to `data/build/`; public normalized SQLite artifact | Retire generated duplicate outputs only after independently verified parity and rights review |
| `tags/` | Keep `tags/` as public REST-editable Markdown | Never remove canonical editorial content |
| `convert-input-to-output.py`, data scripts in `scripts/` | `data/build/` preserving behavior first | Small relocation after fixture/golden gates, not mass rewrite |
| `poc/prepare.py`, `poc/build_sqlite.py` | Reference for `data/build/` | Preserve useful algorithms/tests; FTS-only model cannot replace reader schema |
| `poc/frontend/` | `app/` shared production reader | Remove benchmark UI only after parity, data and platform gates |
| `poc/frontend/src-tauri/` | `app/src-tauri/` | Replace benchmark identity/config before first durable user data; retain history |
| `poc/tauri-pmos/` | Release packaging only if musl fallback needed; evidence in docs | Remove obsolete install/benchmark paths after Flatpak device acceptance |
| `KazdyDzienZJezusem/Controllers`, `Services`, `Views`, `wwwroot` | JS domain/renderers, native adapter, build converters | Remove only after route-by-route parity and rollback period |
| `KazdyDzienZJezusem.Tests/` | Behavior fixtures and contract tests in `tests/` | Keep C# tests running during dual-track comparison |
| `ProductionVisualTests/`, `screenshots/` | `tests/specs`, `drivers`, `visual` | Explicitly review replacement JS specs and locale baselines; retain old evidence until cutover |
| `docs/compare.html` | Preserve the standalone parallel PDF reader or integrate its controls into the shared Literature view | Verify its two document URLs, paired page navigation, saved settings and layout before removing the original file |
| `Notes/`, `Topics/`, `Info/`, `Commentarii/` | Keep the existing editorial files in place | Public project files have no automatic mapping into personal private `<login>/Notes`; future ingestion needs a content and rights decision |
| `Index/`, local `IndexHtml/` | Retain historical links and auxiliary generation while consumers use them | Check `scripts/regenerate-topics.py` and any external links before retiring derived output; keep tracked editorial Markdown |
| `systemd/`, sync/install scripts | Legacy laptop services during transition | Identify remaining consumers with the owner before removal; no device service is a dependency of the future app |
| `KazdyDzienZJezusemObsolete/` | Historical reference, then delete obsolete runtime files | Verify no remaining input/build dependency first |
| `compose.yaml`, `podman-up.sh`, Dockerfile and refresh scripts | Removed from final application runtime | Cutover approved, rollback rehearsed, no remaining required service |
| `.github/workflows/poc-*`, `tauri-postmarketos.yml` | Focused production workflows | Remove POC jobs only after replacement evidence is retained |

Generated DBs, packaged installers and published HTML belong to CI artifacts/Releases/
Pages, not additional copies of source content committed to Git. The separately
reviewed golden ZIP is the deliberate test-reference exception with redistribution
rights or per-record hashes as specified in [integrity](seo-and-data-integrity.md).
