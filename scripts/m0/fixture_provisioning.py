#!/usr/bin/env python3
"""Create a fresh, loopback-only SQL fixture for an authorized M0 work item."""
from __future__ import annotations

import json
import argparse
import os
from pathlib import Path
import secrets
import shutil
import socket
import subprocess
import sys
import time
import uuid

ROOT = Path(__file__).resolve().parents[2]
LOCAL = ROOT / ".local" / "m0"
DOTNET = Path("/usr/local/share/dotnet/dotnet")
NATIVE = ROOT / "tools/native-operations/bin/Release/net10.0/Native.dll"
COMMERCE = ROOT / "tests/Commerce/bin/Release/net10.0/Commerce.Tests.dll"
COMPOSE = ROOT / "infra/m0/compose.yaml"
DEFAULT_FIXTURE_PORT = 14336
PURPOSES = {"issue02", "issue06"}


def run(args: list[str], *, env: dict[str, str] | None = None, timeout: int = 90) -> None:
    subprocess.run(args, cwd=ROOT, env=env, check=True, timeout=timeout)


def require_free(port: int) -> None:
    with socket.socket() as listener:
        listener.settimeout(0.2)
        if listener.connect_ex(("127.0.0.1", port)) == 0:
            raise RuntimeError(f"PORT_IN_USE:{port}")


def wait_sql(port: int, seconds: int = 60) -> None:
    deadline = time.monotonic() + seconds
    while time.monotonic() < deadline:
        with socket.socket() as listener:
            listener.settimeout(0.2)
            if listener.connect_ex(("127.0.0.1", port)) == 0:
                return
        time.sleep(0.5)
    raise RuntimeError("SQL_FIXTURE_START_TIMEOUT")


def wait_sql_ready(env: dict[str, str], port: int, seconds: int = 90) -> None:
    """Wait for encrypted SQL authentication, not just the early-open TCP socket."""
    deadline = time.monotonic() + seconds
    while time.monotonic() < deadline:
        result = subprocess.run(
            [str(DOTNET), str(NATIVE), "fixture-status", str(ROOT), str(port)],
            cwd=ROOT,
            env=env,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
            timeout=15,
        )
        if result.returncode == 0:
            print("PASS SQL_FIXTURE_AUTHENTICATED_TLS_READY")
            return
        time.sleep(2)
    raise RuntimeError("SQL_FIXTURE_AUTHENTICATED_READY_TIMEOUT")


def remove_private_fixture() -> None:
    if LOCAL.exists():
        shutil.rmtree(LOCAL)


def valid_fixture_port(port: int) -> bool:
    return 14336 <= port <= 14399


def main(purpose: str, port: int = DEFAULT_FIXTURE_PORT) -> None:
    if purpose not in PURPOSES:
        raise RuntimeError("FIXTURE_PURPOSE_NOT_ALLOWED")
    if not valid_fixture_port(port):
        raise RuntimeError("FIXTURE_PORT_OUT_OF_RANGE")
    if LOCAL.exists() or LOCAL.is_symlink():
        raise RuntimeError("REFUSE_EXISTING_LOCAL_MATERIAL")
    if not DOTNET.is_file() or not NATIVE.is_file() or not COMMERCE.is_file():
        raise RuntimeError("BUILD_NATIVE_AND_COMMERCE_RELEASE_FIRST")
    if not COMPOSE.is_file():
        raise RuntimeError("COMPOSE_SOURCE_MISSING")
    required_ports = (port, 5080, 3000) if purpose == "issue02" else (port,)
    for port in required_ports:
        require_free(port)

    project = "kohami-" + purpose + "-" + uuid.uuid4().hex[:12]
    project_file = LOCAL / (purpose + "-compose-project")
    started = False
    complete = False
    try:
        run([sys.executable, str(ROOT / "scripts/m0/provision-local.py")])
        runtime_path = LOCAL / "runtime-private.json"
        worker_path = LOCAL / "worker-private.json"
        runtime = json.loads(runtime_path.read_text())
        worker = json.loads(worker_path.read_text())
        for data, keys in ((runtime, ("connectionString",)), (worker, ("workerConnectionString",))):
            for key in keys:
                data[key] = data[key].replace("localhost,14334", f"localhost,{port}")
                if f"localhost,{port}" not in data[key]:
                    raise RuntimeError("FIXTURE_CONNECTION_TARGET_INVALID")
        runtime_path.write_text(json.dumps(runtime, separators=(",", ":")))
        worker_path.write_text(json.dumps(worker, separators=(",", ":")))
        for path in (runtime_path, worker_path):
            path.chmod(0o600)

        secrets_path = LOCAL / "provisioner-private.json"
        secrets_data = json.loads(secrets_path.read_text())
        fixture_id = str(uuid.uuid4())
        secrets_data["fixtureId"] = fixture_id
        secrets_data["fixtureBootstrap"] = "K0!" + secrets.token_urlsafe(36)
        secrets_path.write_text(json.dumps(secrets_data, separators=(",", ":")))
        secrets_path.chmod(0o600)
        project_file.write_text(project + "\n")
        project_file.chmod(0o600)

        env = os.environ.copy()
        env["M0_SQL_PORT"] = str(port)
        started = True
        run(["docker", "compose", "-p", project, "-f", str(COMPOSE), "up", "-d"], env=env)
        wait_sql(port)
        wait_sql_ready(env, port)

        run([str(DOTNET), str(NATIVE), "provision-fixture", str(ROOT), str(port)])
        run([str(DOTNET), str(NATIVE), "write-fixture-config", str(ROOT), str(port)])
        run([str(DOTNET), str(NATIVE), "development-fixture-bootstrap-checks", str(LOCAL / "fixture-private.json")])
        env = os.environ.copy()
        env["KOHAMI_M0_TEST_CONFIG"] = str(LOCAL / "fixture-private.json")
        run([str(DOTNET), str(COMMERCE)], env=env, timeout=300)
        run([str(DOTNET), str(NATIVE), "initialize-rings", str(runtime_path)])
        complete = True
        print(f"PASS fresh loopback-only SQL2025 fixture for {purpose}; Commerce SQL checks passed; no Owner created")
        if purpose == "issue02":
            print("Next: run the interactive development-fixture-bootstrap command; it requires the real authenticator TOTP proof.")
        else:
            print("Fixture remains isolated and ready for authorized Issue 06 SQL fault/replay evidence; no Owner or native authority was created.")
    finally:
        if not complete:
            if started:
                env = os.environ.copy()
                env["M0_SQL_PORT"] = str(port)
                subprocess.run(["docker", "compose", "-p", project, "-f", str(COMPOSE), "down", "--volumes"], cwd=ROOT, env=env, check=False, timeout=90)
            remove_private_fixture()


def run_cli(purpose: str) -> None:
    try:
        parser = argparse.ArgumentParser(description="Create a dedicated disposable local M0 SQL fixture.")
        parser.add_argument("--port", type=int, default=DEFAULT_FIXTURE_PORT,
                            help="unused loopback port in the dedicated 14336-14399 fixture range")
        args = parser.parse_args(sys.argv[1:])
        main(purpose, args.port)
    except Exception as error:
        print("DISPOSABLE_M0_FIXTURE_PROVISION_FAILED:" + type(error).__name__, file=sys.stderr)
        raise SystemExit(1)


if __name__ == "__main__":
    run_cli(sys.argv[1] if len(sys.argv) == 2 else "")
