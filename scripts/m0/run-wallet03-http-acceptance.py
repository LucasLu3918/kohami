#!/usr/bin/env python3
"""Preflight a new local fixture, then run Wallet03's real HTTP acceptance."""
from __future__ import annotations

import argparse
import os
from pathlib import Path
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]
DOTNET = Path("/usr/local/share/dotnet/dotnet")
COMMERCE_DLL = ROOT / "tests/Commerce/bin/Release/net10.0/Commerce.Tests.dll"


def validate_local_dir(repo: Path, local_dir: Path) -> Path:
    repo = repo.resolve(strict=True)
    candidate = local_dir.absolute()
    expected_parent = repo / ".local"
    if expected_parent.is_symlink() or not candidate.is_dir() or candidate.is_symlink():
        raise RuntimeError("WALLET03_LOCAL_DIR_INVALID")
    resolved = candidate.resolve(strict=True)
    try:
        relative = resolved.relative_to(expected_parent.resolve(strict=True))
    except (ValueError, FileNotFoundError):
        raise RuntimeError("WALLET03_LOCAL_DIR_OUTSIDE_REPO_LOCAL") from None
    current = expected_parent
    for part in relative.parts:
        current = current / part
        if current.is_symlink():
            raise RuntimeError("WALLET03_LOCAL_DIR_SYMLINK")
    if not relative.parts:
        raise RuntimeError("WALLET03_LOCAL_DIR_SYMLINK")
    for name in ("runtime-private.json", "provisioner-private.json", "fixture-private.json"):
        path = resolved / name
        if not path.is_file() or path.is_symlink():
            raise RuntimeError("WALLET03_FIXTURE_CONFIG_MISSING")
    return resolved


def validate_port(port: int) -> int:
    if type(port) is not int or not 5080 <= port <= 5099:
        raise RuntimeError("WALLET03_API_PORT_INVALID")
    return port


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--local-dir", required=True, type=Path)
    parser.add_argument("--port", required=True, type=int)
    args = parser.parse_args()
    try:
        local_dir = validate_local_dir(ROOT, args.local_dir)
        port = validate_port(args.port)
    except RuntimeError as error:
        print(str(error), file=sys.stderr)
        return 2
    if not DOTNET.is_file() or not COMMERCE_DLL.is_file():
        print("WALLET03_ACCEPTANCE_BUILD_REQUIRED", file=sys.stderr)
        return 2
    env = os.environ.copy()
    env["KOHAMI_API_PORT"] = str(port)
    # Credentials and connection strings are read only by the process from private files.
    completed = subprocess.run(
        [str(DOTNET), str(COMMERCE_DLL), "--wallet03-http", str(ROOT), str(local_dir), str(port)],
        cwd=ROOT,
        env=env,
        check=False,
    )
    return completed.returncode


if __name__ == "__main__":
    raise SystemExit(main())
