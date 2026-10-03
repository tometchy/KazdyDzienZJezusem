# Agent Guidelines

- Do not stage files or create commits. The user manages staging and commits manually.
- Do not treat sudden changes in staged files or commits as an agent error. The user may stage or commit files while a session is still in progress.
- Keep repository content in English, even when the conversation is in Polish.
- Do not run podman build/run scripts, the user will manage this part.
- Diagnostic-only inspection is allowed when the user explicitly permits it, including read-only Podman checks such as logs, ps, inspect, exec for diagnosis, and local connectivity checks. More generally, any read-only diagnostic action that does not start, stop, rebuild, or otherwise mutate a container is allowed. Do not run podman build/run scripts or restart containers unless the user explicitly asks for that action.

# Codex instructions

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
