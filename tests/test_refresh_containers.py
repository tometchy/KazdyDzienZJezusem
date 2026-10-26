"""Offline deployment tests. No real Podman commands or connections."""
import importlib.util
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location("refresh", Path(__file__).resolve().parents[1] / "scripts/refresh-containers.py")
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class RefreshTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="kdj-refresh-")
        self.addCleanup(self.temp.cleanup)
        root = Path(self.temp.name) / "checkout"
        root.mkdir()
        (root / "compose.yaml").write_text("services:\n  cloudflared:\n    image: docker.io/cloudflare/cloudflared:latest\n")
        with patch.dict(module.os.environ, {"COMPOSE_PROJECT_NAME": "kdj", "CLOUDFLARE_TUNNEL_TOKEN": "secret-test-token"}):
            self.refresh = module.Refresh(root)
        self.containers = {self.refresh.name(service): True for service in ["redis", "kazdy-dzien", "cloudflared"]}
        self.commands = []
        self.fail_pull = False
        self.fail_create = None
        self.refresh.run = self.run_podman
        self.refresh.app_ready = lambda: True
        self.refresh.tunnel_ready = lambda: True
        self.refresh.wait = lambda ready: self.assertTrue(ready())

    def run_podman(self, *args, check=True):
        self.commands.append(args)
        result = ""
        code = 0
        if args[0] == "pull" and self.fail_pull:
            raise RuntimeError("Pull failed")
        if args[:2] == ("container", "exists"):
            code = 0 if args[2] in self.containers else 1
        elif args[0] == "inspect":
            result = str(self.containers[args[-1]]).lower()
        elif args[0] == "compose":
            service = args[-1]
            if self.fail_create == service:
                raise RuntimeError("Create failed")
            self.containers[args[-2]] = True
        elif args[0] == "stop":
            self.containers[args[-1]] = False
        elif args[0] == "start":
            self.containers[args[1]] = True
        elif args[0] == "rename":
            self.containers[args[2]] = self.containers.pop(args[1])
        elif args[0] == "rm":
            del self.containers[args[1]]
        elif args[0] == "exec":
            result = "PONG" if args[-1] == "ping" else "cloudflared version test"
        return subprocess.CompletedProcess(args, code, result)

    def test_pulls_latest_on_every_refresh_and_never_restarts_redis(self):
        self.refresh.refresh()
        self.refresh.refresh()
        self.assertEqual(2, self.commands.count(("pull", "docker.io/cloudflare/cloudflared:latest")))
        self.assertFalse(any(command[0] in {"stop", "start", "rename", "rm"} and "kdj_redis_1" in command for command in self.commands))
        self.assertFalse(any("down" in command or "up" in command for command in self.commands))
        creates = [command for command in self.commands if command[0] == "compose"]
        self.assertEqual(["kazdy-dzien", "cloudflared"] * 2, [command[-1] for command in creates])
        self.assertTrue(all("--no-deps" in command for command in creates))

    def test_failed_pull_does_not_stop_anything(self):
        self.fail_pull = True
        with self.assertRaisesRegex(RuntimeError, "Pull failed"):
            self.refresh.refresh()
        self.assertEqual([("pull", "docker.io/cloudflare/cloudflared:latest")], self.commands)
        self.assertTrue(all(self.containers.values()))

    def test_failed_creation_restores_previous_app_and_leaves_tunnel_untouched(self):
        self.fail_create = "kazdy-dzien"
        with self.assertRaisesRegex(RuntimeError, "Create failed"):
            self.refresh.refresh()
        self.assertEqual({"kdj_redis_1": True, "kdj_kazdy-dzien_1": True, "kdj_cloudflared_1": True}, self.containers)
        self.assertFalse(any(command[0] in {"stop", "rename"} and "kdj_cloudflared_1" in command for command in self.commands))

    def test_unhealthy_new_app_is_retained_and_old_app_restored(self):
        self.refresh.app_ready = lambda: False
        with self.assertRaises(AssertionError):
            self.refresh.refresh()
        self.assertTrue(self.containers["kdj_kazdy-dzien_1"])
        self.assertTrue(any("_failed_" in name and not running for name, running in self.containers.items()))

    def test_old_tunnel_stays_connected_until_replacement_registers(self):
        def ready():
            self.assertTrue(self.containers["kdj_cloudflared_1"])
            backups = [running for name, running in self.containers.items() if "cloudflared_1_rollback_" in name]
            self.assertEqual([True], backups)
            return True
        self.refresh.tunnel_ready = ready
        self.refresh.refresh()
        self.assertEqual(3, len(self.containers))

    def test_failed_tunnel_restores_old_connector(self):
        self.refresh.tunnel_ready = lambda: False
        with self.assertRaises(AssertionError):
            self.refresh.refresh()
        self.assertTrue(self.containers["kdj_cloudflared_1"])
        self.assertTrue(self.containers["kdj_redis_1"])

    def test_provider_errors_never_expose_tunnel_token(self):
        result = subprocess.CompletedProcess([], 125, "run --token secret-test-token rejected")
        with patch.object(module.subprocess, "run", return_value=result):
            with self.assertRaises(RuntimeError) as error:
                module.Refresh.run(self.refresh, "compose", "run")
        self.assertNotIn("secret-test-token", str(error.exception))
        self.assertIn("[REDACTED]", str(error.exception))

    def test_readiness_requires_current_connections_not_historical_logs(self):
        self.refresh.running = lambda name: True
        with patch.object(self.refresh, "run", return_value=subprocess.CompletedProcess([], 1, "not ready")):
            self.assertFalse(module.Refresh.tunnel_ready(self.refresh))
        with patch.object(self.refresh, "run", return_value=subprocess.CompletedProcess([], 0, "ready")) as command:
            self.assertTrue(module.Refresh.tunnel_ready(self.refresh))
            self.assertIn("ready", command.call_args.args)
            self.assertIn("127.0.0.1:2000", command.call_args.args)


if __name__ == "__main__":
    unittest.main()
