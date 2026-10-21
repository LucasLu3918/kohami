#!/usr/bin/env python3
"""Run real API/SQL wallet checkout acceptance against a marker-verified local demo fixture."""
from __future__ import annotations

import argparse
import json
import os
from pathlib import Path
import re
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]
DOTNET = Path("/usr/local/share/dotnet/dotnet")
NATIVE_DLL = ROOT / "tools/native-operations/bin/Release/net10.0/Native.dll"
COMMERCE_DLL = ROOT / "tests/Commerce/bin/Release/net10.0/Commerce.Tests.dll"


def validate_local_dir(repo: Path, local_dir: Path) -> Path:
    lexical_repo = repo.absolute()
    repo = repo.resolve(strict=True)
    local_root = repo / ".local"
    lexical_local_root = lexical_repo / ".local"
    candidate = local_dir.absolute()
    if local_root.is_symlink() or not local_root.is_dir():
        raise RuntimeError("LOCAL_DEMO_PROFILE_INVALID")
    try:
        lexical_relative = candidate.relative_to(lexical_local_root)
    except ValueError:
        raise RuntimeError("LOCAL_DEMO_PROFILE_OUTSIDE_REPO_LOCAL") from None
    if not lexical_relative.parts:
        raise RuntimeError("LOCAL_DEMO_PROFILE_INVALID")
    lexical_cursor = lexical_local_root
    for part in lexical_relative.parts:
        lexical_cursor = lexical_cursor / part
        if lexical_cursor.is_symlink():
            raise RuntimeError("LOCAL_DEMO_PROFILE_SYMLINK")
    if not candidate.is_dir():
        raise RuntimeError("LOCAL_DEMO_PROFILE_INVALID")
    resolved = candidate.resolve(strict=True)
    try:
        relative = resolved.relative_to(local_root.resolve(strict=True))
    except (ValueError, FileNotFoundError):
        raise RuntimeError("LOCAL_DEMO_PROFILE_OUTSIDE_REPO_LOCAL") from None
    if not relative.parts:
        raise RuntimeError("LOCAL_DEMO_PROFILE_INVALID")
    current = local_root
    for part in relative.parts:
        current = current / part
        if current.is_symlink():
            raise RuntimeError("LOCAL_DEMO_PROFILE_SYMLINK")
    return resolved


def validate_profile(repo: Path, local_dir: Path) -> Path:
    local = validate_local_dir(repo, local_dir)
    required = ("runtime-private.json", "provisioner-private.json", "fixture-private.json", "local-demo-compose-project")
    paths = {name: local / name for name in required}
    if any(path.is_symlink() or not path.is_file() for path in paths.values()):
        raise RuntimeError("LOCAL_DEMO_PROFILE_CONFIG_MISSING")
    if os.name != "nt" and any(path.stat().st_mode & 0o077 for path in paths.values()):
        raise RuntimeError("LOCAL_DEMO_PROFILE_MODE_INVALID")
    try:
        fixture = json.loads(paths["fixture-private.json"].read_text())
        runtime = json.loads(paths["runtime-private.json"].read_text())
        provisioner = json.loads(paths["provisioner-private.json"].read_text())
        port = fixture["port"]
        connection = runtime["connectionString"]
        fixture_id = provisioner["fixtureId"]
        sql_server_name = runtime["sqlServerName"]
        certificate = Path(runtime["sqlCertificatePath"])
    except (OSError, ValueError, KeyError, TypeError):
        raise RuntimeError("LOCAL_DEMO_PROFILE_CONFIG_INVALID") from None
    if fixture.get("KohamiDemoDedicated") is not True:
        raise RuntimeError("LOCAL_DEMO_DEDICATED_FIXTURE_REQUIRED")
    if type(port) is not int or not 14336 <= port <= 14399 or port in {14349, 14350}:
        raise RuntimeError("LOCAL_DEMO_FIXTURE_PORT_INVALID")
    if not re.fullmatch(r"[0-9a-fA-F-]{36}", str(fixture_id)):
        raise RuntimeError("LOCAL_DEMO_FIXTURE_ID_INVALID")
    project = paths["local-demo-compose-project"].read_text().strip()
    if not re.fullmatch(r"kohami-local-demo-[0-9a-f]{12}", project):
        raise RuntimeError("LOCAL_DEMO_COMPOSE_MARKER_INVALID")
    normalized = connection.lower().replace(" ", "")
    if f"server=localhost,{port}" not in normalized and f"data source=localhost,{port}" not in normalized:
        raise RuntimeError("LOCAL_DEMO_SQL_TARGET_INVALID")
    if "database=kohami" not in normalized and "initial catalog=kohami" not in normalized:
        raise RuntimeError("LOCAL_DEMO_SQL_DATABASE_INVALID")
    if "encrypt=strict" not in normalized or "trustservercertificate=false" not in normalized:
        raise RuntimeError("LOCAL_DEMO_SQL_TLS_REQUIRED")
    if sql_server_name != "localhost" or not certificate.is_absolute():
        raise RuntimeError("LOCAL_DEMO_SQL_CERTIFICATE_INVALID")
    try:
        certificate_relative = certificate.resolve(strict=True).relative_to(local.resolve(strict=True))
    except (ValueError, FileNotFoundError):
        raise RuntimeError("LOCAL_DEMO_SQL_CERTIFICATE_INVALID") from None
    cert_cursor = local
    for part in certificate_relative.parts:
        cert_cursor = cert_cursor / part
        if cert_cursor.is_symlink():
            raise RuntimeError("LOCAL_DEMO_SQL_CERTIFICATE_SYMLINK")
    if not certificate.is_file():
        raise RuntimeError("LOCAL_DEMO_SQL_CERTIFICATE_INVALID")
    return local


def validate_api_port(port: int) -> int:
    if type(port) is not int or not 5080 <= port <= 5099:
        raise RuntimeError("LOCAL_DEMO_API_PORT_INVALID")
    return port


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--local-dir", required=True, type=Path)
    parser.add_argument("--port", type=int, default=5088)
    args = parser.parse_args()
    try:
        local = validate_profile(ROOT, args.local_dir)
        port = validate_api_port(args.port)
    except RuntimeError as error:
        print(str(error), file=sys.stderr)
        return 2
    if not DOTNET.is_file() or not NATIVE_DLL.is_file() or not COMMERCE_DLL.is_file():
        print("LOCAL_DEMO_ACCEPTANCE_RELEASE_BUILD_REQUIRED", file=sys.stderr)
        return 2
    env = os.environ.copy()
    env["KOHAMI_API_PORT"] = str(port)
    env["KOHAMI_LOCAL_DIR"] = str(local)
    env["KOHAMI_FIXTURE_PURPOSE"] = "local-demo"
    env["KOHAMI_TEST_CONFIG"] = str(local / "fixture-private.json")
    verify = subprocess.run(
        [str(DOTNET), str(NATIVE_DLL), "verify-local-demo-catalog", str(ROOT), str(json.loads((local / "fixture-private.json").read_text())["port"]), "--local-dir", str(local)],
        cwd=ROOT, env=env, check=False,
    )
    if verify.returncode:
        return verify.returncode
    return subprocess.run(
        [str(DOTNET), str(COMMERCE_DLL), "--local-demo-wallet-http", str(ROOT), str(local), str(port)],
        cwd=ROOT, env=env, check=False,
    ).returncode


if __name__ == "__main__":
    raise SystemExit(main())
