# Migration plan: one reader on the web and in an installed app

This is a documentation-only plan dated 2026-11-18. It does not migrate the app,
publish Pages, modify production settings, install a new runtime or remove the POC.
The audited starting commit is `a25f2c4` on `master`, equal to fetched `origin/master`
at the start of the audit. Source data remains in place.

The proposed reader keeps Bible comparisons, Greek explanations, Fathers, tags and
literature in one Polish/English interface. The website serves real readable pages
without JavaScript and remains publicly read-only. The installed Tauri app reads
offline and signs into GitHub without a personal access token. Private notes are
ordinary Markdown in the user's private `Notes` repository. Unsent edits live in one
IndexedDB queue and are removed only when GitHub has safely confirmed them.

This direction is feasible enough to plan, but it is not yet release-ready. The most
important gates are actual IndexedDB persistence across device restarts and upgrades,
private-repository authorization on a real GitHub App, source redistribution rights,
the full bilingual static-site size/deployment budget, and signed installation on
each supported device. The existing Pixel SQLite result does not prove any of those.

## Reading order

| Document | What it answers |
| --- | --- |
| [Repository audit](repository-audit.md) | What exists, what changed since the prompt, source counts, rights and feature parity |
| [Architecture and UX](architecture.md) | One frontend, component boundaries, navigation, multiple tabs and screen sketches |
| [Repository layout](repository-layout.md) | Where future code belongs and when legacy files may be removed |
| [Data and synchronization](data-and-sync.md) | Public schema, contracts, pending-only Outbox, leases and safe acknowledgement |
| [GitHub authentication](github-auth.md) | Device login, exactly one private Notes repo, REST writes and effective rights |
| [SEO and data integrity](seo-and-data-integrity.md) | Every verse's HTML, Pages budget and independent complete golden ZIP check |
| [PL/EN and store listings](i18n-and-store-listings.md) | Independent interface/content languages and translation/screenshot gates |
| [Testing and CI](testing-and-ci.md) | CodeceptJS specifications, drivers/stubs, visual review and hosted automation |
| [Releases](releases.md) | OS/architecture matrix, installable packages, updates, signing and repo rename |
| [Migration roadmap](migration-roadmap.md) | Small PRs, dependencies, acceptance, demonstrations and rollback |
| [Decisions and risks](decisions.md) | Chosen constraints, evidence limits and concrete future experiments |

## Delivery boundary

The task asks for a documentation PR without merge or deploy, while the laptop's
AGENTS.md ordinarily requires direct work/push on master. That conflict was raised
with the owner before any commit or push. The task explicitly excludes the usual
end-of-task `podman-up.sh` refresh here. No application containers are rebuilt by
this planning work. The future architecture puts automated builds and tests in
hosted Actions; physical devices only consume finished artifacts and validate hardware.

Current-checkout validation and the selected Git handoff are recorded in the final
handoff. Future experiments listed here have not been performed by this change and
must not be described as passed in a release or implementation PR.
