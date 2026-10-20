#!/bin/sh
# This script runs inside the image build. Never enable tracing or print Git errors.
set +x
set -eu

secret_file="${1:-/run/secrets/github_read_token}"
repository_dir="${2:-/repository}"
if [ ! -r "$secret_file" ]; then
    echo "The github_read_token build secret is missing or unreadable." >&2
    exit 1
fi
read_token="$(cat "$secret_file")"
case "$read_token" in
    ''|*[!a-zA-Z0-9_]*)
        echo "The github_read_token build secret is empty or invalid." >&2
        exit 1
        ;;
esac

export GIT_TERMINAL_PROMPT=0 GIT_CONFIG_NOSYSTEM=1 GIT_CONFIG_GLOBAL=/dev/null
export GIT_CONFIG_COUNT=1
export GIT_CONFIG_KEY_0=http.https://github.com/.extraheader
GIT_CONFIG_VALUE_0="Authorization: Basic $(printf 'x-access-token:%s' "$read_token" | base64 | tr -d '\n')"
export GIT_CONFIG_VALUE_0
unset read_token
if ! git -c credential.helper= -c core.hooksPath=/dev/null clone --quiet \
    --depth=1 --single-branch --branch master \
    https://github.com/tometchy/KazdyDzienZJezusem.git "$repository_dir" >/dev/null 2>&1; then
    echo "Cannot clone the tag repository. Check the readonly token and network access." >&2
    exit 1
fi
unset GIT_CONFIG_COUNT GIT_CONFIG_KEY_0 GIT_CONFIG_VALUE_0
git -C "$repository_dir" config kdj.editorClone true
