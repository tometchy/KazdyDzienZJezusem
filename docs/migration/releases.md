# Distribution, updates and repository identity

Status: architecture proposal, checked against the checkout and linked official documentation on 18 November 2026. No installer, workflow, release, store listing or deployment is created by this planning change. Platform builds are proposals until their listed gates pass.

## Existing evidence and its limits

- [`tauri-postmarketos.yml`](../../.github/workflows/tauri-postmarketos.yml) builds on `ubuntu-24.04-arm` inside `alpine:3.24`, compiles an AArch64/musl ELF, runs its non-GUI `--self-test`, and uploads an archive with a 14-day retention. This is neither an Android APK nor a permanent public release. It does not test installation, WebView persistence, Flatpak, signing or updating.
- [`tauri.conf.json`](../../poc/frontend/src-tauri/tauri.conf.json) has the benchmark identifier `pl.kazdydzienzjezusem.benchmark`, one window, `bundle.active: false` and no CSP. [`Cargo.toml`](../../poc/frontend/src-tauri/Cargo.toml) pins Tauri 2.12.3 and tauri-build 2.7.2; it has bundled `rusqlite`, but no updater. These choices are experimental inputs, not a production security configuration.
- [`lib.rs`](../../poc/frontend/src-tauri/src/lib.rs) embeds the compressed corpus and verifies its hash during extraction; it uses a process-local mutex. Reuse checks file length, so the production snapshot verifier must not treat matching length as content integrity. This is not proof of cross-process snapshot or IndexedDB safety.
- [`install-pixel.sh`](../../poc/tauri-pmos/install-pixel.sh) enables `WEBKIT_DISABLE_DMABUF_RENDERER=1`. Preserve the observation in device evidence and enable the workaround only for the tested device/runtime combination after comparison with the default renderer.
- [`poc-pages.yml`](../../.github/workflows/poc-pages.yml) is manual, hardcodes `/KazdyDzienZJezusem/POC/`, and publishes a POC landing redirect. It is not the proposed per-verse website or a rename-safe updater origin.

## Target and evidence matrix

All build runners are GitHub-hosted. No owner laptop or phone compiles the product. Build success, automatic GUI tests, physical installation and distribution approval are separate results. Every GUI checkpoint is captured in PL and EN. `Required` means a release gate once the associated implementation stage is reached, not an assertion of present support.

| Target and priority | Runtime, package and build runner | Automatic driver and screenshot evidence | Signing and update channel | Remaining gate |
| --- | --- | --- | --- | --- |
| WWW, required | Static Pages; SQLite WASM worker; Chromium, Firefox, WebKit; `ubuntu-24.04` Node build | CodeceptJS Playwright helper, desktop/tablet/phone; JS on/off; three tabs | Pages deployment identity and verified public-content manifest; no app installer | Full real HTML, Pages budget, cross-browser storage/locks; WebKit on Linux is not physical Safari |
| Linux Mint x86_64/glibc, required | Flatpak first; GNOME runtime with WebKitGTK; `ubuntu-24.04` + pinned Flatpak SDK | CodeceptJS WebDriver through `tauri-driver`/WebKitWebDriver under Xvfb; installed Flatpak sandbox smoke; actual Mint check | Flathub signing/distribution; updates by Flatpak | Runtime version, sandbox/portal and IDB update evidence |
| X13s Ubuntu aarch64/glibc, required | Same Flatpak manifest, architecture-specific dependencies; `ubuntu-24.04-arm` | Same CodeceptJS driver where ARM WebKitWebDriver is available; actual X13s screenshots and touch/scale check | Flathub | ARM build does not establish hardware GPU or window behavior |
| Pixel 3a XL postmarketOS/Alpine aarch64/musl host, required | Prefer aarch64 Flatpak with its glibc runtime; `ubuntu-24.04-arm` + Flatpak SDK | Hosted sandbox smoke; physical Phosh/Wayland/touch screenshots and durability test | Flathub; app never runs Tauri updater inside Flatpak | Real Pixel performance, portals, disk/RAM, profile sharing, device login and upgrade |
| Mint/X13s standalone, secondary channel | AppImage x86_64/aarch64 glibc; native `ubuntu-22.04` / `ubuntu-22.04-arm` build for a declared compatible baseline | CodeceptJS native driver + installer/update OS harness; both arch packages actually launched | Tauri updater signatures; optional distribution signature for first download | ABI/WebKitGTK/FUSE compatibility and version 1→2 on supported distro |
| Pixel native musl, conditional fallback | Alpine AArch64 archive initially; signed Alpine `.apk` only if a maintained package is justified; `ubuntu-24.04-arm` + pinned Alpine image | Existing self-test retained; CodeceptJS driver feasibility and physical device test separately | Signed static archive manifest and explicit restart/rollback, described below | Activated only after measured Flatpak failure; no glibc AppImage offered as musl package |
| Windows x86_64, required | WebView2, per-user NSIS EXE; `windows-2022`, Rust MSVC/C++ tools | CodeceptJS WebDriver through `tauri-driver` + matching Edge WebDriver; installed-app screenshots and upgrade harness | Tauri updater signature; Authenticode identity separately | Signing budget/identity, clean WebView2 install and multi-process IDB |
| Windows ARM64, conditional | WebView2 ARM64, NSIS; `windows-11-arm`, MSVC ARM64 tools | Native ARM64 install/driver probe then CodeceptJS; do not replace with x64 emulation evidence | Separate ARM64 signed asset/manifest entry | Native driver and installer availability; promote to required only after probe |
| Android arm64-v8a, required before mobile launch | Android System WebView; signed APK pre-store, AAB for Play; `ubuntu-24.04`, pinned JDK/SDK/NDK | CodeceptJS Appium/UiAutomator2 on x86_64 emulator; real ARM64 phone separately | Stable Android signing key; user-approved APK install before Play, Play updates afterwards | ARM64 real-device IDB and WebView lifecycle; Play enrollment/testing/listing |
| iOS arm64, required before iOS launch | WKWebView; signed archive/IPA; `macos-15`, pinned installed Xcode and Rust iOS targets | CodeceptJS Appium/XCUITest simulator integration probe; simulator screenshot plus actual provisioned iPhone test | Apple certificate/provisioning; TestFlight then App Store; Ad Hoc only registered devices | Membership, certificates, real installation and WKWebView durability; no public unrestricted IPA button |

The listed standard public-repository runners include native Linux ARM64, Windows ARM64 and macOS ARM64; standard public-repository usage is currently free. Private-repository usage and larger runners have different billing. Recheck availability and runner image tool versions when implementing, record them with results, and do not silently substitute `-latest`. Account-specific billing and store membership were not inspected. [GitHub-hosted runner reference](https://docs.github.com/en/actions/reference/runners/github-hosted-runners).

ARM AppImages must be built on ARM hardware or emulation; use hosted native ARM rather than QEMU. Build against the oldest declared compatible glibc baseline and test actual newer targets; an Ubuntu build is not automatic Mint/X13s compatibility. [Tauri AppImage guidance](https://v2.tauri.app/distribute/appimage/). Windows ARM64 NSIS may use an x86 installer while installing a native ARM64 app; WebView2 bootstrapper and offline runtime choices must be explicit. Prefer a normal small installer and a separately labeled offline installer when the runtime needs bundling. [Windows installer guidance](https://v2.tauri.app/distribute/windows-installer/).

## Flatpak first, including the Pixel

Choose one stable production application ID before first persistent data, for example `io.github.tometchy.KazdyDzienZJezusem` after namespace ownership review. Freeze it across repo rename, package updates and locales. Do not reuse the benchmark ID accidentally. Select and pin a supported GNOME runtime/SDK with WebKitGTK 4.1 in the first packaging PR; the exact runtime branch is deliberately a packaging acceptance result, not guessed here.

The future manifest vendors locked npm/Cargo inputs, has network-free compilation, installs the desktop file, icons, license notices and PL/EN AppStream metadata. CI runs `flatpak-builder`, dependency/license audit, manifest lint and a sandbox smoke on both architectures. Proposed runtime permissions are display/Wayland with necessary fallback, graphics devices and network for GitHub/public snapshots; use document portals for export rather than unrestricted home access. Test the system browser portal for Device Flow. No generic shell or unrestricted filesystem capability is needed in the frontend.

Flathub normally builds both x86_64 and aarch64, requires dependency sources before its offline build, and reviews applications rather than automatically accepting every wrapper. The app's offline reader, search and notes are material native functionality; admission is still a gate. After approval, the owner submits the manifest to the Flathub submission repository, then maintains the resulting application repository. Release automation prepares version/hash/source updates there for review; it does not assume an upstream GitHub Release updates Flathub automatically. [Flathub requirements](https://docs.flathub.org/docs/for-app-authors/requirements), [Tauri packaging instructions](https://v2.tauri.app/distribute/flatpak/).

The device validation PR supplies installation instructions built around `doas apk add flatpak`, adding the official Flathub remote, `flatpak install flathub <stable-app-id>` and `flatpak update <stable-app-id>`. These are future user setup instructions, not commands executed by this task. Verify the actual Phosh software-center integration; do not promise unattended background updates. [Official Alpine setup](https://flathub.org/en/setup/Alpine), [Flatpak commands](https://docs.flatpak.org/en/latest/using-flatpak.html).

Keep persistent WebView data under the package's data location, public disposable data under an appropriate cache/data directory, and prove which path the engine actually uses. Flatpak maps XDG data into `~/.var/app/<app-id>/data`; a native musl installation has a different sandbox boundary. Treat switching channels as an explicit export/import of pending records, preserving operation IDs, versions, account IDs and hashes, verified before deleting anything at the old location. This is a user-mediated recovery artifact, not a second live Outbox. Do not copy an open WebView database or promise cross-sandbox automatic sharing. [Flatpak conventions](https://docs.flatpak.org/en/latest/conventions.html).

Pixel go/no-go: record cold app-to-first-verse time, warmed navigation/query latency, peak RSS, installed/runtime/download bytes and touch usability with a representative offline corpus; compare native musl and Flatpak on the same device. Require no OOM under a three-window reading workload on the 4 GB device, correct fonts/Greek, functional file/browser portals, preserved pending Markdown after kill/reboot/upgrade, and the same queue in all supported windows/processes. Establish acceptable measured start/RAM budgets with evidence in that PR. A failure requires a concrete report, not an assertion that Alpine cannot use Flatpak.

Only after a demonstrated Flatpak blocker, propose the smallest musl updater: a signed, fixed-target static manifest points to a versioned archive in Releases; a minimal native verifier stages it beside the active executable, checks signature, target and content hash, and requests restart after JS commits pending edits and every window closes. A launcher switches an atomic version pointer and retains the previous executable until a health marker is written. Reject archive traversal, unexpected files and downgrades. Failure leaves the previous pointer intact. Test crash at every transition, low disk space and invalid signatures on the Pixel before approval. This is a future platform-specific fallback, not an existing updater and not a new backend. Never `curl | sh`, and never label it the official AppImage updater.

## Desktop updates and recovery

Use the official Tauri updater for standalone NSIS and AppImage only. It verifies signatures using a public key bundled with the application; build jobs create signatures using a protected private key. A static HTTPS JSON contains `version` and `platforms[target].url` / `.signature` (the signature text, not its URL). Tauri's default version comparison accepts newer versions, and Windows installation exits the running application. [Updater reference](https://v2.tauri.app/plugin/updater/).

Our release policy:

1. Store signing secrets only in a protected Actions release environment, with an offline owner-controlled backup and a recovery rehearsal. Distinguish Tauri artifact signatures from Windows Authenticode, Android signing and Apple provisioning. PR/fork jobs never receive these credentials.
2. Publish immutable assets first, then a manifest verified to reference successful downloads. Start with `stable`; add `beta` only when there is a genuine test audience. Pin a signing identity and target per installed channel. Use separate manifest URLs for glibc AppImage and any musl fallback; no ambiguous `linux-aarch64` file selection.
3. Before closing windows, request draft flush, await IndexedDB transaction completion, stop new sync leases, wait/cancel outstanding HTTP with recoverable operation state and release public SQLite handles. The OS harness verifies that Windows exit cannot outrun an unsaved transaction. Recovery uses the committed pending record and remote reconciliation.
4. Do not claim automatic crash rollback from the plugin alone. Retain a known-good installer/AppImage and test recovery. For a faulty higher release, publish a corrected higher version containing reverted code. An explicit user-initiated reinstall of an older compatible package is a documented emergency option; never silently disable anti-downgrade checks.
5. Rotate updater keys by a bridge release trusted by the previous key before changing the publishing key. A lost key with no bridge requires separately authenticated reinstall; it cannot be repaired by unsigned JSON. Do not distribute private keys as recovery assets.

Flatpak installation displays “Updates are managed by Flathub/Flatpak” and opens its published store page or software center. It cannot also update the sandbox's files through the desktop updater. Test Flatpak version 1→2 and rollback to an available prior commit without removing app data; package downgrade is not permission to downgrade an incompatible Outbox envelope.

## Public-content update is a separate operation

App SemVer, `publicSchemaVersion`, `publicContentVersion` and `minAppVersion` are distinct. A tag/source change can publish a new public snapshot without a new executable. A code change may require an executable and a compatible snapshot. No runtime `ALTER TABLE` or `schema_migrations` exists.

Download a content-addressed snapshot to staging, verify the trusted manifest, size, SHA-256, schema and quick-check, then activate an atomic pointer. Keep the last compatible snapshot until all readers release it. Never overwrite a SQLite file open in another process. On incompatible schema, continue the prior version and explain which app update is needed. Old browser JS keeps a compatible content generation rather than mixing a new DB with old SQL contracts. The stable IndexedDB Outbox is outside this replacement and survives all cache resets. See [data and synchronization](data-and-sync.md).

Each update acceptance test starts with an offline unconfirmed note, two windows and a second process if supported. Upgrade app, replace public DB, restart offline, inspect the exact pending bytes, reconnect to a stub, reconcile and confirm that the acknowledged payload disappears. Repeat after corrupted download, failed install, canceled restart and recovery. Cache disappearance is allowed; disappearance of pending edits is not.

## Mobile installation and costs

Android pre-store distribution is a signed ARM64 APK with a stable application ID and signing key, downloaded from an immutable Release. Show version and hash, then hand installation to the system with explicit user consent. On Play, publish AAB through the protected release job and use Play updates. Preserve signing compatibility when moving from direct APK distribution to Play; an upload key and Play app-signing key are different concepts. A higher version code and compatible signing identity are required for updates. [Android update rules](https://developer.android.com/google/play/app-updates).

iOS requires the appropriate signing/provisioning path. The plan starts with simulator builds and TestFlight for authorized testers, then App Store distribution; Ad Hoc is restricted to registered devices and current provisioning. The supplied older Apple Xcode Ad Hoc URL returned 404 during this audit; the current [Ad Hoc provisioning instructions](https://developer.apple.com/help/account/provisioning-profiles/create-an-ad-hoc-provisioning-profile) were verified instead. A generated IPA is not evidence of a publicly installable app. Keep the download row disabled with a reason until a real supported distribution link exists.

Apple Developer Program currently lists US$99 per membership year (regional terms/waivers may apply); Google Play lists a US$25 registration fee and additional verification/testing requirements. These are owner decisions before store launch, not runtime backend costs. Domain registration and optional Windows certificate services may also cost money. No paid device farm is assumed. [Apple enrollment](https://developer.apple.com/programs/enroll/), [Play enrollment](https://support.google.com/googleplay/android-developer/answer/6112435?hl=en).

## One public repository parameter and stable installed identity

Proposed build input: `KDZJ_PUBLIC_REPOSITORY`, an optional repository variable defaulting to the Actions `github.repository` context. One generated `app-config.json` is consumed only through `config.js`; no views, API calls or fixtures hardcode the production owner/name. Build-time metadata supplies the rest, rather than requiring five manually duplicated environment variables. [Actions variables](https://docs.github.com/en/actions/reference/workflows-and-actions/variables).

Illustrative public contract (not a committed runtime file):

```json
{
  "configVersion": 1,
  "publicRepository": { "id": 123456, "fullName": "owner/project" },
  "siteOrigin": "https://reading.example.org",
  "basePath": "/",
  "contentManifestUrl": "https://reading.example.org/content/manifest.json",
  "releaseManifestUrl": "https://reading.example.org/releases/stable.json",
  "notesRepositoryName": "Notes",
  "appVersion": "1.0.0",
  "publicSchemaVersion": 1,
  "publicContentVersion": "sha256:example"
}
```

The domain above is illustrative, not a claimed owned endpoint. Production selects a controlled stable domain before cutover; otherwise the project Pages prefix is derived and its rename limitation documented. Validate HTTPS, allowed hosts, schema and repository numeric identity. Tauri bundles a validated offline config. A downloaded config cannot authorize a new write repository, updater key or arbitrary host. Changes to those trust roots require a signed app release or a migration manifest signed by an already trusted key.

Rename/transfer procedure: prepare a bridge release while the old endpoint works; record old/new full names and the same repository numeric ID; recheck installation access and branch rules; update the one repository input; regenerate base, canonical, sitemap, release links and store links; test every legacy deep link and 404; publish through the unchanged domain. If no stable domain exists, maintain an explicit old static site/link page while possible and accept that old binaries may need manual installation. GitHub project Pages URLs do not automatically follow repository rename. [GitHub rename behavior](https://docs.github.com/en/repositories/creating-and-managing-repositories/renaming-a-repository).

Outbox partitioning uses GitHub numeric user ID and stored destination repository ID; neither locale, login spelling nor public repo name changes it. `<login>/Notes` remains exactly `Notes`. Resolve a changed login using identity, not by silently creating a second repository. Freeze pending shared edits when the destination cannot be verified; only an authorized identity-preserving migration resumes them. Test repo A→B with both a private note and a tag pending: note sync remains independent, unauthorized tag transmission never occurs.

## Release manifest and the Application page

One release manifest records per asset: OS, architecture, ABI, channel, version, source commit, date, bytes, SHA-256, signature status, permanent URL, install/update instructions key and availability reason. The updater-specific JSON is generated from that manifest. Validate URLs by downloading and hashing the actual public asset before publishing download buttons. Artifacts requiring an Actions account or expiring in 14 days are never download links.

The shared renderer generates a real PL/EN Application HTML page with available package/store links, manual-download fallback, changelog and clear update ownership. Unsupported/unsigned/unpublished platforms show their actual status without an active fake button. Linux distinguishes Flatpak from AppImage and musl; Android differs from Alpine `.apk`; iOS distinguishes TestFlight/App Store from restricted Ad Hoc. See [localization requirements](i18n-and-store-listings.md).

Publish order is tests → reproducible content → required target builds/tests → protected signing → immutable Release assets → verified static manifests → atomic Pages deployment and reviewed store submissions. A missing required platform blocks claiming a complete release; an explicitly optional platform is marked unavailable. Keep previous static artifacts and manifests for a verified rollback, without reverting private notes or their Outbox. This plan does not run any of those publication steps.
