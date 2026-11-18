# GitHub identity, private Notes and shared edits

Status: architectural decision and future acceptance plan, checked against official documentation on 2026-11-18. No authentication experiment or application change is included here.

## Decision and current implementation

The native product uses a GitHub App **user access token obtained through Device Flow**, direct GitHub REST requests and one JavaScript synchronization implementation. GitHub Pages is a production **read-only** product: no login, private notes, editing controls or token exchange. This is an accepted outcome, not a delayed backend project. No broker, Worker, serverless function, proxy, PAT, embedded client secret, App private key or runtime Git checkout is permitted. Actions only builds/tests/publishes public material.

Today [LoginController](../../KazdyDzienZJezusem/Controllers/LoginController.cs) accepts a pasted string on `/login`; this is not OAuth. [EditorSessions](../../KazdyDzienZJezusem/Services/EditorSessions.cs) stores protected credentials in server memory/Redis and issues an opaque HttpOnly cookie. [GitHubWriteAccess](../../KazdyDzienZJezusem/Services/GitHubWriteAccess.cs) reads repository `permissions.push`; [TagEditingService](../../KazdyDzienZJezusem/Services/TagEditingService.cs) serializes writes with a process semaphore. [TagGitStore](../../KazdyDzienZJezusem/Services/TagGitStore.cs) fetches/resets a dedicated clone, applies a semantic fragment addition, commits and pushes with up to three retries, then reconciles ambiguous pushes. Its safety properties must survive the REST replacement; its server sessions, separate read credential and clone must not.

## What the provider actually supports

| Question | Verified documentation and implication |
| --- | --- |
| Native login without a secret? | GitHub documents Device Flow for desktop apps, enabled in the App registration. Request a device code with the public client ID, then poll with device code and grant type; no client secret is specified. [GitHub user tokens](https://docs.github.com/en/apps/creating-github-apps/authenticating-with-a-github-app/generating-a-user-access-token-for-a-github-app) |
| Refresh without a secret? | The refresh endpoint explicitly exempts device-issued user tokens from its client-secret requirement. Expiring user tokens normally last eight hours and refresh tokens six months; use returned expiry fields. [Refresh](https://docs.github.com/en/apps/creating-github-apps/authenticating-with-a-github-app/refreshing-user-access-tokens) |
| Does PKCE make Pages login safe? | PKCE is supported, but the documented GitHub App authorization-code exchange still requires a secret. PKCE is not a substitute for that requirement and is not used in Device Flow. [PKCE announcement](https://github.blog/changelog/2025-07-14-pkce-support-for-oauth-and-github-app-authentication/), [token endpoint](https://docs.github.com/en/apps/creating-github-apps/authenticating-with-a-github-app/generating-a-user-access-token-for-a-github-app) |
| Does API CORS prove browser Device Flow? | No. GitHub documents CORS for `api.github.com` REST requests; that does not establish browser access to `github.com/login/device/code`, token exchange or refresh. No complete supported secretless browser flow was established by this audit. [REST CORS](https://docs.github.com/en/rest/using-the-rest-api/using-cors-and-jsonp-to-make-cross-origin-requests) |
| Can an App create personal Notes? | `POST /user/repos` accepts GitHub App user tokens with repository `Repository creation:write` or `Administration:write`. Choose the narrower creation permission, not general administration. Explicitly send `private:true`; its default is false. [Create repository](https://docs.github.com/en/rest/repos/repos#create-a-repository-for-the-authenticated-user) |
| Does installation equal login? | No. Account authorization and installation/repository selection are separate. Apps are automatically granted access to repositories they create, according to the installation guide. Check the actual resulting access before sending notes. [Installation](https://docs.github.com/en/apps/using-github-apps/installing-a-github-app-from-a-third-party) |

These findings establish a documented native design, not a completed integration. A future controlled-account experiment must exercise actual registration permissions, installation, creation, access-token refresh and revoked grants. A future writable web proposal needs a real browser trace proving initial authorization, token exchange, CORS and refresh with no secret or intermediary, plus a reviewed token-security design. Until all are demonstrated, Pages remains read-only without blocking migration.

## Registration, permissions and native transport

Register one GitHub App with Device Flow enabled and expiring user tokens. The public client ID/App slug belongs in generated public configuration. Minimum requested repository permissions: Metadata read, Contents write and Repository creation write. Request Pull requests write only when implementing the protected-branch contribution path. Do not request Workflows, organization administration, email or general Administration solely for this design. Keep registration changes explicit: installation approval after a permission change is a real user-visible step.

The shared JS modules own polling state, account verification, repository identity, permissions, conflict rules and retries. A narrow native transport owns HTTPS and secret handles. Permit only the necessary `github.com` device/token paths and `api.github.com` REST endpoints; reject arbitrary URLs and redirects carrying Authorization to another host. Response bodies are size bounded and redacted. Prefer keeping access/refresh values inside that transport and returning opaque session handles, identity and expiry to JS. UI components cannot issue arbitrary authenticated requests. The transport must not become a second GitHub business layer.

Store credentials using an audited OS-backed facility: Windows Credential Manager/DPAPI-backed storage, Apple Keychain, Android Keystore-backed storage, Linux Secret Service where available. Availability in Phosh/Flatpak and sandbox portal access is an explicit experiment. A password-unlocked encrypted vault is a possible separately reviewed option; an encryption key packaged beside a vault is not protection. If secure persistence is unavailable, keep credentials in memory and require Device Flow again after restart; never fall back to plaintext tokens. This does not remove the pending queue. Serialize refresh per account and atomically replace rotating credentials; a lost refresh response can require reauthorization, not deletion of notes.

## Login and exactly one private personal repository

1. Clicking **Sign in with GitHub** preserves reader state and existing pending edits. JS obtains a device/user code through native HTTP and opens the official verification page in the system browser. Show the code, expiry and cancel action; never log device/access/refresh codes. Poll at the supplied interval, adding the instructed delay on `slow_down`; stop on cancellation/expiry/denial.
2. Verify `GET /user` and retain the numeric user ID as the account partition. A token or installation alone is not identity. Anonymous drafts stay under a separate local account key until the user explicitly accepts assignment to this numeric identity.
3. Guide installation on the **personal** account. Selected repositories is preferred; explain the creation permission and private storage. If `Notes` exists, guide selection of that repository. If no repository exists, exercise the create-then-access path permitted by the provider; the controlled-account experiment must verify the exact install sequence, including an account with no repositories.
4. Look up a previously bound numeric repository ID first. Revalidate the returned owner ID, exact name `Notes`, private visibility and access. This prevents a rename/deletion/transfer from silently creating a replacement. For a first connection, inspect `<current-login>/Notes`. A 404 is ambiguous: check account authorization and installation access before treating it as absence.
5. If absent and creation is authorized, `POST /user/repos` with `{name:"Notes", private:true, auto_init:true, has_issues:false, has_projects:false, has_wiki:false}`. A README initializes the branch without a special empty-repository Git Data path. Then reread metadata and installation-access results; only confirmed private ownership permits writes. Store stable repository ID and discover default branch rather than assuming `master` or `main`.
6. Two devices may race. On 422/existence or an ambiguous response, reread and validate the same `Notes`. Never create `Notes-2` or a per-device repository. GitHub names are case insensitive, so an existing `notes` occupying the name is not permission to silently rename it: explain the exact-name mismatch and request an explicit correction.
7. Existing unrelated files are preserved. Use only `kdzj/notes/<uuid>.md`; ask for adoption of this namespace if a pre-existing repository/namespace is unfamiliar. A public Notes repository, wrong owner, inaccessible repository, deleted bound repository or unexpected renamed identity suspends remote writes and leaves drafts available for export. Never change privacy, transfer ownership or recreate deleted Notes implicitly.
8. Show **Notes connected**, identity and private-repository link only after validation. Ten subsequent logins reuse the same ID. Refreshing the public application repository configuration never changes the Notes binding.

```mermaid
sequenceDiagram
    participant UI as Shared JS UI
    participant N as Native HTTP / secrets
    participant G as GitHub
    UI->>N: Start Device Flow with public client ID
    N->>G: Request device code
    N-->>UI: User code, verification URI, expiry
    UI->>G: User opens official page and approves
    loop Provider interval
      UI->>N: Poll authorization
      N->>G: Exchange device code
    end
    N-->>UI: Opaque session handle
    UI->>N: Verify user and Notes identity
    N->>G: GET /user; inspect or create private Notes
    N-->>UI: Numeric user/repo IDs and validated access
    UI->>UI: Resume pending-only IndexedDB queue
```

## Effective rights to shared content

A reader may use their own Notes regardless of public-repository write access. Shared editing requires the intersection of current user rights, installation access, App permissions and branch rules. No local allowlist grants access. Keep an allowlist of **editable paths**, initially `tags/*.md`, not an allowlist of people. Do not add Bible-source editing until its validation and licensing rules are specified in a separate feature PR.

Refresh identity/access at login, entering an editor, focus and explicit refresh, with a maximum 60-second UI cache. Before every dispatch revalidate repository identity and permissions; the authenticated write itself remains authoritative. Loss of public write freezes shared operations as `blocked-permission` metadata on pending records (no automatic background retry until deliberate permission refresh). It does not block private Notes. Offline shared edits are marked unverified drafts, not promises of publication.

Use repository metadata `permissions.push` and, when accessible, `GET /repos/{owner}/{repo}/collaborators/{login}/permission`. The documented fine-grained permission is Metadata read; returned `permission` maps maintain to write and triage to read, while `role_name` includes custom names. Do not infer a custom role's capabilities from its name, nor infer denial from a network failure. The endpoint's response documentation also describes an admin result, so its usability for ordinary collaborators must be tested. If it is inaccessible, fail closed for automatic sends, use other authenticated repository metadata where conclusive, and never request broad Administration only to improve a UI check. [Collaborator permissions](https://docs.github.com/en/rest/collaborators/collaborators#get-repository-permissions-for-a-user)

Branch protection is not a permission error to bypass. Where direct write is refused, offer creation of an authorized branch and PR. Record that the change is submitted for review; it is not published until merge and successful public snapshot publication. Once the branch/PR contains the acknowledged full payload, remove the pending payload and fetch status as disposable metadata. Preview can overlay that remote proposed tag in the submitting window with a clear **awaiting review/publication** label; never modify the public immutable snapshot.

## Concrete replacement of runtime Git

| Existing operation in TagGitStore | Shared JS REST operation and concurrency rule |
| --- | --- |
| Dedicated clone, `fetch`, `reset --hard`, `clean` | Read repository/ref metadata and Contents or Git Trees at a pinned commit. No checkout or destructive reset. Paginate tree traversal; detect `truncated` and fetch subtrees rather than silently losing files. |
| Read local Markdown | GET Contents with explicit ref and raw/object media type; preserve bytes, blob SHA and source commit. Reject symlinks/submodules in app-owned editable paths. |
| `add` + `commit` + `push` for one file | PUT Contents with Base64 UTF-8 payload, branch, commit message and previous blob `sha`; omit SHA only for confirmed creation. DELETE Contents also requires current SHA. |
| Atomic multiple-file change | Create blobs, tree based on verified `base_tree`, commit with current head as parent, then PATCH ref with `force:false`. If the head advanced, non-fast-forward is rejected; reread/reconcile and build a new commit. Do not assume the refs endpoint accepts an expected-head CAS parameter. |
| Retry semantic tag add | Reapply validated add-fragment to fresh remote Markdown only if it is safe and preserves unrelated edits; otherwise retain a conflict with both versions. |
| Ambiguous push outcome | Read current file/ref and, if needed, relevant commit history to establish the exact intended effect before acknowledging. Never infer success from 409 or retry a blind overwrite. |
| Protected branch | Create permitted branch/ref, write there, create PR; track remote acceptance separately from published content. |
| Server write token and read token | Current user's opaque native session; public readers download public build artifacts without credentials. |

Contents writes/deletes must be serialized per repository branch by this client; other devices are handled by remote SHA conflicts. ETags support conditional reads, **not** a replacement for the Contents `sha` write precondition. [Contents API](https://docs.github.com/en/rest/repos/contents), [non-forced refs](https://docs.github.com/en/rest/git/refs#update-a-reference)

Proposed JS contracts (documentation only; Promise results, no arbitrary HTTP exposed to views):

```js
github.getUser(session) // { id, login }
github.getRepository({ id, owner, name }, session)
github.ensurePrivateNotesRepo(user, session) // validated { id, ownerId, name, defaultBranch }
github.readFile(repo, path, ref, session) // { markdown, blobSha, commitSha, etag }
github.putFile(repo, path, markdown, baseSha, branch, session)
github.deleteFile(repo, path, baseSha, branch, session)
github.commitBatch(repo, changes, baseCommit, branch, session)
github.getEffectivePermissions(repo, session) // allowed | denied | unknown, reason, checkedAt
github.createChangePullRequest(repo, branch, title, body, session)
notes.syncPending(accountId) // accepted, pending, conflicts; never returns secret values
```

Validate numeric repository identity, owner, paths (no traversal or encoded separators), file mode, Unicode and a proposed 1 MiB note/edit payload cap before dispatch. A cap failure preserves the draft and offers export. Use a reviewed pinned REST API version in one transport configuration; this audit found `2026-03-10` in current endpoint examples, whereas legacy uses `2022-11-28`. Version changes require contract tests.

## Retries, privacy and acceptance gates

401 pauses for refresh/reauthorization; 403 distinguishes access, branch rules and rate limiting; 404 distinguishes missing from inaccessible; 409/422 triggers reread/conflict handling; 429/secondary limits honor `Retry-After`, reset headers and exponential backoff with jitter. Authenticated user-token requests share the user's primary rate budget (commonly 5,000/hour); unauthenticated requests have 60/hour/IP. Secondary/content-generation limits also apply and can change. These are ceilings, not throughput targets: coalesce explicit saves, serialize writes and avoid polling every note independently. [Rate limits](https://docs.github.com/en/rest/using-the-rest-api/rate-limits-for-the-rest-api)

GitHub private visibility does not encrypt the local WebView profile. Numeric account partitioning prevents accidental cross-account display, not forensic access by another OS user. Use OS account/device protection, hide inactive-account drafts and clear RAM/token handles on logout. Optional Outbox encryption needs a separate key-loss/unlock/recovery decision; never claim it exists by virtue of private GitHub. User-requested destructive clearing/reinstall requires an unsent-draft warning and export first. Ordinary logout or update does not delete pending content.

| Future experiment/test | Success and failure behavior |
| --- | --- |
| Real controlled GitHub App: first login, selected repositories, no existing repos, creation, refresh | Private personal Notes accessible with documented least permissions; otherwise native auth gate remains red, no PAT/server fallback. |
| Ten logins and concurrent two-device creation | One stable private Notes ID, no duplicate names. |
| Public/foreign/inaccessible/deleted/renamed Notes, 404 and 422 stubs | No private upload and no silent replacement; retained queue/export. |
| Manual GitHub Markdown edit, then local offline edit | Remote edit appears; divergence preserves both versions and requires resolution. |
| Grant/revoke shared write while an old window is open | UI refresh within its bound; every remote write enforced by GitHub; Notes still works. |
| Protected branch and PR | No bypass/force update; accurate pending-review status and snapshot publication after merge only. |
| Lost upload response; revoked/rotated tokens; 403/429 | Reconcile exact remote result; no duplicate overwrite or queue loss. |
| Native transport and secret facilities on Pixel/Flatpak, Windows, Android, iOS | Endpoint allowlist, no secrets in JS storage/logs/screenshots; re-login fallback if secure persistence unavailable. |
| Read-only Pages without JavaScript and with JavaScript | Full public reading/SEO, no login/edit/Notes UI or credential request. |

Use stub accounts and fixture text in acceptance screenshots; run real API contracts only in a trusted, opt-in CI environment with disposable test repositories and explicit cleanup rules. Actions never receives real users' private Notes for builds, synchronization or diagnostics. See [pending storage and reconciliation](data-and-sync.md) and [test design](testing-and-ci.md).
