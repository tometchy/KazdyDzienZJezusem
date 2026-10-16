# Screenshot regression tests

`baseline/` contains reviewed, versioned PNGs. `results/` and `report/` contain
ignored actual/diff images, traces and the HTML report from the last run.
The suite covers Bible navigation, verse comparison, Church Fathers, tag details
and a chapter with linked tags, including desktop and mobile Bible/tag pages.

## Laptop and Codex Web

Prepare an environment with Node.js 22, Python 3, curl, tar, make and a C compiler.
Run this from the repository root, with network access during setup:

```bash
bash scripts/setup-visual-tests.sh --with-deps
bash scripts/test-screenshots.sh
```

`--with-deps` installs Chromium's OS libraries/fonts and may require root or sudo.
Omit it if those dependencies are already installed. The script installs .NET 10
locally if needed, builds pinned Redis 8.2.2 without installing a system service,
restores NuGet/npm dependencies and downloads Playwright's pinned Chromium and
Liberation 2.1.5 fonts (including their license). Redis/font archives are checked
against pinned SHA-256 hashes.
Tools are cached under ignored `artifacts/`; setup can be rerun after dependency
changes. Do not use the production `setup`/Podman scripts for these tests.

In Codex Web, use the setup command when preparing the environment (or as its
setup script in legacy environments). Run the test command in each coding task,
after checkout and edits. Setup must be rerun if dependencies or the lockfile
change. Do not start long-lived services during environment preparation:
processes are created afresh by each test run. The browser serves the exact
Unpoly 3.14.3 CDN assets from the pinned npm package and rejects other external
requests, so test execution does not need internet access after setup.

Each run builds the current checkout, starts a private Redis instance on a free
loopback port, then starts the real application at `http://127.0.0.1:5287`.
An occupied application port causes failure instead of testing a stale process.
The same `Program.cs`/`TagRepository` startup used by Compose populates Redis
from versioned `tags/*.md`. Setup verifies that every tag and the fragment index
exist in Redis before running the browser. Both processes and the temporary
Redis directory are cleaned up after success, failure or interruption.

There is **no production dump to export**: `dump.rdb` generation belongs to
`KazdyDzienZJezusemObsolete/Dockerfile`, which current `compose.yaml` does not use.
Current Bible/Fathers data comes from versioned `input/`, and tag definitions
come from `tags/`; the application project copies both during a build.
If raw source data changes, run `python3 convert-input-to-output.py` and review
the generated changes before running tests. Redis persistence is disabled for
tests. No existing Redis instance, local container or production data is touched.

To use separately installed Redis executables, set both `KDJ_REDIS_SERVER` and
`KDJ_REDIS_CLI` to their absolute paths. To test an already running application
instead (without starting or inspecting its Redis), use:

```bash
KDJ_VISUAL_BASE_URL=http://localhost:8081 bash scripts/test-screenshots.sh
```

This also supports an explicit production URL, but pre-merge checks should use
the default mode so they exercise the current checkout.

## Review and approval

```bash
npm --prefix ProductionVisualTests run report
# Only after reviewing intentional visual changes:
bash scripts/test-screenshots.sh --update-snapshots
```

Normal runs never create or update baselines. Review and commit changed PNGs
alongside the UI change. A failed comparison produces expected, actual and diff
images in `results/` and an HTML report in `report/`.

Use Ubuntu 24.04-compatible Linux and the pinned Playwright/Chromium for shared
baselines. The browser uses `ProductionVisualTests/fonts.conf`, which reads only
the downloaded Liberation fonts and excludes the host's font directories and
alias rules. It does not override application CSS. The baseline files intentionally
do not depend on the machine name. Non-Linux runs fail explicitly; use WSL or
the Ubuntu CI runner on another OS. Rendering library differences can still
affect pixels, so review CI differences rather than increasing tolerances.

## Before merge

The `Screenshots` GitHub Actions workflow runs on pull requests and uploads the
report even on failure. Configure its `Screenshot regression` check as required
in the default branch's ruleset/branch protection to enforce it before merging.
The repository workflow alone does not enable branch protection.
