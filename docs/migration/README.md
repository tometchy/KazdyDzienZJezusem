# Didaske migration plan

Architecture review, 19 November 2026. The [repository audit](repository-audit.md)
is based on the reader and corpus at [fe43220](https://github.com/tometchy/KazdyDzienZJezusem/commit/fe4322096769fe01b2e23fb6efa7594cfd6f13b5).
The first planning session had already added the documents on `master` in
[`667db64`](https://github.com/tometchy/KazdyDzienZJezusem/commit/667db64) and
[`5c30761`](https://github.com/tometchy/KazdyDzienZJezusem/commit/5c30761).
This review builds on those documents.

**Didaske** is the name of the reader in Polish and English. Its planned public
address is `https://didaske.com/`. The existing repository and legacy deployment
keep their current identifiers during migration.

The proposed reader keeps Bible comparisons, Greek explanations, Fathers, tags and
literature in one Polish/English interface. The website serves real readable pages
without JavaScript and remains publicly read-only. The installed Tauri app reads
offline and signs into GitHub without a personal access token. Private notes are
ordinary Markdown in the user's private `Notes` repository. Unsent edits live in one
IndexedDB queue and are removed only when GitHub has safely confirmed them.

In TNP, readers see the words of Jesus in bold wherever the source EPUB marks
them. Selecting a footnote star opens its explanation over the passage. The same
interaction applies to a verse comparison, a word's grammar and other study
details. Every detail offers **Open full page** and a choice of four docking
positions: left, right, top or bottom. A docked detail stays on screen while the
reader continues into another passage. A shared link opens the dedicated page
directly. [Reader interactions](architecture.md) · [TNP source evidence](repository-audit.md).

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

## Where to start

The first implementation PR records what a reader sees today, including the current
Greek reading controls, broken comparisons and source notices. Capture the accepted
behavior before porting it. The independent reference archive and the bilingual shell
can then proceed in parallel. [Roadmap and dependencies](migration-roadmap.md).

Three results determine how far the migration can go:

| Question | Evidence needed for a decision |
| --- | --- |
| Will all public reading and study links fit on Pages? | The 31,102 references require 62,204 PL/EN verse pages. Budget the dedicated footnote and word-detail pages too. Sample the actual templates and measure the complete artifact before switching the domain. [SEO budget](seo-and-data-integrity.md). |
| Can someone trust an unsent note on their phone? | Save a note without internet, close the app, update the installed package and reopen it on each supported device. A lost response, a second editor or an unavailable private repository must leave a recoverable copy. [Storage and recovery](data-and-sync.md). |
| Which texts and packages can be distributed? | Record permission to distribute each edition and dictionary in each channel. Test the signed package and update on its intended system. The Pixel result measures database preparation only. [Sources](repository-audit.md) · [Packages](releases.md). |

The owner will need to settle any missing redistribution grants, verify control of
`didaske.com`, reserve package identifiers and arrange signing or store enrollment for the
channels they release. Public reading on the website can proceed independently of
native sign-in. The deliverable for this review is documentation in a PR against
`master`. Implementation, cutover and container refresh are outside this task.
