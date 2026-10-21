#!/usr/bin/env python3
"""Create the ignored local environment file from the current private SQL profiles."""
import json
import os
from pathlib import Path
import stat
import sys

ROOT = Path(__file__).resolve().parents[2]
TARGET_DIR = ROOT / ".local" / "environments"
TARGET = TARGET_DIR / "local.env"


def parse_connection(value: str) -> dict[str, str]:
    fields = {}
    for part in value.split(";"):
        if not part.strip():
            continue
        key, separator, item = part.partition("=")
        if not separator:
            raise ValueError("PRIVATE_CONNECTION_CONFIG_INVALID")
        fields[key.strip().lower()] = item.strip()
    return fields


def profile(path: Path, key: str) -> dict[str, str]:
    data = json.loads(path.read_text())
    connection = parse_connection(data[key])
    server = connection.get("server", "")
    host, separator, port = server.rpartition(",")
    if not separator or not host or not port:
        raise ValueError("PRIVATE_CONNECTION_TARGET_INVALID")
    return {"host": host, "port": port, "database": connection.get("database", ""),
            "user": connection.get("user id", connection.get("uid", "")),
            "password": connection.get("password", connection.get("pwd", ""))}


def main() -> int:
    private = ROOT / ".local" / "kohami-sql" / "identity"
    runtime = profile(private / "runtime-private.json", "connectionString")
    worker = profile(private / "worker-private.json", "workerConnectionString")
    if runtime["host"] not in {"localhost", "127.0.0.1"} or worker["host"] not in {"localhost", "127.0.0.1"}:
        raise ValueError("LOCAL_SQL_HOST_MUST_BE_LOOPBACK")
    if runtime["database"] != "Kohami" or worker["database"] != runtime["database"]:
        raise ValueError("LOCAL_SQL_DATABASE_MISMATCH")
    TARGET_DIR.mkdir(parents=True, mode=0o700, exist_ok=True)
    os.chmod(TARGET_DIR, 0o700)
    if TARGET.exists() or TARGET.is_symlink():
        raise FileExistsError("LOCAL_ENVIRONMENT_ALREADY_EXISTS")
    values = {
        "KOHAMI_ENVIRONMENT": "local",
        "KOHAMI_SQL_HOST": runtime["host"],
        "KOHAMI_SQL_PORT": runtime["port"],
        "KOHAMI_SQL_DATABASE": runtime["database"],
        "KOHAMI_API_SQL_USER": runtime["user"],
        "KOHAMI_API_SQL_PASSWORD": runtime["password"],
        "KOHAMI_WORKER_SQL_USER": worker["user"],
        "KOHAMI_WORKER_SQL_PASSWORD": worker["password"],
        "KOHAMI_API_PORT": "5083",
        "KOHAMI_SITE_PORT": "3003",
        "KOHAMI_WORKER_ENABLED": "false",
        "KOHAMI_REDIS_ENABLED": "false",
        "KOHAMI_REDIS_URL": "",
        "KOHAMI_DB_VIEWER_PORT": "8978",
        "KOHAMI_DB_VIEWER_SQL_USER": "kohami_reader",
        "KOHAMI_DB_VIEWER_SQL_PASSWORD": "",
    }
    descriptor = os.open(TARGET, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
    with os.fdopen(descriptor, "w") as output:
        for key, value in values.items():
            if "\n" in value or "\r" in value:
                raise ValueError("LOCAL_ENVIRONMENT_VALUE_INVALID")
            output.write(f"{key}={value}\n")
    os.chmod(TARGET, 0o600)
    print("LOCAL_ENVIRONMENT_CREATED_PRIVATE")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ValueError, KeyError, json.JSONDecodeError) as error:
        print(f"BLOCKED: {error}", file=sys.stderr)
        raise SystemExit(1)
