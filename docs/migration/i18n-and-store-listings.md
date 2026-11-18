# Polish and English from the first production feature

Every future feature PR updates both UI catalogs, help, accessibility, SEO and
affected store text/screenshots. English source-code/documentation language does
not restrict the languages of product content or translation assets. Existing mixed
labels and `<html lang="en">` in the [layout](../../KazdyDzienZJezusem/Views/Shared/_Layout.cshtml)
are an audited defect to fix during the new shell PR, not a template to reproduce.

## Three independent choices

| Axis | Proposed value | Effect |
| --- | --- | --- |
| `uiLocale` | `pl` or `en` | Labels, help, errors, accessibility and formatting only |
| `textSources` | `['TR', 'TNP', 'KJV']` | Which licensed source texts are visible |
| `annotationLayers` | `[{kind:'contextualGloss', locale:'pl'}, {kind:'gloss', locale:'en'}, {kind:'grammar'}, {kind:'pronunciation', convention:'pl-erasmian'}]` | Independent layers over Greek, per reader/tab |

`uiLocale=en` must allow Polish TNP/UBG and Polish contextual glosses; `uiLocale=pl`
must allow KJV and English glosses. Grammar explanation language follows the UI,
but Greek grammar codes and source text do not change. Preserve the separate
Strong-plus-morphology Polish layer and its conservative alternatives. Pronunciation
is a labelled convention, not magically English pronunciation after a locale switch.
German annotations appear only after licensed data exists; the architecture supports
the extra locale without a new global switch or blocking PL/EN delivery.

First launch: use an explicit URL locale on WWW, otherwise a saved device preference,
then supported system/browser language, then Polish fallback. A visible UI switch
changes labels without restart, lost draft, selection or scroll. The semantic verse
reference and layers remain unchanged; WWW updates only the locale path segment and
associated metadata. Preference persistence uses `preferences` in the same IDB,
separate from pending changes. Independent tabs do not jump to another tab's verse
or automatically replace its selected layers when preferences change.

## Catalog and formatting contract

Use stable keys, e.g. `notes.savedLocally`, `sync.conflict`, `grammar.case.dative`,
`application.updateAvailable`, `rights.sourceAttribution`. `t(key, params)` escapes
values; trusted markup is composed by views, never embedded arbitrary HTML in strings.
Use `Intl.NumberFormat`, `DateTimeFormat`, `RelativeTimeFormat` and `PluralRules`
for locale-aware formatting. Define Polish one/few/many/other and English one/other
where count messages need them. Store UTC instants/semantic counts, not formatted
strings. This small formatter is sufficient initially; adopt ICU only for messages
that require it. [Intl reference](https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Global_Objects/Intl).

Missing keys may fall back safely in development but fail CI. CI compares complete
key sets, placeholder names and types, plural branches and empty/unresolved values;
flags accidental identical prose except an explicit proper-name/code allowlist.
Human review still checks meaning. A new locale adds a catalog and registry entry,
not duplicated view logic. Errors travel as stable codes plus safe parameters; GitHub
raw error bodies and tokens are never user-facing translation text.

Set document `lang` to the interface language and source spans to `pl`, `en`, `grc`
as appropriate. Preserve Unicode and original accents. Localize accessible names,
keyboard hints, live regions, focus-return messages, word/help dialogs, offline states,
permissions, update progress, download instructions and attribution explanation.
Do not machine-translate Scripture, dictionary content or attested glosses.

## Localized static pages

Use `/pl/.../` and `/en/.../` with the same stable route vocabulary and reference.
Each real localized page has a self-canonical URL, reciprocal `hreflang=pl/en`, an
`x-default` pointing to the chosen default, localized title/description/navigation,
and unchanged source-language content. A single comparison page per reference per
UI locale shows all permitted translations: no separate indexed page per translation
of that verse. Two locales yield 62,204 comparison pages for the current 31,102-key
union, before legacy aliases and other pages. Budget this duplication explicitly in
[SEO feasibility](seo-and-data-integrity.md). Do not cross-canonical English to Polish
and simultaneously promise both as independently indexed translations. Google can
choose another canonical or omit indexing; sitemap membership is not a guarantee.
[Localized-page guidance](https://developers.google.com/search/docs/specialty/international/localized-versions).

Legacy unprefixed URLs lead to Polish equivalents via small static alias pages with
canonical and visible links; verify host slash behavior rather than assuming server
rewrites. Language links point to the same reference in the other locale. No-JS users
can switch languages and navigate source texts through actual anchors.

## Store source files and review

| Destination | Versioned source proposal | Required PL/EN material and verification |
| --- | --- | --- |
| Flathub | `app/store-listings/flathub/<stable-id>.metainfo.xml` | Default English plus Polish `xml:lang` entries for supported name, summary, description, keywords, screenshot captions and release text; metadata/project license distinct; AppStream and Flathub lint |
| Google Play | `app/store-listings/google-play/{en-US,pl-PL}/` | Title, short/full description, release notes, supported localized graphics; privacy/help/disclosure links; validate provider limits and locale mapping |
| Apple | `app/store-listings/app-store/{en-US,pl}/` | Name, subtitle, description, keywords, promotional/what's-new text where supported, support/privacy URL and device-family screenshots; validate fields against provider schema |

Screenshots live beneath each listing locale and are generated from the same reviewed
build/fixtures used for visual tests. Keep machine metadata (bundle ID, version,
signing identity) locale-independent. A release checklist maps each changed feature
to affected descriptions, help and images; a no-change entry must explain why current
screenshots remain accurate. Store automation stages reviewed metadata in Actions;
publishing requires the release gate and real store accounts, never a translation API.

Flathub supports AppStream translation and validates its metadata; Google supports
localized listing text and graphics; Apple supports localized app information and
screenshots. Provider fallback behavior does not satisfy this project's requirement
to supply and review both languages. Sources checked 2026-11-18:
[Flathub](https://docs.flathub.org/docs/for-app-authors/metainfo-guidelines),
[Google Play](https://support.google.com/googleplay/android-developer/answer/9844778?hl=en),
[App Store Connect](https://developer.apple.com/help/app-store-connect/manage-app-information/localize-app-information).

## Acceptance for every future feature PR

CodeceptJS exercises PL and EN menus, chapter, comparison, dictionary, interlinear,
tags, native login/notes/consent, offline/retry, update and Application downloads.
Capture desktop and narrow mobile images in each locale, plus `UI=en; gloss=pl` and
`UI=pl; gloss=en`, with a visible Layers panel and word details. Add German only when
the data exists. Compare before/after images with fixed fonts, fixture data, viewport,
timezone and clock; review intended text reflow rather than blindly replacing baselines.
Assert no clipping/overflow, keyboard navigation, accessible names, correct `lang`,
preserved draft/history/scroll after locale changes, and no console failures.
Catalog parity and store metadata validation are blocking checks from the shell PR.
