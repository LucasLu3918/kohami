#!/usr/bin/env python3
"""Prepare private TLS and credentials for the persistent local SQL test service."""
from __future__ import annotations

import json
import os
from pathlib import Path
import secrets
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]
LOCAL_ROOT = ROOT / ".local"
LOCAL = LOCAL_ROOT / "kohami-sql-test" / "identity"
PROVISIONER = ROOT / "scripts" / "foundation" / "provision-local.py"


def require_private_directory(path: Path) -> None:
    if path.is_symlink() or (path.exists() and not path.is_dir()):
        raise RuntimeError("LOCAL_PROFILE_INVALID")


def write_json_private(path: Path, value: dict) -> None:
    path.write_text(json.dumps(value, separators=(",", ":")))
    path.chmod(0o600)


def prepare() -> None:
    if LOCAL_ROOT.is_symlink() or not LOCAL_ROOT.is_dir():
        raise RuntimeError("LOCAL_ROOT_INVALID")
    require_private_directory(LOCAL.parent)
    require_private_directory(LOCAL)
    if not LOCAL.exists():
        LOCAL.mkdir(parents=True, mode=0o700)
        if any(LOCAL.iterdir()):
            raise RuntimeError("LOCAL_PROFILE_NOT_EMPTY")
        env = os.environ.copy()
        env["KOHAMI_LOCAL_DIR"] = str(LOCAL)
        env["KOHAMI_LOCAL_RESERVED"] = "1"
        subprocess.run([sys.executable, str(PROVISIONER)], cwd=ROOT, env=env, check=True)

    required = ("runtime-private.json", "worker-private.json", "provisioner-private.json", "sql-container.env", "ca/public.pem", "sql-tls/server.pem", "sql-tls/server.key")
    if any(not (LOCAL / name).is_file() or (LOCAL / name).is_symlink() for name in required):
        raise RuntimeError("LOCAL_PROFILE_INCOMPLETE; inspect before repair")

    ca_bundle = LOCAL / "sql-tls/ca.pem"
    ca_bundle.write_bytes((LOCAL / "ca/public.pem").read_bytes())
    ca_bundle.chmod(0o644)

    for name, connection_name in (("runtime-private.json", "connectionString"), ("worker-private.json", "workerConnectionString")):
        path = LOCAL / name
        settings = json.loads(path.read_text())
        connection = settings.get(connection_name)
        if not isinstance(connection, str):
            raise RuntimeError("SQL_TEST_CONNECTION_TARGET_INVALID")
        if "localhost,14349" in connection:
            settings[connection_name] = connection.replace("localhost,14349", "localhost,14333")
            write_json_private(path, settings)
        elif "localhost,14333" not in connection:
            raise RuntimeError("SQL_TEST_CONNECTION_TARGET_INVALID")

    secrets_path = LOCAL / "provisioner-private.json"
    provisioner = json.loads(secrets_path.read_text())
    secrets_changed = provisioner.get("sourceDatabase") != "KohamiM0"
    provisioner["sourceDatabase"] = "KohamiM0"
    fixture_id = provisioner.get("fixtureId")
    if fixture_id is None:
        provisioner["fixtureId"] = secrets.token_hex(16)
        secrets_changed = True
        write_json_private(secrets_path, provisioner)
    elif not isinstance(fixture_id, str) or len(fixture_id) != 32 or any(c not in "0123456789abcdef" for c in fixture_id):
        raise RuntimeError("FIXTURE_ID_INVALID")
    elif secrets_changed:
        write_json_private(secrets_path, provisioner)

    print("KOHAMI_SQL_TEST_PRIVATE_PROFILE_READY")


if __name__ == "__main__":
    try:
        prepare()
    except (OSError, RuntimeError, json.JSONDecodeError, subprocess.CalledProcessError) as error:
        print("KOHAMI_SQL_TEST_PROFILE_BLOCKED:" + str(error).replace("\n", "_"), file=sys.stderr)
        raise SystemExit(1)
