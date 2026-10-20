#!/usr/bin/env python3
"""Create a fresh, loopback-only SQL fixture for an authorized M0 work item."""
from __future__ import annotations

import json
import argparse
import os
from pathlib import Path
import re
import secrets
import shutil
import socket
import subprocess
import sys
import time
import uuid
import json

ROOT = Path(__file__).resolve().parents[2]
DEFAULT_LOCAL = ROOT / ".local" / "m0"
DOTNET = Path("/usr/local/share/dotnet/dotnet")
NATIVE = ROOT / "tools/native-operations/bin/Release/net10.0/Native.dll"
COMMERCE = ROOT / "tests/Commerce/bin/Release/net10.0/Commerce.Tests.dll"
COMPOSE = ROOT / "infra/m0/compose.yaml"
DEFAULT_FIXTURE_PORT = 14336
WALLET03_HTTP_DEFAULT_PORT = 14350
LOCAL_DEMO_DEFAULT_PORT = 14351
PURPOSES = {"issue02", "issue06", "wallet03-http", "local-demo"}


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
    last_diagnostic = None
    while time.monotonic() < deadline:
        try:
            result = subprocess.run(
                [str(DOTNET), str(NATIVE), "fixture-status", str(ROOT), str(port)],
                cwd=ROOT,
                env=env,
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                text=True,
                timeout=min(15, max(1, deadline - time.monotonic())),
            )
            if result.returncode == 0:
                print("PASS SQL_FIXTURE_AUTHENTICATED_TLS_READY")
                return
            diagnostic = re.search(r"Diagnostic type=[A-Za-z]+(?: sql-number=-?\d+)?", result.stderr)
            reason = re.search(r"Safe reason code=[A-Z0-9_]+", result.stderr)
            last_diagnostic = diagnostic.group(0) if diagnostic else reason.group(0) if reason else last_diagnostic
        except subprocess.TimeoutExpired:
            # SQL may accept TCP before it can finish TLS and login during cold startup.
            pass
        time.sleep(min(2, max(0, deadline - time.monotonic())))
    suffix = "" if last_diagnostic is None else ":" + last_diagnostic.replace(" ", "_")
    raise RuntimeError("SQL_FIXTURE_AUTHENTICATED_READY_TIMEOUT" + suffix)


def resolve_local_dir(root: Path, configured: str | Path | None, purpose: str | None = None) -> Path:
    if purpose == "local-demo":
        default = root / ".local" / "kohami-demo" / "environment"
    elif purpose == "wallet03-http":
        default = root / ".local" / "kohami-demo" / "wallet03-http"
    else:
        default = root / ".local" / "m0"
    return validate_local_dir(root, Path(configured).expanduser() if configured else default)


def validate_local_dir(root: Path, candidate: Path) -> Path:
    root = Path(os.path.abspath(root))
    local_root = root / ".local"
    if local_root.is_symlink() or not local_root.is_dir():
        raise RuntimeError("LOCAL_ROOT_INVALID")
    candidate = candidate if candidate.is_absolute() else root / candidate
    candidate = Path(os.path.abspath(candidate))
    try:
        relative = candidate.relative_to(local_root)
    except ValueError as error:
        raise RuntimeError("LOCAL_PROFILE_OUTSIDE_ALLOWED_ROOT") from error
    if not relative.parts:
        raise RuntimeError("LOCAL_PROFILE_OUTSIDE_ALLOWED_ROOT")
    parent = local_root
    for component in relative.parts[:-1]:
        parent = parent / component
        if parent.is_symlink() or (parent.exists() and not parent.is_dir()):
            raise RuntimeError("LOCAL_PROFILE_PARENT_INVALID")
    if candidate.is_symlink() or candidate.exists():
        raise RuntimeError("REFUSE_EXISTING_LOCAL_MATERIAL")
    return candidate


def ensure_local_root(root: Path) -> Path:
    local_root = Path(os.path.abspath(root)) / ".local"
    if local_root.is_symlink():
        raise RuntimeError("LOCAL_ROOT_INVALID")
    if not local_root.exists():
        local_root.mkdir(mode=0o700)
    if not local_root.is_dir():
        raise RuntimeError("LOCAL_ROOT_INVALID")
    return local_root


def ensure_local_parent(local: Path) -> None:
    parent = local.parent
    missing: list[Path] = []
    local_root = next((directory for directory in local.parents if directory.name == ".local"), None)
    if local_root is None or local_root.is_symlink() or not local_root.is_dir():
        raise RuntimeError("LOCAL_ROOT_INVALID")
    while parent != local_root:
        if parent.exists():
            if parent.is_symlink() or not parent.is_dir():
                raise RuntimeError("LOCAL_PROFILE_PARENT_INVALID")
        else:
            missing.append(parent)
        parent = parent.parent
    for directory in reversed(missing):
        directory.mkdir(mode=0o700)
    current = local_root
    for component in local.parent.relative_to(local_root).parts:
        current = current / component
        if current.is_symlink() or not current.is_dir():
            raise RuntimeError("LOCAL_PROFILE_PARENT_INVALID")


def local_identity(local: Path) -> tuple[int, int]:
    info = local.stat(follow_symlinks=False)
    if not local.is_dir() or local.is_symlink():
        raise RuntimeError("LOCAL_PROFILE_IDENTITY_INVALID")
    return info.st_dev, info.st_ino


def remove_private_fixture(local: Path, expected_identity: tuple[int, int]) -> None:
    if local.is_symlink():
        raise RuntimeError("REFUSE_FIXTURE_CLEANUP_SYMLINK")
    if local.exists():
        if local_identity(local) != expected_identity:
            raise RuntimeError("REFUSE_FIXTURE_CLEANUP_IDENTITY_CHANGED")
        shutil.rmtree(local)


def valid_fixture_port(port: int) -> bool:
    return 14336 <= port <= 14399


def valid_wallet03_http_port(port: int) -> bool:
    return valid_fixture_port(port) and port != 14349


def default_port_for(purpose: str) -> int:
    if purpose == "local-demo":
        return LOCAL_DEMO_DEFAULT_PORT
    return WALLET03_HTTP_DEFAULT_PORT if purpose == "wallet03-http" else DEFAULT_FIXTURE_PORT


def valid_demo_port(port: int) -> bool:
    return valid_fixture_port(port) and port not in {14349, 14350}


def required_ports_for(purpose: str, fixture_port: int) -> tuple[int, ...]:
    return (fixture_port, 5080, 3000) if purpose == "issue02" else (fixture_port,)


def existing_compose_projects() -> set[str]:
    result = subprocess.run(
        ["docker", "compose", "ls", "--all", "--format", "json"],
        cwd=ROOT,
        check=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
        timeout=30,
    )
    projects = json.loads(result.stdout or "[]")
    if not isinstance(projects, list) or any(not isinstance(item, dict) or not isinstance(item.get("Name"), str) for item in projects):
        raise RuntimeError("COMPOSE_PROJECT_INVENTORY_INVALID")
    return {item["Name"] for item in projects}


def unique_compose_project(purpose: str) -> str:
    existing = existing_compose_projects()
    for _ in range(20):
        project = "kohami-" + purpose + "-" + uuid.uuid4().hex[:12]
        if project not in existing:
            return project
    raise RuntimeError("COMPOSE_PROJECT_UNIQUENESS_UNAVAILABLE")


def native_profile_options(local: Path, purpose: str | None = None) -> list[str]:
    options = ["--local-dir", str(local)]
    if purpose in {"wallet03-http", "local-demo"}:
        options.extend(["--fixture-purpose", purpose])
    return options


def main(purpose: str, port: int = DEFAULT_FIXTURE_PORT, local_dir: str | Path | None = None) -> None:
    if purpose not in PURPOSES:
        raise RuntimeError("FIXTURE_PURPOSE_NOT_ALLOWED")
    if not (valid_demo_port(port) if purpose == "local-demo" else valid_wallet03_http_port(port) if purpose == "wallet03-http" else valid_fixture_port(port)):
        raise RuntimeError("FIXTURE_PORT_OUT_OF_RANGE")
    ensure_local_root(ROOT)
    local = resolve_local_dir(ROOT, local_dir, purpose)
    if not DOTNET.is_file() or not NATIVE.is_file() or not COMMERCE.is_file():
        raise RuntimeError("BUILD_NATIVE_AND_COMMERCE_RELEASE_FIRST")
    if not COMPOSE.is_file():
        raise RuntimeError("COMPOSE_SOURCE_MISSING")
    for required_port in required_ports_for(purpose, port):
        require_free(required_port)

    project = unique_compose_project(purpose)
    ensure_local_parent(local)
    project_file = local / (purpose + "-compose-project")
    local.mkdir(mode=0o700)
    reserved_identity = local_identity(local)
    started = False
    complete = False
    try:
        local_env = os.environ.copy()
        local_env["KOHAMI_M0_LOCAL_DIR"] = str(local)
        local_env["KOHAMI_M0_LOCAL_RESERVED"] = "1"
        run([sys.executable, str(ROOT / "scripts/m0/provision-local.py")], env=local_env)
        if local_identity(local) != reserved_identity:
            raise RuntimeError("LOCAL_PROFILE_IDENTITY_CHANGED")
        runtime_path = local / "runtime-private.json"
        worker_path = local / "worker-private.json"
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

        secrets_path = local / "provisioner-private.json"
        secrets_data = json.loads(secrets_path.read_text())
        fixture_id = str(uuid.uuid4())
        secrets_data["fixtureId"] = fixture_id
        secrets_path.write_text(json.dumps(secrets_data, separators=(",", ":")))
        secrets_path.chmod(0o600)
        project_file.write_text(project + "\n")
        project_file.chmod(0o600)

        env = os.environ.copy()
        env["M0_SQL_PORT"] = str(port)
        env["KOHAMI_M0_LOCAL_DIR"] = str(local)
        started = True
        run(["docker", "compose", "-p", project, "-f", str(COMPOSE), "up", "-d"], env=env)
        wait_sql(port)
        wait_sql_ready(env, port)

        local_arg = native_profile_options(local)
        run([str(DOTNET), str(NATIVE), "provision-fixture", str(ROOT), str(port), *local_arg], env=env)
        config_arg = native_profile_options(local, purpose)
        run([str(DOTNET), str(NATIVE), "write-fixture-config", str(ROOT), str(port), *config_arg], env=env)
        env = os.environ.copy()
        env["KOHAMI_M0_LOCAL_DIR"] = str(local)
        env["KOHAMI_M0_TEST_CONFIG"] = str(local / "fixture-private.json")
        run([str(DOTNET), str(COMMERCE)], env=env, timeout=300)
        if purpose == "issue02":
            run([str(DOTNET), str(NATIVE), "seed-issue02-owner", str(ROOT), str(port), *local_arg], env=env)
        elif purpose == "wallet03-http":
            run([str(DOTNET), str(NATIVE), "wallet03-http-activate", str(ROOT), str(port), *local_arg], env=env)
        elif purpose == "local-demo":
            run([str(DOTNET), str(NATIVE), "verify-local-demo-catalog", str(ROOT), str(port), *local_arg], env=env)
        run([str(DOTNET), str(NATIVE), "initialize-rings", str(runtime_path)])
        complete = True
        if purpose == "issue02":
            print("PASS fresh loopback-only SQL2025 fixture; password-only synthetic KOHAMI account is ready for acceptance")
            print("Fixture remains isolated; account password is held only in the mode-0600 local fixture config.")
        elif purpose == "local-demo":
            print("PASS fresh loopback-only SQL2025 demo fixture; migrations and 15-product catalog seed checked; no identity or grants created")
            print("Demo fixture remains isolated with maintenance unchanged; credentials remain mode-0600 under the private profile.")
        else:
            print("PASS fresh loopback-only SQL2025 fixture for issue06; Commerce SQL checks passed; no Owner created")
            print("Fixture remains isolated and ready for synthetic SQL acceptance; no Owner or native authority was created.")
    finally:
        if not complete:
            safe_to_remove = True
            if started:
                env = os.environ.copy()
                env["M0_SQL_PORT"] = str(port)
                env["KOHAMI_M0_LOCAL_DIR"] = str(local)
                marker_valid = (
                    not project_file.is_symlink()
                    and project_file.is_file()
                    and not (project_file.stat().st_mode & 0o077)
                    and project_file.read_text().strip() == project
                )
                if marker_valid:
                    teardown = subprocess.run(["docker", "compose", "-p", project, "-f", str(COMPOSE), "down", "--volumes"], cwd=ROOT, env=env, check=False, timeout=90)
                    safe_to_remove = teardown.returncode == 0
                else:
                    print("FIXTURE_TEARDOWN_REQUIRED: owned Compose project marker is missing or unsafe", file=sys.stderr)
                    safe_to_remove = False
            if safe_to_remove:
                remove_private_fixture(local, reserved_identity)
            else:
                print("FIXTURE_TEARDOWN_REQUIRED: private profile retained for exact project cleanup", file=sys.stderr)


def run_cli(purpose: str) -> None:
    try:
        parser = argparse.ArgumentParser(description="Create a dedicated disposable local M0 SQL fixture.")
        parser.add_argument("--port", type=int, default=None,
                            help="unused loopback port in the dedicated 14336-14399 fixture range (wallet03-http defaults to 14350; local-demo defaults to 14351)")
        parser.add_argument("--local-dir", type=Path,
                            help="new private profile directory under this repository's .local/ (local-demo defaults to .local/kohami-demo/environment)")
        args = parser.parse_args(sys.argv[1:])
        main(purpose, args.port if args.port is not None else default_port_for(purpose), args.local_dir)
    except Exception as error:
        message = str(error)
        diagnostic = re.fullmatch(
            r"SQL_FIXTURE_AUTHENTICATED_READY_TIMEOUT(?::Diagnostic_type=[A-Za-z]+_sql-number=-?\d+|:Safe_reason_code=[A-Z0-9_]+)?",
            message,
        )
        suffix = ":" + message.replace(" ", "_") if diagnostic else ""
        print("DISPOSABLE_M0_FIXTURE_PROVISION_FAILED:" + type(error).__name__ + suffix, file=sys.stderr)
        raise SystemExit(1)


if __name__ == "__main__":
    run_cli(sys.argv[1] if len(sys.argv) == 2 else "")
