#!/usr/bin/env python3
"""Refresh the application and latest tunnel, retaining Redis and rollback copies."""
import os
from pathlib import Path
import re
import subprocess
import sys
import time
import urllib.error
import urllib.request
import uuid

from dotenv import dotenv_values
import yaml


class Refresh:
    def __init__(self, directory):
        self.directory = Path(directory).resolve()
        self.environment = {**{k: v for k, v in dotenv_values(self.directory / ".env").items() if v is not None}, **os.environ}
        project = self.environment.get("COMPOSE_PROJECT_NAME", self.directory.name.lower())
        if not re.fullmatch(r"[a-z0-9][a-z0-9_-]*", project):
            raise ValueError("Invalid Compose project name.")
        self.project = project
        self.compose = ["compose", "-p", project, "-f", str(self.directory / "compose.yaml")]
        self.config = yaml.safe_load((self.directory / "compose.yaml").read_text())
        self.token = self.environment.get("CLOUDFLARE_TUNNEL_TOKEN", "")
        if not self.token:
            raise ValueError("Set CLOUDFLARE_TUNNEL_TOKEN before refreshing containers.")

    def run(self, *arguments, check=True):
        result = subprocess.run(["podman", *arguments], env=self.environment, cwd=self.directory,
                                text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
        if check and result.returncode:
            # Compose 1.0.6 echoes the command, including the tunnel credential.
            output = result.stdout.replace(self.token, "[REDACTED]")
            output = re.sub(r"(--token(?:=|\s+))\S+", r"\1[REDACTED]", output)
            raise RuntimeError(f"Podman {arguments[0]} failed ({result.returncode}).\n{output}")
        return result

    def name(self, service):
        return f"{self.project}_{service}_1"

    def exists(self, name):
        result = self.run("container", "exists", name, check=False)
        if result.returncode not in (0, 1):
            raise RuntimeError("Cannot inspect container existence; refresh aborted.")
        return result.returncode == 0

    def running(self, name):
        return self.run("inspect", "--format", "{{.State.Running}}", name).stdout.strip() == "true"

    def create(self, service):
        self.run(*self.compose, "run", "--no-deps", "--service-ports", "-T", "-d",
                 "--name", self.name(service), service)

    def wait(self, ready, seconds=90):
        deadline = time.monotonic() + seconds
        while time.monotonic() < deadline:
            if ready():
                return
            time.sleep(1)
        raise RuntimeError("Container readiness check timed out; restoring the previous service.")

    def app_ready(self):
        if not self.running(self.name("kazdy-dzien")):
            return False
        try:
            with urllib.request.urlopen("http://127.0.0.1:8081/Biblia", timeout=3) as response:
                return response.status == 200
        except (OSError, urllib.error.URLError):
            return False

    def tunnel_ready(self):
        name = self.name("cloudflared")
        # Unlike a historical log entry, /ready checks current connectivity.
        return self.running(name) and self.run("exec", name, "cloudflared", "tunnel",
            "--metrics", "127.0.0.1:2000", "ready", check=False).returncode == 0

    def replace(self, service, ready, overlap=False):
        name = self.name(service)
        previous = f"{name}_rollback_{uuid.uuid4().hex[:12]}" if self.exists(name) else None
        was_running = self.running(name) if previous else False
        renamed = False
        try:
            if previous:
                if was_running and not overlap:
                    self.run("stop", "-t", "10", name)
                self.run("rename", name, previous)
                renamed = True
            self.create(service)
            self.wait(ready)
        except BaseException:
            if renamed or not previous:
                if self.exists(name):
                    self.run("stop", "-t", "10", name)
                    self.run("rename", name, f"{name}_failed_{uuid.uuid4().hex[:12]}")
                if renamed:
                    self.run("rename", previous, name)
                    if was_running:
                        self.run("start", name)
            elif was_running:
                self.run("start", name)
            raise
        if previous:
            if overlap and was_running:
                self.run("stop", "-t", "10", previous)
            self.run("rm", previous)  # No -v: never delete persistent volumes.
        print(f"Refreshed {service}; readiness verified.", flush=True)

    def refresh(self):
        image = self.config["services"]["cloudflared"]["image"]
        # Pull before stopping anything. `:latest` alone reuses the cached image.
        self.run("pull", image)
        print("Pulled the latest Cloudflared image.", flush=True)
        redis = self.name("redis")
        if not self.exists(redis):
            self.create("redis")
        elif not self.running(redis):
            self.run("start", redis)
        self.wait(lambda: self.run("exec", redis, "redis-cli", "ping", check=False).stdout.strip() == "PONG")
        self.replace("kazdy-dzien", self.app_ready)
        # Keep the old connector connected until the new one registers.
        self.replace("cloudflared", self.tunnel_ready, overlap=True)
        version = self.run("exec", self.name("cloudflared"), "cloudflared", "--version").stdout.strip()
        print(version, flush=True)


def main():
    try:
        Refresh(sys.argv[1] if len(sys.argv) > 1 else Path(__file__).resolve().parents[1]).refresh()
        return 0
    except (RuntimeError, ValueError, OSError) as error:
        print(str(error), file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
