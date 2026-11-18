# Small implementation PRs and release gates

This planning change authorizes no implementation or deployment. Each row below is
a future reviewable PR; split further when a row cannot be demonstrated independently.
P0 blocks foundational correctness, P1 delivers parity, P2 completes distribution and
cutover. Dependencies are explicit. A row is complete only after its acceptance evidence
is reviewed, not when code compiles. All future automated build/test/release work runs
on hosted GitHub Actions; device users install ready artifacts and perform physical
checks, never act as mandatory build runners.

Every row inherits these obligations: English repository code/docs; JavaScript tests;
unit/contract checks for changed behavior; complete data check on every PR and default
branch push once introduced; PL/EN catalogs/help/SEO/store metadata updated together;
desktop/mobile screenshot comparisons for affected views; no secrets or private notes
in CI; a plain-language demo and before/after images in the PR. For data-only rows,
screenshots of representative consumers still demonstrate unchanged presentation.
Deliberate baseline changes require review and a subsequent normal comparison.

| ID / priority / dependency | User outcome and modules | Evidence, screenshot/demo and acceptance | Main risk and rollback |
| --- | --- | --- | --- |
| M01 / P0 / plan reviewed | Existing reading behavior is recorded before replacement. Audit route fixtures, C# tests, current visuals and source rights | Route/feature checklist covers Literature/PDF comparison, rights notices and grammar detail; approved legacy desktop/mobile captures; source permissions traced | Hidden features or rights omitted: retain legacy; no production change |
| M02 / P0 / M01 | Public texts keep every original character. Independent golden exporter and `tests/data/` reference | Review all source keys/counts and human-readable diff; source fingerprints and legal redistribution matrix approved; representative Greek/TNP screenshots unchanged | Self-certifying reference or unlawful ZIP: reject baseline, use audited hash exception where necessary |
| M03 / P0 / M02 | Reader and search use a complete, reproducible public snapshot. `data/build/`, normalized schema and manifest | Two builds same bytes; 100% key/value golden comparison, FK/FTS checks, injected missing/changed/extra records detected; watchdog <=60 s on hosted runner; source/attribution image | FTS-only model loses detail: keep original sources and previous snapshot; no release on failed integrity |
| M04 / P0 / M01 | One simple reader shell works in both UI languages. `app/src/views`, `routes`, `i18n`, styles, initial store files | PL/EN switch, keyboard/help/labels, mixed source/gloss choices; no full reload or lost scroll/draft; CodeceptJS four-layer skeleton and screenshots in both locales | Duplicated UI or hardcoded strings: keep shell unpublished and legacy active |
| M05 / P0 / M03,M04 | Public content reads offline through both SQLite engines. `data.js`, browser worker, native commands | Same fixtures/results/error contract; read-only SQL allowlist; three browser tabs with crash/reset/import; native concurrent readers; independent positions captured | SAH deadlock/unsupported storage: static WWW or per-tab memory public-data fallback; native read-only snapshot retained |
| M06 / P0 / M04 | Unsent notes survive restart and upgrades. One `outbox.js`, fixed app/profile/origin configuration | Physical device matrix for Pixel Flatpak, Mint, X13s, Windows, Android, iOS plus three desktop browser tabs; strict transaction/fence tests; version 1→2 with pending payload and account switch; status images | Missing shared profile/evicted queue: block writable target and seek owner decision with evidence; no second JSON store |
| M07 / P0 / M04 | Sign-in works without PAT or a maintained server. Native GitHub App auth/secure HTTP boundary | Real sandbox Device Flow, expired/denied/slow polling, secret-free refresh, selected-repository access, secure token storage; return-to-passage PL/EN captures | Permissions or keyring not viable: native auth release blocked; Pages remains production read-only |
| M08 / P1 / M03–M05 | Bible browsing, source comparison and deep links match legacy | Every source/book/chapter via contracts, representative partial coverage, next/previous/back/forward, three independent tabs; no-JS HTML and PL/EN chapter/comparison screenshots | Wrong verse or history: switch canary back to legacy; keep public URLs unchanged until ready |
| M09 / P1 / M08 | Greek reading preserves exact forms and trustworthy explanations. JS grammar/interlinear views and normalized queries | Legacy golden cases, accents/homographs, Strong plus morphology and context, alternatives, absent forms, no invented inflection; word modal + help PL/EN; en UI/pl gloss and reverse | FTS normalization corrupts precise lookup: retain exact-form path; disable only unreleased new reader |
| M10 / P1 / M08 | Fathers, tags and literature remain readable. Shared views, reference/range handling | Ignatius book/chapter/section, tag ranges/deduplication, Markdown sanitization and PDF comparison controls; legal source notices; PL/EN mobile/desktop screenshots | Lost non-Bible functions: no cutover, restore previous reader artifact |
| M11 / P0 / M06,M07 | Exactly one private personal Notes repository is safely connected. JS Notes bootstrap | Existing/private/public/inaccessible/foreign files; concurrent create 422; stable user/repo IDs, rename/transfer stop; real sandbox selected-repo proof; consent/readiness images | Private text exposed or existing content overwritten: fail closed before writes, preserve queue and support export |
| M12 / P0 / M11 | Offline Markdown edits synchronize and resolve manual GitHub edits. `notes-sync.js` | Drop response after successful PUT, newer local edit while in flight, stale lease, crash/restart, 401/403/409/422/429; exact ack removes only acknowledged pending payload; conflict/empty-queue PL/EN captures | Lost update or wrong account: stop sync, keep both versions and export; no rollback that clears IDB |
| M13 / P1 / M10,M12 | Authorized contributors edit shared tags. REST write policy and branch/PR path | Grant/revoke access without release; branch rules; semantic idempotence; unconfirmed PR not shown as published; public Actions rebuild excludes notes; before/after tag + revoked status captures | Stale authority: GitHub rejects write, retain pending proposal; never force-push or redirect to another repo |
| M14 / P0 / M03,M08–M10 | Each verse can have its own search-visible page. Shared renderer/static route generator prototype | Future 1,000-real-verse sample, PL+EN plus aliases and all assets; extrapolate full bytes/file count then full build; no-JS checks and screenshot pair; <=800 MB and deploy target <=8 min | Exceeds Pages margin: optimize, then present measured chapter-anchor fallback for owner decision; no silent reduction |
| M15 / P1 / M14 | All public deep links work without JavaScript. Full static build/sitemap/alias pipeline | Full allowed reference set equals output routes, no missing/duplicate page; representative actual Pages slash/404 refresh probes; PL/EN canonical/hreflang checks; source text visible | Broken legacy links or denied licenses: leave old site live; retain prior complete Pages artifact |
| M16 / P0 / M05,M06 | Linux packages install and update safely. Flatpak x86_64/aarch64; musl only conditional fallback | Hosted builds + Flatpak lint; real Mint/X13s/Pixel launch/offline/touch/IDB 1→2 tests; no forced DMABUF workaround without proof; localized images | Build success mistaken for hardware support: block affected channel; retain installed working version |
| M17 / P1 / M06,M12 | Standalone desktop users receive verified updates. Windows NSIS and optional AppImage updater | Hosted OS/arch artifacts, updater signatures, tamper rejection, version/channel test and pending queue survives; Windows certificate distinct from updater key; update progress PL/EN screenshots | Broken origin/profile or incompatible downgrade: halt update, keep working package; ship higher-version recovery build |
| M18 / P1 / M06,M12 | Android and iOS install through legitimate signed channels | Android signed APK install/upgrade and Play path; iOS simulator plus actual provisioned device, TestFlight/App Store account approval; fixed identity; bilingual store screenshots | No signing/account entitlement or native driver: explicit blocked distribution, no fake IPA download |
| M19 / P1 / M15–M18 per available channel | Application page offers real downloads and explains updates. Config/manifest/listing renderer | Every visible asset anonymously downloadable with expected hash/size; unavailable platforms labelled; repo rename/transfer and Notes isolation exercised; PL/EN page captures | Mutable config redirects writes/updates: reject untrusted config; retain signed bundled configuration |
| M20 / P0 / M08–M19 required targets | Canary matches current reader and rollback is demonstrated | Side-by-side legacy/new routes, complete integrity, browser/native/hardware matrix, accessibility/security, app-start vs DB-ready timing; restored old public artifact with pending queue intact | Any missing parity, license or durability gate: no-go; keep legacy runtime |
| M21 / P2 / M20 + explicit cutover approval | Final product has one UI and no obsolete server dependency | Archive evidence; remove POC UI/jobs, ASP.NET/Redis/Compose/Cloudflare runtime only after dependency scan; verify offline/public routes, native sync, release and rollback; full screenshots | Removing a still-needed service/source: revert removal, restore preserved legacy deployment; never delete Notes, source licenses or Git history |

## Sequencing and review size

M06 and M07 can run in parallel after the shell. M08–M10 can progress while storage
proof is pending, because read-only public reading does not depend on auth. M16–M18
are separate channels and may publish only the channels whose gates pass. A release
must list blocked targets explicitly rather than call an incomplete matrix complete.
M14's budget experiment precedes mass page generation; M06's device experiment precedes
any promise of durable private drafts. CodeceptJS/visual and i18n gates grow with each
row, never in a final catch-up PR.

Future PR descriptions lead with what a reader can now do, why, executed checks and
links to evidence, before/after PL+EN images, remaining limitations and whether a new
program/data version is required. Do not copy a plan into the test-results section.
CI reports exact checked commit, artifacts, blocked/skipped targets and elapsed time.

## Cutover, rollback and completion

Before changing the public domain, retain the last working server image/config and
public snapshot/Pages artifact, source exports, release manifests and necessary legacy
session/index backup under the owner's existing backup policy. Never export private
notes into CI. Rehearse URL rollback and DNS/cache consequences; HTML meta redirects
are not HTTP 301s. Agree a canary observation window using real feedback and error
evidence, not an arbitrary date. Keep each released binary compatible with the last
good snapshot or bundle a fallback. A public rollback must not revert or discard user
Notes commits, pending IndexedDB operations, credentials or stable app identity.

Final go/no-go requires all mandatory source rights, complete golden data within
60 seconds, measured full Pages margin, PL/EN parity, reviewable screenshots, safe
Notes/account/permission behavior, proven IDB durability and multi-instance behavior,
installable required targets, signed updates and rehearsed recovery. Hardware gaps
remain named blockers. Only then remove legacy and the separate POC frontend. No
automatic merge or production cutover is authorized by this document.
