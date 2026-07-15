# Agent Guidelines

- Do not stage files or create commits. The user manages staging and commits manually.
- Do not treat sudden changes in staged files or commits as an agent error. The user may stage or commit files while a session is still in progress.
- Keep repository content in English, even when the conversation is in Polish.
- Do not run podman build/run scripts, the user will manage this part.
- Diagnostic-only inspection is allowed when the user explicitly permits it, including read-only Podman checks such as logs, ps, inspect, exec for diagnosis, and local connectivity checks. More generally, any read-only diagnostic action that does not start, stop, rebuild, or otherwise mutate a container is allowed. Do not run podman build/run scripts or restart containers unless the user explicitly asks for that action.
