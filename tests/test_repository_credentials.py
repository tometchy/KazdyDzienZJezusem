"""Offline tests: all Git and Podman executables below are test doubles."""

import base64
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest


ROOT = Path(__file__).resolve().parents[1]


class RepositoryCredentialsTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="kdj-credentials-test-")
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.bin = self.root / "bin"
        self.bin.mkdir()
        self.secret = self.root / "read-token"
        self.secret.write_text("readonly_test_token\n")
        self.env = {
            **os.environ,
            "PATH": f"{self.bin}:{os.environ['PATH']}",
            "KDJ_GITHUB_READ_TOKEN_FILE": str(self.secret),
            "TEST_LOG": str(self.root / "commands"),
        }
        self.header = "Authorization: Basic " + base64.b64encode(
            b"x-access-token:readonly_test_token"
        ).decode()

    def executable(self, name, script):
        path = self.bin / name
        path.write_text("#!/bin/sh\nset -eu\n" + script)
        path.chmod(0o755)

    def run_script(self, path, *args):
        result = subprocess.run(
            ["sh", str(path), *map(str, args)],
            env=self.env, cwd=self.root, text=True, capture_output=True,
        )
        self.assertNotIn("readonly_test_token", result.stdout + result.stderr)
        self.assertNotIn(self.header, result.stdout + result.stderr)
        return result

    def test_clone_uses_process_header_and_credential_free_url(self):
        self.env["EXPECTED_TEST_HEADER"] = self.header
        self.executable("git", """
case " $* " in
    *" clone "*)
        [ "$GIT_CONFIG_VALUE_0" = "$EXPECTED_TEST_HEADER" ]
        [ "$GIT_CONFIG_KEY_0" = 'http.https://github.com/.extraheader' ]
        [ "$GIT_TERMINAL_PROMPT" = 0 ]
        printf '%s\\n' "$*" >> "$TEST_LOG"
        ;;
    *" config "*)
        [ -z "${GIT_CONFIG_VALUE_0:-}" ]
        printf '%s\\n' "$*" >> "$TEST_LOG"
        ;;
    *) exit 1 ;;
esac
""")
        result = self.run_script(ROOT / "scripts/clone-tag-repository.sh", self.secret, self.root / "clone")
        self.assertEqual(0, result.returncode, result.stderr)
        commands = (self.root / "commands").read_text()
        self.assertIn("https://github.com/tometchy/KazdyDzienZJezusem.git", commands)
        self.assertIn("kdj.editorClone true", commands)
        self.assertNotIn("readonly_test_token", commands)
        self.assertNotIn(self.header, commands)

    def test_clone_failure_suppresses_remote_error_with_credentials(self):
        self.executable("git", 'printf "%s\\n" "$GIT_CONFIG_VALUE_0" >&2\nexit 1\n')
        result = self.run_script(ROOT / "scripts/clone-tag-repository.sh", self.secret, self.root / "clone")
        self.assertNotEqual(0, result.returncode)
        self.assertIn("Cannot clone", result.stderr)

    def test_missing_and_empty_secret_fail_before_git(self):
        self.executable("git", 'touch "$TEST_LOG"\nexit 99\n')
        for path in (self.root / "missing", self.secret):
            self.secret.write_text("")
            result = self.run_script(ROOT / "scripts/clone-tag-repository.sh", path)
            self.assertNotEqual(0, result.returncode)
            self.assertFalse((self.root / "commands").exists())

    def prepare_deploy(self):
        checkout = self.root / "checkout"
        checkout.mkdir()
        shutil.copyfile(ROOT / "podman-up.sh", checkout / "podman-up.sh")
        self.executable("git", "exit 0\n")
        self.executable("podman", """
printf '%s\\n' "$*" >> "$TEST_LOG"
if [ "$1" = build ]; then exit "${TEST_BUILD_EXIT:-0}"; fi
""")
        return checkout

    def test_failed_build_never_stops_or_recreates_containers(self):
        checkout = self.prepare_deploy()
        self.env["TEST_BUILD_EXIT"] = "125"
        result = self.run_script(checkout / "podman-up.sh")
        self.assertEqual(125, result.returncode)
        commands = (self.root / "commands").read_text().splitlines()
        self.assertEqual(1, len(commands))
        self.assertTrue(commands[0].startswith("build --no-cache --secret id=github_read_token,src="))
        self.assertNotIn("readonly_test_token", commands[0])

    def test_successful_build_precedes_recreation_and_consumes_directory_argument(self):
        checkout = self.prepare_deploy()
        result = self.run_script(checkout / "podman-up.sh", checkout)
        self.assertEqual(0, result.returncode, result.stderr)
        commands = (self.root / "commands").read_text().splitlines()
        self.assertEqual(3, len(commands))
        self.assertTrue(commands[0].startswith("build --no-cache"))
        self.assertTrue(commands[1].endswith(" down"))
        self.assertTrue(commands[2].endswith(" up --no-build --force-recreate -d"))

    def test_deploy_reads_only_path_from_dotenv_without_executing_it(self):
        checkout = self.prepare_deploy()
        self.env.pop("KDJ_GITHUB_READ_TOKEN_FILE")
        marker = self.root / "must-not-exist"
        (checkout / ".env").write_text(
            f"KDJ_GITHUB_READ_TOKEN_FILE='{self.secret}'\n"
            f"UNRELATED=$(touch {marker})\n"
        )
        result = self.run_script(checkout / "podman-up.sh")
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertFalse(marker.exists())


if __name__ == "__main__":
    unittest.main()
