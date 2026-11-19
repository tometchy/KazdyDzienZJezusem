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
| M02 / P0 / M01 | Public texts keep every original character. Independent golden exporter and `tests/data/` reference | Independently inventory TNP footnote markers, note bodies, their associations and Jesus-speech boundaries against the EPUB alongside all other source keys; source fingerprints and legal redistribution matrix approved; representative Greek/TNP screenshots unchanged | Self-certifying reference or unlawful ZIP: reject baseline, use audited hash exception where necessary |
| M03 / P0 / M02 | Reader and search use a complete, reproducible public snapshot. `data/build/`, normalized schema and manifest | Two builds same bytes; 100% key/value golden comparison, FK/FTS checks and injected corruptions, including a swapped footnote target or a one-word shift across a TNP bold boundary; watchdog <=60 s on hosted runner; source/attribution image | FTS-only model loses detail: keep original sources and previous snapshot; no release on failed integrity |
| M04 / P0 / M01 | One simple reader shell works in both UI languages. `app/src/views`, `routes`, `i18n`, styles, initial store files | PL/EN switch, keyboard/help/labels, mixed source/gloss choices; no full reload or lost scroll/draft; CodeceptJS four-layer skeleton and screenshots in both locales | Duplicated UI or hardcoded strings: keep shell unpublished and legacy active |
| M04a / P0 / M04 | A tester can install two consecutive builds of the bilingual shell. Use hosted jobs for development-signed packages with a fixed test identity, origin and profile. | Install v1 and upgrade in place to v2 on each target as it becomes available, including a test Flatpak remote for Pixel and a provisioned iPhone. Capture version and locale. | Provisioning or tooling unavailable: keep that target pending. A fixture app or browser result cannot qualify its production WebView. |
| M04b / P0 / M04 | A reader can open a study detail over the passage, keep it at a chosen edge or share its page. Build the shared detail view, route and modal/dock controls with a fixture. | In PL/EN, exercise the dedicated-page link, Back, keyboard focus, all four edges and undocking. Change chapters while the panel stays visible. Verify the other tab retains its own layout. Capture the overlay and dock on phone and desktop. | A panel obscures text or traps focus: fix the shared reader shell before porting individual details. The page URL remains usable directly. |
| M05 / P0 / M03,M04 | Public content reads offline through both SQLite engines. `data.js`, browser worker, native commands | Same fixtures/results/error contract; read-only SQL allowlist; three browser tabs with crash/reset/import; native concurrent readers; independent positions captured | SAH deadlock/unsupported storage: static WWW or per-tab memory public-data fallback; native read-only snapshot retained |
| M06 / P0 / M04a | Unsent notes survive restart and upgrades, assessed per installable target. One `outbox.js`, fixed app/profile/origin configuration | Physical device matrix for Pixel Flatpak, Mint, X13s, Windows, Android, iOS plus three desktop browser tabs; strict transaction/fence tests; version 1→2 with pending payload and account switch; status images | Missing shared profile/evicted queue: block writable target and seek owner decision with evidence; no second JSON store |
| M07 / P0 / M04 | Sign-in works without PAT or a maintained server. Native GitHub App auth/secure HTTP boundary | Real sandbox Device Flow, expired/denied/slow polling, secret-free refresh, selected-repository access, secure token storage; return-to-passage PL/EN captures | Permissions or keyring not viable: native auth release blocked; Pages remains production read-only |
| M08 / P1 / M04b,M05 | Select a verse to compare sources in the modal, dock the comparison, or follow its dedicated URL | Every source/book/chapter via contracts, representative partial coverage, next/previous/back/forward, three independent tabs; no-JS HTML and PL/EN chapter/comparison screenshots | Wrong verse or history: switch canary back to legacy; keep public URLs unchanged until ready |
| M08a / P1 / M05,M08 | Find a passage by a phrase or Greek form and open its source. Implement the search view on the shared data contract. | Compare ranked results and snippets for accented Greek and PL/EN words on both adapters. Open a result, return to the same query, run a different search in another tab, and capture empty and unavailable-index states in both languages. | Unusable index or memory pressure: preserve the query and offer direct catalog navigation. Keep the previous verified index available. |
| M08b / P1 / M03,M04b,M08 | Read TNP with the editorial cues from the EPUB. Add marked verse text and footnotes to the shared reader and comparison views. | In John 1:38 only the marked speech is bold. In John 3:16, the two printed stars open the correct distinct explanations. Test a repeated marker sharing one note, an unmatched marker and both UI locales. The modal, dock and full page must show the same source note in an offline installed reader and on Pages. | A note attaches to the wrong star or speech extends into narration: fail the corpus check, retain the previous snapshot and block release. |
| M09 / P1 / M08 | Greek reading preserves exact forms and trustworthy explanations. JS grammar/interlinear views and normalized queries | Legacy golden cases, accents/homographs, Strong plus morphology and context, alternatives, absent forms, no invented inflection; word modal, dock and directly linked grammar page identify the selected form in context, with PL/EN help; en UI/pl gloss and reverse | FTS normalization corrupts precise lookup: retain exact-form path; disable only unreleased new reader |
| M10 / P1 / M08 | Fathers, tags and literature remain readable. Shared views, reference/range handling | Ignatius book/chapter/section, tag ranges/deduplication, Markdown sanitization and PDF comparison controls; legal source notices; PL/EN mobile/desktop screenshots | Lost non-Bible functions: no cutover, restore previous reader artifact |
| M11 / P0 / M06,M07 | Exactly one private personal Notes repository is safely connected. JS Notes bootstrap | Existing/private/public/inaccessible/foreign files; concurrent create 422; stable user/repo IDs, rename/transfer stop; real sandbox selected-repo proof; consent/readiness images | Private text exposed or existing content overwritten: fail closed before writes, preserve queue and support export |
| M12 / P0 / M11 | Offline Markdown edits synchronize and resolve manual GitHub edits. `notes-sync.js` | Drop response after successful PUT, newer local edit while in flight, stale lease, crash/restart, 401/403/409/422/429; exact ack removes only acknowledged pending payload; conflict/empty-queue PL/EN captures | Lost update or wrong account: stop sync, keep both versions and export; no rollback that clears IDB |
| M13 / P1 / M10,M12 | Authorized contributors edit shared tags. REST write policy and branch/PR path | Grant/revoke access without release; branch rules; semantic idempotence; unconfirmed PR not shown as published; public Actions rebuild excludes notes; before/after tag + revoked status captures | Stale authority: GitHub rejects write, retain pending proposal; never force-push or redirect to another repo |
| M14 / P0 / M03,M08b,M09 | Each verse can have its own search-visible page. Shared renderer/static route generator prototype | Future 1,000 real references in PL+EN. Record weighted page sizes, generation time and no-JS screenshots. Count and sample dedicated footnote and Greek word pages, then estimate the full artifact against 800 MB, including aliases and assets. Record the distinct note and word-page counts and their average bytes | Exceeds Pages margin: optimize, then present measured chapter-anchor fallback for owner decision; no silent reduction |
| M15 / P1 / M09,M10,M14 | All public deep links work without JavaScript. Full static build/sitemap/alias pipeline | Build all authorized pages and measure <=800 MB and <=8 minutes for a complete staging deployment. Compare the full reference set to the actual files. Use a fixture manifest for the Application page until M19. Probe real Pages slash/404 responses and check PL/EN canonicals, language alternates and visible source text | Broken legacy links or denied licenses: leave old site live; retain prior complete Pages artifact |
| M16 / P0 / M05,M06 | Linux packages install and update safely. Flatpak x86_64/aarch64; musl only conditional fallback | Hosted builds + Flatpak lint; real Mint/X13s/Pixel launch/offline/touch/IDB 1→2 tests; no forced DMABUF workaround without proof; localized images | Build success mistaken for hardware support: block affected channel; retain installed working version |
| M17 / P1 / M06,M12 | Standalone desktop users receive verified updates. Windows NSIS and optional AppImage updater | Hosted OS/arch artifacts, updater signatures, tamper rejection, version/channel test and pending queue survives; Windows certificate distinct from updater key; update progress PL/EN screenshots | Broken origin/profile or incompatible downgrade: halt update, keep working package; ship higher-version recovery build |
| M18 / P1 / M06,M12 | Android and iOS install through legitimate signed channels | Android signed APK install/upgrade and Play path; iOS simulator plus actual provisioned device, TestFlight/App Store account approval; fixed identity; bilingual store screenshots | No signing/account entitlement or native driver: explicit blocked distribution, no fake IPA download |
| M19 / P1 / M15 plus the packaging stage for the channel offered | Application page offers real downloads and explains updates. Config/manifest/listing renderer | Every visible asset anonymously downloadable with expected hash/size; unavailable platforms labelled; repo rename/transfer and Notes isolation exercised; PL/EN page captures | Mutable config redirects writes/updates: reject untrusted config; retain signed bundled configuration |
| M20 / P0 / M08–M19 required targets | Canary matches current reader and rollback is demonstrated | Side-by-side legacy/new routes, complete integrity, browser/native/hardware matrix, accessibility/security, app-start vs DB-ready timing; restored old public artifact with pending queue intact | Any missing parity, license or durability gate: no-go; keep legacy runtime |
| M20a / P2 / M20 + explicit cutover approval | Allow the final editor to write tags through GitHub REST exclusively. Retire the legacy editor session/write route, TagGitStore, clone/credential mounts and periodic Git sync as a separate change. | From a clean installation without Git or an SSH agent, save one allowed tag, revoke write and verify the next edit stays pending. Compare the published SHA with the new public tag and capture both reader and editor views. Verify no other source consumer depends on the checkout. | Two writers or missing updates: pause shared writes, reconcile outstanding proposals by path/SHA, and restore the last working editor only after ownership is handed back. |
| M21 / P2 / M20a + completed observation window | Final product has one UI and no obsolete server dependency | Archive evidence; remove POC UI/jobs, ASP.NET/Redis/Compose/Cloudflare runtime only after dependency scan; verify offline/public routes, native sync, release and rollback; full screenshots | Removing a still-needed service/source: revert removal, restore preserved legacy deployment; never delete Notes, source licenses or Git history |

## Sequencing and review size

M07 can start with the shell. M06 uses the two installable shell builds from M04a. Each target can proceed to Notes testing as soon as its own M06 result passes. M08–M10 can progress while storage
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

## Acceptance evidence at each handoff

| Handoff | Evidence the next PR can actually use |
| --- | --- |
| M01 → M02/M04 | Record a URL, input revision, expected text/absence, locale, screenshot and interaction for each row in the audit. Review discrepancies against the current app with the owner. Keep the old regression suite runnable. Introduce the first JavaScript CodeceptJS reader scenario against legacy here and reuse its DSL when the shell arrives. |
| M03 → M05/M08 | Publish the independently reviewed reference digest, public snapshot digest and timed full row comparison. Compare the navigation keys in the ZIP to SQLite here. M15 separately checks that the generated HTML files exist and contain those passages. |
| M04 → M14 → M15 | Use Didaske as the display name. Bind the production Pages base to `https://didaske.com/` and reserve stable package IDs before any durable profile is shipped. Once a verse comparison renders, measure 1,000 real references in both locales before committing to the full generation cost. Confirm the estimate with the exact publishable artifact including note and word pages, catalogs, dictionaries, attribution, assets and aliases. |
| M06/M07 → M11/M12 | Record installed package hash, OS, WebView, app/profile identity and the digest of a synthetic pending payload before and after upgrade. Attach real sandbox evidence for creating and reconnecting one private repository. Only targets with both proofs may claim writable Notes. Repeat storage and reauthorization tests using the real release channel in M16–M18. |
| M13 → M20a | Record the last tag commit accepted by the legacy writer, the first REST edit and its resulting public snapshot hash. Before switching writers, reconcile outstanding changes, disable the old write entry point and advance the reader to the verified snapshot. Document who can restore the previous version. |

Keep the evidence in the corresponding future PR and link it from its successor. A
resumed session can start from the first dependency without a reviewed result.
Implementation has not begun in this planning PR.

## Cutover, rollback and completion

Before directing `didaske.com` to the new public reader, verify control of the apex domain, DNS, Pages binding and HTTPS. Test old bookmarked links from the existing public hostname and the new dedicated detail URLs under both locales. Retain the last working server image/config and
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
