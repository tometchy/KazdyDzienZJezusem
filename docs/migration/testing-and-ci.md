# Acceptance tests, visual review and CI

This document specifies future JavaScript CodeceptJS tests; fenced examples are design contracts, not installed tests. The current documentation task does not introduce a framework, dependencies or workflows. See [the audit](repository-audit.md), [data verification](seo-and-data-integrity.md) and [platform matrix](releases.md).

## What exists, and what it does not prove

[`ProductionVisualTests`](../../ProductionVisualTests/package.json) currently uses Playwright Test 1.55.1 and TypeScript. Its [configuration](../../ProductionVisualTests/playwright.config.ts) fixes Linux fonts, `pl-PL`, a 1440×1000 desktop and Pixel 7 mobile Chromium, and fails on missing baselines. It does not test EN, native packages or private Notes. [`screenshots.yml`](../../.github/workflows/screenshots.yml) is currently manual-only, not a PR gate. Preserve those baseline assertions during migration; replace the acceptance runner incrementally with CodeceptJS JS without retaining a second permanent E2E specification library.

The [.NET tests](../../KazdyDzienZJezusem.Tests) cover Bible navigation/rendered HTML, dictionaries/morphology, Redis tags, Git authorization and a local bare-remote Git store. [`tests`](../../tests) includes Python generator/container tests and JS grammar tests. These supply behavior fixtures and contracts; old Git/Redis infrastructure tests are retired only with their implementation. [`poc-sqlite.yml`](../../.github/workflows/poc-sqlite.yml) checks counts and selected queries plus browser smoke; it is not an independent full golden-content comparison. [`tauri-postmarketos.yml`](../../.github/workflows/tauri-postmarketos.yml) runs native SQLite self-test, not GUI/IDB upgrade acceptance.

For the present checkout, repository policy still calls for `env -u KDJ_VISUAL_BASE_URL bash scripts/test-screenshots.sh` after final edits. This tests isolated current-checkout ASP.NET/Redis processes, not containers or production. Missing dependencies use `bash scripts/setup-visual-tests.sh --with-deps` as directed by AGENTS; setup failure is incomplete validation, never a green result. The handoff records actual results and, on failure, `screenshots/results/` and `screenshots/report/`. No current test result in this document claims completion of a future target experiment.

## Four boundaries, one acceptance language

Adopt the separation described in [Dave Farley's ATDD guide](https://continuous-delivery.co.uk/downloads/ATDD%20Guide%2026-03-21.pdf):

| Layer | Proposed location | Responsibility and forbidden leakage |
| --- | --- | --- |
| Executable specifications | `tests/specs/*.js` | User outcomes expressed through `I.*`; no selectors, clicks, SQL or HTTP status codes |
| Domain-specific language | `tests/dsl/study.js` | Reading, note, account and update vocabulary; translates intent, not rendering details |
| Protocol drivers and stubs | `tests/drivers/*.js`, `tests/stubs/*.js` | CodeceptJS Playwright/WebDriver/Appium adapters, process/installer harness and GitHub HTTP stub; selectors/status codes belong here |
| System under test | Built Pages tree or packaged native executable | Production renderer, domain logic, storage, IPC and capabilities; do not substitute a mock UI |

All tests/configurations are `.js` and use English repository text. Test reports and product screenshots cover PL/EN. Playwright is a CodeceptJS helper, not a second test suite. Unit tests, SQL contracts and the full-content verifier are allowed separate tools because they test different boundaries. [CodeceptJS Playwright](https://codecept.io/playwright/), [CodeceptJS WebDriver](https://codecept.io/webdriver/).

### Three complete example specifications

Each scenario gets a new test profile, pinned content fixture and a clean stub. `haveReaderSession` selects a driver and locale; it does not bypass app initialization. These examples intentionally describe requested behavior; their DSL methods must be implemented in the future test stage.

```js
Feature('Independent study tabs');

for (const locale of ['pl', 'en']) {
  Scenario(`I keep three independent places while reading (${locale})`, async ({ I }) => {
    await I.haveReaderSession({ platform: 'web', locale });
    await I.openVerseInNewTab('J', 1, 1);
    await I.openVerseInNewTab('1Kor', 11, 10);
    await I.openDictionaryInNewTab('λόγος');
    await I.searchInStudyTab(2, 'faith');
    await I.returnToStudyTab(1);
    await I.seeVerse('J', 1, 1);
    await I.navigateBackAndForwardWithoutChangingOtherTabs();
    await I.switchAcrossAllStudyTabs(5);
    await I.seeIndependentHistoryScrollAndSelection();
    await I.seeNoReaderErrors();
    await I.captureStudyTabs(`independent-tabs-${locale}`);
  });
}
```

```js
Feature('Notes survive offline work and app updates');

for (const locale of ['pl', 'en']) {
  Scenario(`I recover and synchronize an offline note (${locale})`, async ({ I }) => {
    await I.haveReaderSession({ platform: 'tauri', locale });
    await I.signInThroughGitHub();
    await I.seeOnePrivateNotesRepository();
    await I.loseInternet();
    await I.saveNoteOffline('Study note with λόγος');
    await I.seePendingChanges(1);
    await I.captureStudyState(`pending-${locale}`);
    await I.closeAndReopenApp();
    await I.seeNote('Study note with λόγος');
    await I.checkForAppUpdate();
    await I.installAvailableUpdateFromFixture();
    await I.seeNoteAfterUpdate('Study note with λόγος');
    await I.restoreInternet();
    await I.seeNoteOnGitHubStub('Study note with λόγος');
    await I.seePendingChanges(0);
    await I.closeAndReopenApp();
    await I.seeNoAcknowledgedPayloadInOutbox();
    await I.captureStudyState(`synchronized-${locale}`);
  });
}
```

The update fixture is delivered by the OS test harness from a local verified artifact, so the note stays offline during installation. This is distinct from pretending that a real internet updater succeeds without connectivity.

```js
Feature('Shared editing follows current GitHub access');

for (const locale of ['pl', 'en']) {
  Scenario(`I retain private notes when shared write access is revoked (${locale})`, async ({ I }) => {
    await I.haveReaderSession({ platform: 'tauri', locale });
    await I.signInThroughGitHub();
    await I.receiveSharedWritePermission();
    await I.refreshAccountPermissions();
    await I.seeSharedTagEditingAvailable();
    await I.prepareSharedTagEdit('faith', 'A reviewed description');
    await I.loseSharedWritePermission();
    await I.submitPreparedSharedTagEdit();
    await I.seeSharedEditSafelyPending();
    await I.seeNoUnauthorizedSharedCommit();
    await I.refreshAccountPermissions();
    await I.seeSharedTagEditingUnavailable();
    await I.savePrivateNote('My private note remains independent');
    await I.seePrivateNoteSynchronized();
    await I.captureStudyState(`revoked-shared-access-${locale}`);
  });
}
```

### DSL and two protocol-driver skeletons

The composition root injects one driver into a custom CodeceptJS helper; exported public methods become `I` methods. Use CodeceptJS actor/helper integration, and await every asynchronous domain operation. The following is a sketch of responsibility, not a complete helper package.

```js
// tests/dsl/study.js — injected into a CodeceptJS Helper
export function study(driver) {
  return {
    async openVerseInNewTab(book, chapter, verse) {
      await driver.openReadingPlace({ kind: 'verse', book, chapter, verse }, true);
      await driver.waitForReaderReady();
    },
    async saveNoteOffline(markdown) {
      await driver.editPrivateNote(markdown);
      await driver.waitForLocalSaveComplete();
    },
    async seePendingChanges(count) {
      await driver.assertPendingCount(count);
    },
    async captureStudyState(name) {
      await driver.waitForReaderReady();
      await driver.capture(name);
    }
  };
}
```

```js
// tests/drivers/browser.js — inside a CodeceptJS custom Helper
export class BrowserDriver {
  constructor(playwrightHelper, routes, visual) {
    Object.assign(this, { browser: playwrightHelper, routes, visual });
  }
  async openReadingPlace(place, newTab) {
    if (newTab) await this.browser.openNewTab();
    await this.browser.amOnPage(this.routes.urlFor(place));
  }
  async waitForReaderReady() {
    await this.browser.waitForElement('[data-reader-ready="true"]', 10);
  }
  async capture(name) {
    await this.browser.saveScreenshot(`${name}.png`, true);
    await this.visual.compare(name, { allowNewBaseline: false });
  }
  // editPrivateNote, assertPendingCount and other operations use locators here.
  // A read-only web driver rejects writable scenario selection in setup.
}
```

```js
// tests/drivers/tauri.js — native WebDriver + OS lifecycle test harness
export class TauriDriver {
  constructor(webDriverHelper, lifecycle, visual) {
    Object.assign(this, { ui: webDriverHelper, lifecycle, visual });
  }
  async waitForReaderReady() {
    await this.ui.waitForElement('[data-reader-ready="true"]', 10);
  }
  async closeAndReopenApp() {
    await this.lifecycle.closeProcessAndAwaitExit();
    await this.lifecycle.startInstalledAppWithSameProfile();
    await this.lifecycle.reconnectWebDriver();
    await this.waitForReaderReady();
  }
  async capture(name) {
    await this.ui.saveScreenshot(`${name}.png`);
    await this.visual.compare(name, { allowNewBaseline: false });
  }
  // OS harness installs a signed test package; JS never impersonates installation.
}
```

These helpers use the screenshot methods behind the domain `I.captureStudyState`; where useful, a DSL method delegates to `I.saveElementScreenshot` for the grammar modal or layer panel. Capture screenshots from the actual native window where available. [CodeceptJS screenshot API](https://codecept.io/web-api/).

Native Linux/Windows use direct `tauri-driver`; Linux additionally needs the matching WebKitWebDriver and a display, Windows the matching Edge WebDriver. Current Tauri guidance also describes an embedded WebDriver service supporting macOS; using it through CodeceptJS needs a small compatibility experiment, without importing a second WDIO scenario suite. Test-only driver plugins must be excluded from production bundles. Mobile Appium WebView context discovery and restart attachment require their own proof. Browser tests with mocked `invoke` do not prove native persistence. [Tauri driver support](https://v2.tauri.app/develop/tests/webdriver/).

## Stub and fixture boundaries

The GitHub stub is a local CI/test-process service, never a production proxy. A narrow test transport points only test builds at it; release config rejects test hosts and contains no stub routes. The stub records method, path, body, version and authorization identity without storing real credentials. Its finite scripts include device pending/slow-down/expired/denied, refresh rotation, GET identity, private/public/inaccessible Notes, creation races, SHA mismatch, branch protection, rate limits, partial response loss after a committed write and account revocation. It models GitHub's permission intersection rather than granting writes because a UI button was enabled.

Contract tests check request schemas against actual documented endpoints; a separately authorized sandbox account verifies device flow, selected/all installation behavior, creation and revocation. Ordinary PRs use only fixtures and never contact private Notes. Sandbox tests run in a protected environment on reviewed code and use synthetic nonpersonal Markdown. Their result does not replace per-platform WebView tests.

Fixtures pin the corpus version, numeric account/repository IDs, UTC clock, random seed, ordered query results, fonts, device scale, locale, timezone and viewport. Reset public caches and test profiles before a scenario, except explicit persistence/upgrade tests that keep the same profile. Use readiness events and bounded polling; no arbitrary `sleep`, retry-until-green or accidental success from a prior process. Preserve failing artifacts with redacted HTTP traces, not real token headers or note payloads.

## Coverage and acceptance gates

| Requirement | Required assertions and failure injection | Evidence |
| --- | --- | --- |
| Reader parity | All actual routes in audit; translations, missing TNP coverage, next/previous, compare, tags/ranges, Fathers, dictionary and Greek exact form/context | Fixture contracts + CodeceptJS representative desktop/mobile PL/EN screenshots |
| Locale independence | EN UI + PL gloss/TR; PL UI + EN gloss/TR where licensed data exists; both UI locales allow all translations; locale switch preserves URL/draft/layers/scroll | Layer panel and reader screenshot pairs; missing-data state instead of fabricated gloss |
| Fragment navigation | No document reload during navigation/search/modal; back/forward and focus correct; Unpoly preserved nodes remain | Document-instance marker, history assertions, console failure collection |
| Three browser tabs | Different verses/dictionary; simultaneous search; five switching rounds; active import tab closed; worker crash and wake | Bounded completion, no deadlock/console errors, three independent screenshots |
| Public snapshot replacement | Old/new readers, import/reset race, missing Web Locks, NotFoundError, offline, corrupt hash, incompatible schema | DB contract tests and live browser/native scenarios |
| IndexedDB persistence | Put/transaction completion, physical close/restart/reboot, application version 1→2, snapshot replacement, quota/disk failure, logout/relogin | Exact pending Markdown bytes and IDs survive; actual device PASS/FAIL matrix |
| Multiple writers | Two windows and independent processes share profile/origin; expired lease; stale fenced response; same note edited on second device | No lost payload, no duplicate confirmed write, conflict preserves both versions |
| Native login and Notes | First login creates one private exact-name repo; ten subsequent logins; two concurrent creations; unrelated existing files; public/inaccessible/deleted/renamed Notes | Stub contract and UI screenshots; protected real-GitHub experiment |
| Sync acknowledgement | Offline restart, timeout after remote success, 401/403/404/409/422/429, remote manual Markdown edit and unknown metadata | Proven remote confirmation before deletion; queue and full payload absent after restart when synced |
| Accounts and rights | Logout/change account retains pending records but isolates views; grant/revoke shared write; stale enabled button; private Notes unaffected | UI and independent stub no-unauthorized-write assertion |
| WWW read-only | Complete public content with no login/private-note/edit affordance, even for a GitHub collaborator | JS on/off screenshots; no token or private API request |
| Program update | Signed compatible package, invalid signature, target mismatch, interrupted install and recovery, Flatpak ownership | OS installer test + CodeceptJS note-before/after and screenshot; not merely new SQLite |
| Static SEO | Direct deep URL, refresh, 404, canonical/hreflang/lang/sitemap, no-JS text and link traversal | Separate static HTTP/HTML assertions and CodeceptJS no-JS pages |
| Full content | Every key/value and relational/FTS consistency against independent approved golden ZIP | Separate <=60-second verifier; never sample 31,000 UI pages as a substitute |
| App/store metadata | Existing download assets, hashes, locale key/placeholder parity, valid AppStream and PL/EN store fields | Metadata checks + PL/EN Application page and listing screenshot gallery |

## Screenshot review standard

Store approved PNGs at `tests/visual/baseline/<platform>/<viewport>/<locale>/<scenario>.png`; generated actual/expected/diff/HTML reports live at `tests/visual/reports/` and in Actions artifacts, not Git. Begin with phone 393×852, tablet 768×1024 and desktop 1365×768 CSS pixels; keep existing baseline geometry while comparing legacy. Use light theme plus dark only where product supports it. Pin fonts and renderers per platform; do not compare Linux glyph rasterization against a Windows baseline.

Capture chapter, comparison, interlinear, dictionary/grammar modal, tags, Fathers, search, language/layer panel, offline, sync conflict, login consent fixture, account rights, upgrade and Application views. Include multi-tab/tab-local Greek mode, narrow Phosh controls, keyboard focus and long EN/PL error text. Consent screenshots use a safe local fake; no real credentials or personal GitHub page is published.

Use `I.saveScreenshot`/`I.saveElementScreenshot` under the DSL and a small local visual comparator. Evaluate the CodeceptJS Resemble integration's current maintenance/license in the first test PR; pin a compatible version or wrap the image-comparison library in one helper. This does not require a paid screenshot service. Missing baselines and unexpected diffs fail. Tolerance is recorded per renderer with a small justified font allowance, not raised to mask a regression. [Visual testing integrations](https://codecept.io/visual/).

Every UI PR provides a two-to-five sentence explanation and before/after table for both locales, plus actual/expected/diff report links. For inline review, use images from committed, sanitized test baselines at immutable commit URLs; generated HTML galleries are downloadable artifacts. An Actions artifact URL alone is not an embedded image. An optional protected static gallery may publish only sanitized reviewed fixtures; it must not deploy untrusted fork code. A documentation-only PR states “No visual changes” and records the prescribed existing regression check.

Intentional changes require a reviewer to inspect affected actuals before selectively updating baselines, followed by a normal comparison. Do not automatically approve changed baselines. Define success as no missing translations, no clipped controls, readable Greek, correct `lang`/accessible names and unchanged navigation focus—not only an acceptable image percentage.

## Small future workflow set

| Proposed workflow | Trigger and jobs | Gate / outputs |
| --- | --- | --- |
| `verify.yml` | Every PR and every default-branch push, without path filters that skip content validation; pinned Node/Python; unit/contracts, locale/metadata, build public snapshot, independent full golden verifier, CodeceptJS three browser engines, no-JS SEO and visual report | `data-integrity` watchdog <=60 s after inputs prepared; missing ZIP/DB/results fail; required checks aggregate even when upstream fails; no secrets on forks |
| `packages.yml` | PR smoke matrix for required platforms; default-branch full matrix; reusable jobs for release; hosted runners from release matrix, lockfile caches | Package launch/contract/GUI checks, Flatpak lint/build, Android/iOS emulator tests where proven; hardware checks explicitly separate |
| `release.yml` | Approved SemVer tag/environment after required tests; reuse validated artifacts; OS upgrade harness, sign, publish immutable assets, verify download manifest, propose store/Flathub updates | No partially successful matrix advertised as a complete release; protected secrets; signed hashes, provenance and PL/EN release/listing assets |
| `pages.yml` | Validated public data/default-branch source or approved release; same renderer, full static URL manifest and data gate, size/time budget, upload then deploy | Atomic static deployment and retained rollback artifact; no private Notes or runtime service |

Use dependency/lockfile hashes for caches and content source hashes for generated-data caches; still run the full reference comparison on every required trigger. Pin Actions revisions/toolchain versions in implementation PRs. Split the fast data/UI response from slower packaging so failures remain understandable. Cancel superseded PR jobs, serialize publication, and do not cancel an in-progress production deployment halfway through. Default `permissions: contents: read`; only dedicated protected publishing jobs obtain `contents: write` or Pages/id-token permissions. Never use untrusted PR code with write secrets or `pull_request_target` checkout-and-execute.

The 60-second goal covers only complete data verification after input artifacts exist. Packaging, browser tests and device evidence have independent time budgets measured by their first implementation PR; do not promise they fit in that minute. Report all timings and upload machine-readable and readable summaries even on failure.

## Confidence levels and final Definition of Done

1. **PR:** unit/contracts, complete golden comparison, browser CodeceptJS and visual diffs, locale/store parity and required native smoke. New UI has an executable specification and reviewable PL/EN screenshots.
2. **Default branch:** repeat the data gate; full independent-tab/window, offline and native/emulator matrix on real built artifacts. A passed PR run is not reused as the only merge check.
3. **Release:** signatures/downloads, clean install and upgrade/recovery with pending notes, store metadata, all required target statuses and compatibility manifest. A package can be built while distribution remains blocked.
4. **Physical hardware:** Pixel Phosh, Mint, X13s, Windows WebView2, Android and iOS WKWebView record device/OS/WebView/build/profile, steps, actual pending-record hashes, timings and PASS/FAIL. Hosted browsers/emulators cannot confer this result. Missing evidence stays “awaiting device validation”; optional paid farms require a separate decision.

Each feature PR is complete only when its specification, driver/stub contracts, intended UI baseline changes, translations, data/SEO checks and relevant platform evidence pass. Each unresolved hardware failure has an owner, consequence, reproducible experiment and fallback decision in [decisions](decisions.md). CI reports the user outcome, for example “An offline note survived application 1→2 and disappeared from the pending queue only after GitHub confirmation,” alongside the evidence, rather than reporting a compiler success as functionality.
