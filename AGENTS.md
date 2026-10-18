# Agent Guidelines

- Do not stage files or create commits. The user manages staging and commits manually.
- Do not treat sudden changes in staged files or commits as an agent error. The user may stage or commit files while a session is still in progress.
- Keep repository content in English, even when the conversation is in Polish.
- Do not run podman build/run scripts, the user will manage this part.
- Diagnostic-only inspection is allowed when the user explicitly permits it, including read-only Podman checks such as logs, ps, inspect, exec for diagnosis, and local connectivity checks. More generally, any read-only diagnostic action that does not start, stop, rebuild, or otherwise mutate a container is allowed. Do not run podman build/run scripts or restart containers unless the user explicitly asks for that action.

# Codex instructions

## Required screenshot checks before merge

- Codex must run the screenshot tests itself in its current execution environment,
  including Codex Cloud/Web, before reporting changes as ready for merge. A GitHub
  Actions workflow is optional and does not replace this check.
- Run commands from the repository root. If dependencies are missing or have
  changed, prepare them with `bash scripts/setup-visual-tests.sh --with-deps`
  while network access is available. Omit `--with-deps` only when Chromium's
  system dependencies are already installed.
- After the final edits, run
  `env -u KDJ_VISUAL_BASE_URL bash scripts/test-screenshots.sh`.
  This must test the current checkout, not production or a previously started
  application. The runner builds the app, starts isolated Redis and HTTP server
  processes, verifies the tag index, and cleans up those processes afterward.
  Do not start containers or export a Redis dump for this check.
- Treat failed comparisons, missing baselines, and setup failures as blockers to
  declaring the changes ready for merge. Fix the cause and rerun the tests; if
  execution is blocked by the environment, report the exact blocker and mark
  validation incomplete. Never substitute a previous run or assume CI will run it.
- Do not update baselines just to make tests pass. For intentional UI changes,
  inspect the affected screenshots, explicitly update the relevant baselines,
  and then rerun the normal comparison without `--update-snapshots`.
- In the handoff, report the command and result, and point to
  `screenshots/results/` and `screenshots/report/` when a comparison fails.
  Passing tests do not authorize merging, deployment, staging, or commits.

## Git workflow

When implementing a requested change:

1. Never push directly to `master` or `main`.
2. Before making changes, fetch the latest remote state.
3. Create a new branch from the current remote default branch.
4. Name branches using:
   `codex/<short-description>`
5. Make only the changes required for the task.
6. Run the relevant build and automated tests before committing.
7. Do not commit if the build or relevant tests fail unless the task specifically requires work on a failing build.
8. Commit all completed changes with a concise descriptive commit message.
9. Push the branch to `origin`.
10. Create a pull request using `gh pr create`.
11. Target the repository's default branch.
12. In the pull request description include:

    * what was changed,
    * why it was changed,
    * tests that were executed,
    * any known limitations or remaining issues.
13. Never merge the pull request yourself.
14. Finish the task by providing the pull request URL.

## Safety

* Never force-push to `master` or `main`.
* Never rewrite shared branch history.
* Never expose credentials, tokens, secrets, environment variables containing secrets, or authentication files.
* Do not modify CI/CD workflows unless the task explicitly requires it.
* Do not merge or deploy changes unless explicitly instructed.

## Kończenie pracy

Po każdej zmianie w repozytorium utwórz pull request i zakończ odpowiedź bezpośrednim,
klikalnym linkiem do niego. Przed podaniem linku zweryfikuj za pomocą GitHub CLI lub
API, że pull request rzeczywiście istnieje, jest otwarty i prowadzi do właściwego
repozytorium oraz gałęzi. Nie traktuj samego przygotowania tytułu i opisu PR-a jako
jego utworzenia.
