# Agent Guidelines

- When working directly on the user's laptop, stage and commit only the completed task changes, following the laptop Git workflow below. Preserve unrelated staged and unstaged work.
- Do not treat sudden changes in staged files or commits as an agent error. The user may stage or commit files while a session is still in progress.
- Keep repository content in English, even when the conversation is in Polish.
- When working on the user's laptop, after completing application changes and
  passing the required validation, rebuild the application using `compose.yaml`
  and recreate its application container so the new version is immediately live.
  Use `./podman-up.sh`, which also pulls and refreshes Cloudflared on every run.
  The user has authorized these end-of-task rebuilds and refreshes; do not ask for
  confirmation again. Preserve persistent data and avoid restarting unrelated services.
- Read-only Podman diagnostics such as logs, ps, inspect, exec for diagnosis, and local connectivity checks are allowed. Container operations beyond the authorized application rebuild require an explicit user request.

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

## Git workflow on the user's laptop

These rules apply when working directly in the user's laptop checkout, including
`/home/tom/Projects/KazdyDzienZJezusem`. They take precedence over the branch and
pull-request workflow used in hosted environments.

1. Fetch the latest remote state before making changes.
2. Work directly on `master`. Do not create a task branch or a separate worktree.
   If the checkout is on another branch and cannot be safely switched while
   preserving the user's work, report the blocker.
3. Preserve all local and remote commits. Integrate incoming `origin/master`
   changes with a fast-forward or a normal merge; never reset local `master` to
   the remote, rebase published history, or force-push.
4. Make only the changes required for the task. Run the relevant build,
   automated tests, and the required screenshot comparison after the final edits.
   Do not commit or push if required validation fails; report the exact blocker.
5. Stage only the task changes and commit them on `master` with a concise English
   message. Do not include unrelated user changes, even in the same file.
6. Fetch again before pushing. If the remote advanced, integrate its commits
   without discarding either history and rerun validation affected by the merge.
   Push normally to `origin/master`; a rejected push requires another safe sync,
   never a forced push.
7. Verify that the completed task commit is present on remote `master`. Do not
   create a pull request for laptop work. Finish with the commit link and test
   results, or explain any remaining blocker.
8. Run `./podman-up.sh` after validation to build and refresh the application and
   update Cloudflared. `scripts/rebuild.sh` delegates to the same entry point;
   GtdSystem also calls it. Coordinate with GtdSystem using the existing
   `.git/gtd-kdzj-sync.lock` for the entire refresh. Never run a Compose-wide
   `down` or restart a running Redis container. `podman-compose 1.0.6` ignores
   `up --no-deps` for dependency recreation; the refresh helper uses `compose
   run --no-deps` with explicit service names instead. It pulls the latest tunnel
   image before stopping anything, keeps rollback containers until readiness is
   verified, and retains the old connector until the new one registers with
   Cloudflare. Preserve the last working service when a replacement fails.
   Verify the app responds locally, the requested changes are served, the tunnel
   version is updated, and Redis was not restarted. Report these results.

The laptop's `GtdSystem/background-service.sh` handles automatic rebuilds. It must
compare the current commit with the last successfully built commit so both local
and incoming remote commits trigger a rebuild, including local commits already
pushed before the next cycle. Synchronization must preserve both histories and
uncommitted work. A failed build must remain pending for the next cycle. Leave
container builds and restarts to that service or the user except for the authorized
end-of-task application rebuild. Coordinate with the background service to avoid
overlapping builds.

## Git workflow in hosted environments

When implementing a requested change outside the user's laptop (for example,
Codex Cloud/Web):

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
* Do not merge pull requests or deploy changes unless explicitly instructed.
  Normal merges of incoming `origin/master` into laptop `master` are part of the
  authorized laptop workflow.

## Handoff

For laptop work, report the tests and the commit pushed to remote `master`, with
a direct clickable commit link. A pull request is not required.

For hosted work, create a pull request and finish with its direct clickable URL.
Before sharing the URL, verify with GitHub CLI or API that the pull request
exists, is open, and targets the correct repository and default branch.
Preparing a title and description alone does not create a pull request.
