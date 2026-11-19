# Decisions, evidence and unresolved release gates

Reviewed on 19 November 2026. The ledger distinguishes observations of this
checkout, measurements supplied by the owner and future installation tests.

## Architecture decision records

| ADR | Decision and reason | Rejected alternative / consequence |
| --- | --- | --- |
| A01 | One JavaScript/HTML/CSS/Unpoly frontend and pure shared renderers for build and runtime | Separate Razor replacement and SPA, TypeScript or component framework multiply migration work |
| A02 | Native SQLite through Rust; browser public SQLite through WASM worker | Pixel WebKitGTK OPFS absence rules out WASM/SAH as native foundation; does not disprove IDB |
| A03 | Complete immutable public snapshots, normalized reader tables plus FTS | Runtime schema migration and FTS-only content lose reproducibility/reader semantics |
| A04 | One JS IndexedDB, pending-only operations plus distinct preference/lease stores | No outbox.json, Rust Outbox, localStorage-only queue or permanent confirmed-note archive |
| A05 | Tauri GitHub App Device Flow and direct REST; Pages production read-only | No PAT, Git CLI, hosted OAuth broker, secret in JS or runtime Actions processing |
| A06 | GitHub personal private repository exactly `Notes`, stable numeric account/repo IDs | No organization repository, suffixed repository name, auto-publication or account-leaking queue |
| A07 | PL/EN UI, SEO/help/store metadata from first feature; independent source/layer languages | Locale must not filter available translations or fabricate source/gloss translation |
| A08 | Two locale paths per verse reference with self-canonical and reciprocal hreflang | No four source-specific indexed duplicate verse pages; doubled HTML budget is explicit |
| A09 | Preferred Linux Flathub x86_64/aarch64; Windows/AppImage signed updater; mobile store updates | musl Pixel tarball is conditional fallback, not Android or proof of Flatpak compatibility |
| A10 | One public config source derived from Actions repository identity; stable app/origin/update identity | Renaming GitHub repo must not create a different IDB profile or redirect pending writes blindly |
| A11 | Complete independently audited golden reference and <=60-second check, separate from SEO and E2E | Counts, sample searches and self-generated baselines cannot establish 100% integrity |
| A12 | Hosted CI, incremental replacement, final-only legacy/POC removal | No required laptop builder, premature backend deletion or one large rewrite PR |
| A13 | The owner chose **Didaske** for PL and EN and `https://didaske.com/` as the canonical public origin. Derive public URLs from the release configuration. | Verify domain control, reserve platform identifiers and test old inbound links before cutover. Product naming alone does not change the repository, WebView profile or private Notes binding. |
| A14 | Present contextual details modal-first. Each modal offers the same information on a dedicated page and can be docked left, right, top or bottom while reading continues. | The modal, dock and page share the detail ID and renderer. A narrow screen, offline native session or locale switch must preserve access to the full detail. [Reader contract](architecture.md). |
| A15 | Use the TNP EPUB as the authority for footnote markers, their explanations and bold words of Jesus. Carry that structure into the snapshot and the independent golden check. | Preserve publisher-defined speech boundaries and annotate ambiguous marker-to-note associations during source review. [TNP audit](repository-audit.md). |

The prompt contains one stale phrase in §7.7, “Outbox JSON (Tauri)”. The explicit
version header and repeated §5.1 requirements take precedence: Tauri also uses the
single IndexedDB implementation. The optional confirmed-note SQLite cache mentioned
elsewhere would be a separate disposable account cache, never the public snapshot;
the initial plan chooses RAM only to avoid an unnecessary second SQLite artifact.

## Evidence ledger

* Current code and source inventory are recorded in [repository audit](repository-audit.md).
  Merged PR status was queried from GitHub, not inferred from old descriptions.
* The prompt supplies Pixel report `kdzj-tauri-postmarketos-1794824516500.json`:
  cold DB preparation 758.715 ms; warm 67.643/63.642/67.295 ms (median 67.295);
  typical warm SQL p50 0.122 ms; SQLite 3.50.2, 115,317 records. The named raw report
  is not present in this checkout. Treat this as supplied experimental evidence,
  not a measurement rerun here, full app launch time, JS/Rust latency or IDB proof.
* Official Unpoly API supports local HTML, and GitHub documents device-token issuance
  and secret-free refresh. See [architecture](architecture.md) and [auth](github-auth.md).
  Actual production navigation, device token storage and selected-repository bootstrap
  still need their future experiments.
* The complete static page generator, golden ZIP, normalized production schema,
  PL/EN catalog, Notes integration, Flatpak release and updater are proposed work,
  not existing validated features. This change adds none of them.

## Risk register: consequence, smallest future proof, success and fallback

| Risk | Consequence | Minimal future experiment / pass criterion | Fallback or decision |
| --- | --- | --- | --- |
| IDB durability/quota/eviction | Lost unsent text | M06 exact payload survives restart/crash/build upgrade/cache replacement on every required real WebView; failed transaction never reports saved | Block writable target; export while accessible; owner reviews evidence before any storage redesign |
| WebView profiles differ across windows/processes | Split queues and duplicate sends | M06 two windows and two processes see same operations, serialize updates and reject stale fence after sleep | Configure common supported profile; if impossible, propose explicit process-policy change for approval, no hidden second store |
| Power loss vs transaction completion | “Saved” stronger than platform guarantee | Strict durability support + kill/reboot fault cases, report browser/OS and limitations | Clearly scoped guarantee; no claim of protection against device destruction or user clearing site data |
| Device App permissions/install scope | Cannot create/access Notes automatically | M07/M11 real sandbox user token creates private Notes and writes after selected installation; no secret/PAT | Native auth blocked until permissions proved; Pages stays read-only |
| Native secure token store absent | Exposed or nonpersistent credential | Flatpak/Phosh keyring and mobile keystore tests, logout/revocation and log redaction | Session-only token and reauthorization with queue preserved; assess UX before release |
| Existing public/inaccessible Notes | Private content disclosure or wrong repository | Public and masked-404 fixtures, concurrent creation and case/name checks, zero payload sent | Explain conflict, preserve/export queue; never create Notes-2 |
| Owner changes repository visibility during a write | An in-flight note could become public even after a successful privacy check | M11/M12 use a disposable repository and synthetic text. Change visibility between queued writes. Check private owner/name/ID again before dispatch and confirm that subsequent sends stop. | Explain owner control of visibility at connection. The Contents API has no conditional write on privacy. A guarantee covering concurrent owner changes would require revisiting the product requirement. [GitHub write contract](https://docs.github.com/en/rest/repos/contents#create-or-update-file-contents) |
| Lease expires during HTTP | Duplicate write or deletion of newer draft | Pause owner, transfer lease, lose response after write and edit locally again; SHA check and revision fence preserve newest payload | Reconcile remote result; retain unresolved conflict, no silent last-write-wins |
| External Markdown and unknown fields | Lost GitHub edits | Three-way fixture with remote edits, unknown front matter, deletion and non-UTF8/size errors | Conflict comparison and explicit resolution; byte-preserving metadata policy |
| Changed permissions/branch rules/rate limits | Stale edit button or unsent update | Revoke during save, PR-only branch, 403/429 + headers; remote enforcement, bounded retries | Keep pending proposal and explain; refresh rights, no protection bypass |
| Source rights/attribution unclear | Unlawful redistribution to ZIP, Pages or stores | M01/M02 document source-specific display/index/offline rights and exact provenance | Block affected publication; independent hash reference only where lawful, no silent data omission |
| Greek normalization mismatch | Wrong homograph/gloss or missing accents | Exact/contextual/morphology fixture parity, no FTS substitution | Preserve exact source and separate search normalization |
| TNP text conversion loses note identity or emphasis | A star can open someone else's explanation or the reader can misattribute words to Jesus | M02/M03 compare ordered source runs and marker-to-note relations across the whole edition. M08b checks the rendered mixed speech in John 1:38 and both footnotes in John 3:16. | Require a reviewed mapping for ambiguous cases and block a new snapshot that changes the approved relationships. |
| Dock remains modal or changes with the passage | A reader loses access to the passage or the pinned explanation | M04b tests keyboard, pointer and touch at each edge, then changes chapters and opens the pinned detail's page. Both reading areas remain usable and the detail keeps its source identity. | Fix the shared interaction before accepting individual detail views. |
| Pages bytes/deploy/file overhead | Missing per-verse SEO or failed deploy | M14 sample, then M15 full artifact <=800 MB and measured deploy <=8 min, all authorized references included | Optimize first. A measured chapter-anchor scope reduction needs owner review. |
| Corrupt or incompatible snapshot reaches the reader | Readers cannot open the last successful edition | M05 interrupt a download, alter one byte, offer an unsupported schema, then open another tab or process during activation. Verify the active digest and last-good passage after restart. | Keep the previous verified snapshot or the bundled edition. Reclaim only unused versions. [Snapshot activation](data-and-sync.md) |
| Web Locks/SAH unsupported or stale worker | Deadlock or unreadable public cache | Three tabs, close importing tab, wake old worker, bounded recovery, no console errors | Static HTML or per-tab RAM search if memory allows; private Outbox unaffected |
| <=60 s integrity target missed | Publication gate too slow | M03 real corpus timed complete comparison with watchdog and injected corruptions | Optimize streaming/indexes, keep hard gate; do not sample or claim an invented timing |
| UI locale affects scholarly content | Missing sources or fabricated translation | PL UI/EN gloss, EN UI/PL gloss, change locale with open note and modal | Fix policy before feature acceptance; missing DE data does not block PL/EN |
| Flatpak on Alpine/Phosh | Package builds but fails on Pixel | Install hosted artifact, cold/warm launch, touch, offline, updates and shared IDB on physical Pixel | Audited musl fallback; preserve old binary/profile; target support explicitly conditional |
| Native/mobile driver unavailable | False native acceptance claim | Small CodeceptJS protocol bridge that controls packaged app, reports screenshots and kills/restarts it | Separate browser coverage from manual hardware evidence; driver gap blocks claimed automated target |
| Signing/store enrollment missing | Package cannot be installed or updated | Real signed install/update, wrong signature rejection, store entitlement verified | Mark channel unavailable; no public IPA fiction or borrowed certificates |
| Repository rename/transfer/origin change | Dead links or sending private/shared writes elsewhere | M19 signed config A→B; old updater resolves, pending Notes unchanged, shared writes stop until verified | Keep stable domain/app ID; ship bridge update before old endpoint is retired |
| Legacy cutover too early | Readers lose functionality or recovery | M20 parity matrix plus restore drill and user-reviewed screenshots | Retain server and historical sources until all required gates pass |

The author of the indicated implementation PR owns its evidence. The release
maintainer records each required target as passed, failed or awaiting hardware
and links the report to the artifact tested. The owner decides rights, enrollment
and any reduction in supported content or platforms before cutover.

These experiments belong to future PRs. Read-only source counting and official-doc
verification in this planning change do not implement or satisfy them.

## Owner decisions that remain

Confirm rights evidence where source notices do not establish packaging/store scope;
verify ownership of the chosen Didaske domain and reserve stable application identifiers before the first durable profile; register
the GitHub App with reviewed permissions; supply legitimate release-signing/store
accounts when those channels are implemented. Hardware failures may require an explicit
scope decision. No decision is required to accept read-only WWW: the task already
approves it. No fallback introduces a maintained backend.
