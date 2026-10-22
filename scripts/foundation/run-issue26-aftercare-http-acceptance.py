#!/usr/bin/env python3
"""Run the approved Issue26 member aftercare journey on the dedicated SQL fixture."""
from __future__ import annotations

import json
import os
import argparse
from pathlib import Path
import socket
import subprocess
import sys


ROOT = Path(__file__).resolve().parents[2]
DOTNET = Path("/usr/local/share/dotnet/dotnet")
CONFIG = ROOT / ".local/kohami-sql-test/identity/fixture-private.json"
RUNTIME = CONFIG.parent / "runtime-private.json"
COMMERCE = ROOT / "tests/Commerce/bin/Release/net10.0/Commerce.Tests.dll"
NATIVE = ROOT / "tools/native-operations/bin/Release/net10.0/Native.dll"
MAINTENANCE_STATE = CONFIG.parent / "issue11-maintenance-state-private.json"
AFTERCARE_STATE = CONFIG.parent / "issue26-aftercare-private.json"
CANCELLATION_STATE = CONFIG.parent / "issue26-cancellation-private.json"


def run(args: list[str], *, capture: bool = False) -> subprocess.CompletedProcess[str]:
    try:
        return subprocess.run(args, cwd=ROOT, env=os.environ.copy(), stdin=subprocess.DEVNULL,
                              stdout=subprocess.PIPE if capture else None,
                              stderr=subprocess.PIPE if capture else None, text=True,
                              check=True, timeout=180)
    except (OSError, subprocess.SubprocessError):
        raise RuntimeError("BLOCKED: ISSUE26_ACCEPTANCE_COMMAND_FAILED") from None


def verify_config() -> dict:
    if (CONFIG.is_symlink() or not CONFIG.is_file() or CONFIG.stat().st_mode & 0o077
            or RUNTIME.is_symlink() or not RUNTIME.is_file() or RUNTIME.stat().st_mode & 0o077
            or CONFIG.resolve() != CONFIG or not COMMERCE.is_file() or COMMERCE.is_symlink()
            or not NATIVE.is_file() or NATIVE.is_symlink()):
        raise RuntimeError("BLOCKED: ISSUE26_DEDICATED_FIXTURE_CONFIG_INVALID")
    try:
        value = json.loads(CONFIG.read_text())
    except (OSError, UnicodeError, json.JSONDecodeError):
        raise RuntimeError("BLOCKED: ISSUE26_DEDICATED_FIXTURE_CONFIG_INVALID") from None
    if (value.get("datasetId") != "kohami_local" or value.get("sqlServerName") != "localhost"
            or value.get("port") != 14333 or value.get("KohamiSqlTestDedicated") is not True
            or value.get("IdentitySqlTestDedicated") is not True):
        raise RuntimeError("BLOCKED: ISSUE26_DEDICATED_FIXTURE_CONFIG_INVALID")
    return value


def verify_marker() -> None:
    env = os.environ.copy()
    env["KOHAMI_LOCAL_DIR"] = str(CONFIG.parent)
    env["KOHAMI_FIXTURE_PURPOSE"] = "kohami-sql-test"
    try:
        subprocess.run([str(DOTNET), str(NATIVE), "fixture-status", str(ROOT), "14333",
                        "--local-dir", str(CONFIG.parent)], cwd=ROOT, env=env,
                       stdin=subprocess.DEVNULL, stdout=subprocess.DEVNULL,
                       stderr=subprocess.DEVNULL, check=True, timeout=30)
    except (OSError, subprocess.SubprocessError):
        raise RuntimeError("BLOCKED: ISSUE26_DEDICATED_MARKER_UNVERIFIED") from None


def require_maintenance() -> None:
    result = run([str(DOTNET), str(COMMERCE), "issue11-fixture-check", str(CONFIG)], capture=True)
    if result.stdout.strip().splitlines() != [
        "PASS Issue11 SQL target is marker-verified canonical kohami-sql-test",
        "ISSUE11_MAINTENANCE_REQUIRED",
    ]:
        raise RuntimeError("BLOCKED: ISSUE26_FIXTURE_MAINTENANCE_NOT_REQUIRED")


def free_port() -> int:
    for port in range(5080, 5100):
        with socket.socket() as probe:
            if probe.connect_ex(("127.0.0.1", port)) != 0:
                return port
    raise RuntimeError("BLOCKED: ISSUE26_NO_FREE_API_PORT")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--cancellation-only", action="store_true",
                        help="run the bounded member-cancellation HTTP/SQL journey instead of paid-order aftercare")
    args = parser.parse_args()
    verify_config()
    verify_marker()
    if MAINTENANCE_STATE.exists() or AFTERCARE_STATE.exists() or CANCELLATION_STATE.exists():
        raise RuntimeError("BLOCKED: ISSUE26_PRIVATE_STATE_ALREADY_EXISTS")
    require_maintenance()
    opened = False
    try:
        run([str(DOTNET), str(COMMERCE), "issue11-maintenance-window", str(CONFIG), "open", str(MAINTENANCE_STATE)])
        opened = True
        command = "--issue26-cancellation-http" if args.cancellation_only else "--issue26-aftercare-http"
        run([str(DOTNET), str(COMMERCE), command, str(CONFIG), str(RUNTIME), str(free_port())])
    finally:
        if opened or MAINTENANCE_STATE.exists():
            restored = run([str(DOTNET), str(COMMERCE), "issue11-maintenance-window", str(CONFIG), "restore", str(MAINTENANCE_STATE)], capture=True)
            if restored.stdout.strip() != "PASS Issue11 fixture Maintenance and complete SecurityState restored and verified by SQL readback":
                raise RuntimeError("BLOCKED: ISSUE26_SECURITY_STATE_RESTORE_UNVERIFIED")
        if MAINTENANCE_STATE.exists():
            raise RuntimeError("BLOCKED: ISSUE26_MAINTENANCE_STATE_FILE_REMAINS")
        if CANCELLATION_STATE.exists():
            raise RuntimeError("BLOCKED: ISSUE26_CANCELLATION_PRIVATE_STATE_REMAINS")
        if AFTERCARE_STATE.exists():
            raise RuntimeError("BLOCKED: ISSUE26_AFTERCARE_PRIVATE_STATE_REMAINS")
    print("PASS Issue26 dedicated fixture restored to its original Maintenance/SecurityState")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except RuntimeError as error:
        print(str(error), file=sys.stderr)
        raise SystemExit(1)
