#!/usr/bin/env python3
"""Run Wallet03 HTTP acceptance only on the marker-verified canonical SQL test fixture."""
from __future__ import annotations

import argparse
from contextlib import contextmanager
import json
import os
from pathlib import Path
import signal
import socket
import subprocess
import sys
import uuid

ROOT = Path(__file__).resolve().parents[2]
LOCAL = ROOT / ".local" / "kohami-sql-test" / "identity"
CONFIG = LOCAL / "fixture-private.json"
RUNTIME = LOCAL / "runtime-private.json"
PROVISIONER = LOCAL / "provisioner-private.json"
MAINTENANCE_STATE = LOCAL / "issue11-maintenance-state-private.json"
DOTNET = Path("/usr/local/share/dotnet/dotnet")
NATIVE_DLL = ROOT / "tools/native-operations/bin/Release/net10.0/Native.dll"
COMMERCE_DLL = ROOT / "tests/Commerce/bin/Release/net10.0/Commerce.Tests.dll"
API_DLL = ROOT / "apps/api/bin/Release/net10.0/Api.dll"


def connection_properties(connection: str) -> dict[str, list[str]]:
    properties: dict[str, list[str]] = {}
    parts: list[str] = []
    current: list[str] = []
    quote: str | None = None
    index = 0
    while index < len(connection):
        character = connection[index]
        if quote and character == quote:
            if index + 1 < len(connection) and connection[index + 1] == quote:
                current.extend((character, character))
                index += 2
                continue
            quote = None
        elif not quote and character in "\"'":
            quote = character
        elif not quote and character == ";":
            parts.append("".join(current))
            current = []
            index += 1
            continue
        current.append(character)
        index += 1
    parts.append("".join(current))
    for part in parts:
        if not part.strip():
            continue
        key, separator, value = part.partition("=")
        if not separator:
            continue
        key = "".join(character for character in key.strip().casefold() if character.isalnum())
        value = value.strip()
        if len(value) >= 2 and value[0] in "\"'" and value[-1] == value[0]:
            quote = value[0]
            value = value[1:-1].replace(quote * 2, quote)
        properties.setdefault(key, []).append(value)
    return properties


def connection_targets_canonical_test(connection: str, certificate: str, *, require_certificate_binding: bool) -> bool:
    properties = connection_properties(connection)
    one = lambda key: properties.get(key, [])
    server = [value for key in ("datasource", "server", "address", "addr", "networkaddress") for value in one(key)]
    database = one("initialcatalog") or one("database")
    encrypt = one("encrypt")
    trust = one("trustservercertificate")
    hostname = one("hostnameincertificate")
    cert = one("servercertificate")
    return (
        len(server) == 1 and server[0].casefold() == "localhost,14333"
        and len(database) == 1 and database[0].casefold() == "kohami"
        and len(encrypt) == 1 and encrypt[0].casefold() == "strict"
        and len(trust) == 1 and trust[0].casefold() in {"false", "no", "0"}
        and (len(hostname) == 1 and hostname[0].casefold() == "localhost" if require_certificate_binding else len(hostname) <= 1 and (not hostname or hostname[0].casefold() == "localhost"))
        and (len(cert) == 1 and Path(cert[0]).resolve() == Path(certificate).resolve() if require_certificate_binding else len(cert) <= 1 and (not cert or Path(cert[0]).resolve() == Path(certificate).resolve()))
    )


def _private_file(path: Path) -> bool:
    return path.is_file() and not path.is_symlink() and (os.name == "nt" or path.stat().st_mode & 0o077 == 0)


def verify_fixture_config(local: Path = LOCAL, repo: Path = ROOT) -> Path:
    repo = repo.resolve(strict=True)
    expected = repo / ".local" / "kohami-sql-test" / "identity"
    local = local.absolute()
    if local.is_symlink() or not local.is_dir():
        raise RuntimeError("WALLET03_CANONICAL_PROFILE_PATH_REQUIRED")
    current = Path(local.anchor)
    for component in local.parts[1:]:
        current /= component
        if current.is_symlink():
            raise RuntimeError("WALLET03_PROFILE_SYMLINK_REFUSED")
    if local.resolve(strict=True) != expected:
        raise RuntimeError("WALLET03_CANONICAL_PROFILE_PATH_REQUIRED")
    if os.name != "nt" and local.stat().st_mode & 0o077:
        raise RuntimeError("WALLET03_PROFILE_PERMISSIONS_INVALID")
    config_path = local / "fixture-private.json"
    runtime_path = local / "runtime-private.json"
    provisioner_path = local / "provisioner-private.json"
    if any(not _private_file(path) for path in (config_path, runtime_path, provisioner_path)):
        raise RuntimeError("WALLET03_FIXTURE_CONFIG_INVALID")
    try:
        config = json.loads(config_path.read_text())
        runtime = json.loads(runtime_path.read_text())
        provisioner = json.loads(provisioner_path.read_text())
    except (OSError, UnicodeError, json.JSONDecodeError):
        raise RuntimeError("WALLET03_FIXTURE_CONFIG_INVALID") from None
    certificate = config.get("sqlCertificatePath")
    if not isinstance(certificate, str) or not certificate:
        raise RuntimeError("WALLET03_SQL_CERTIFICATE_INVALID")
    certificate_path = Path(certificate)
    if not certificate_path.is_file() or certificate_path.is_symlink():
        raise RuntimeError("WALLET03_SQL_CERTIFICATE_INVALID")
    expected_common = {
        "datasetId": "kohami_local", "origin": "https://localhost:3000",
        "identityNamespace": "kohami.identity_access.v1", "sqlServerName": "localhost",
    }
    if any(config.get(key) != value or runtime.get(key) != value for key, value in expected_common.items()):
        raise RuntimeError("WALLET03_CANONICAL_FIXTURE_CONFIG_REQUIRED")
    if (config.get("IdentitySqlTestDedicated") is not True or config.get("KohamiSqlTestDedicated") is not True
            or config.get("port") != 14333 or runtime.get("sqlCertificatePath") != certificate
            or not isinstance(provisioner.get("fixtureId"), str)):
        raise RuntimeError("WALLET03_CANONICAL_FIXTURE_CONFIG_REQUIRED")
    for key in ("connectionString", "workerConnectionString"):
        if not connection_targets_canonical_test(config.get(key, ""), certificate, require_certificate_binding=True):
            raise RuntimeError("WALLET03_SQL_TARGET_INVALID")
    if not connection_targets_canonical_test(runtime.get("connectionString", ""), certificate, require_certificate_binding=False):
        raise RuntimeError("WALLET03_SQL_TARGET_INVALID")
    if runtime.get("workerConnectionString") and not connection_targets_canonical_test(runtime["workerConnectionString"], certificate, require_certificate_binding=False):
        raise RuntimeError("WALLET03_SQL_TARGET_INVALID")
    return local.resolve(strict=True)


def validate_port(port: int) -> int:
    if type(port) is not int or not 5080 <= port <= 5099:
        raise RuntimeError("WALLET03_API_PORT_INVALID")
    with socket.socket() as probe:
        try:
            probe.bind(("127.0.0.1", port))
        except OSError:
            raise RuntimeError("WALLET03_API_PORT_IN_USE") from None
    return port


def run(args: list[str], *, capture: bool = False, timeout: int = 120) -> subprocess.CompletedProcess[str]:
    try:
        return subprocess.run(args, cwd=ROOT, env=os.environ.copy(), stdin=subprocess.DEVNULL,
                              stdout=subprocess.PIPE if capture else None,
                              stderr=subprocess.PIPE if capture else None, text=True,
                              check=True, timeout=timeout)
    except (OSError, subprocess.SubprocessError):
        raise RuntimeError("WALLET03_ACCEPTANCE_COMMAND_FAILED") from None


def verify_marker() -> None:
    env = os.environ.copy()
    env["KOHAMI_LOCAL_DIR"] = str(LOCAL)
    env["KOHAMI_FIXTURE_PURPOSE"] = "kohami-sql-test"
    try:
        subprocess.run([str(DOTNET), str(NATIVE_DLL), "fixture-status", str(ROOT), "14333", "--local-dir", str(LOCAL)],
                       cwd=ROOT, env=env, stdin=subprocess.DEVNULL, stdout=subprocess.DEVNULL,
                       stderr=subprocess.DEVNULL, check=True, timeout=30)
    except (OSError, subprocess.SubprocessError):
        raise RuntimeError("WALLET03_CANONICAL_FIXTURE_MARKER_UNVERIFIED") from None


def require_maintenance() -> None:
    result = run([str(DOTNET), str(COMMERCE_DLL), "issue11-fixture-check", str(CONFIG)], capture=True)
    if result.stdout.strip().splitlines() != [
        "PASS Issue11 SQL target is marker-verified canonical kohami-sql-test",
        "ISSUE11_MAINTENANCE_REQUIRED",
    ]:
        raise RuntimeError("WALLET03_FIXTURE_MAINTENANCE_NOT_REQUIRED")


@contextmanager
def maintenance_window():
    require_maintenance()
    if MAINTENANCE_STATE.exists() or MAINTENANCE_STATE.is_symlink():
        raise RuntimeError("WALLET03_MAINTENANCE_STATE_ALREADY_EXISTS")
    opened = False
    try:
        result = run([str(DOTNET), str(COMMERCE_DLL), "issue11-maintenance-window", str(CONFIG), "open", str(MAINTENANCE_STATE)], capture=True)
        opened = MAINTENANCE_STATE.exists()
        if (not opened or not result.stdout.startswith(
                "PASS Issue11 maintenance disabled only on marker-verified canonical kohami-sql-test")):
            raise RuntimeError("WALLET03_MAINTENANCE_WINDOW_OPEN_UNVERIFIED")
        yield
    finally:
        if opened or MAINTENANCE_STATE.exists():
            result = run([str(DOTNET), str(COMMERCE_DLL), "issue11-maintenance-window", str(CONFIG), "restore", str(MAINTENANCE_STATE)], capture=True)
            if (not result.stdout.startswith("PASS Issue11 fixture Maintenance and complete SecurityState restored")
                    or MAINTENANCE_STATE.exists()):
                raise RuntimeError("WALLET03_MAINTENANCE_RESTORE_UNVERIFIED")


def free_port() -> int:
    for port in range(5080, 5100):
        try:
            with socket.socket() as probe:
                probe.bind(("127.0.0.1", port))
            return port
        except OSError:
            continue
    raise RuntimeError("WALLET03_NO_FREE_API_PORT")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--port", type=int, help="optional loopback API port; defaults to the first free port in 5080-5099")
    args = parser.parse_args()
    verify_fixture_config()
    verify_marker()
    if not COMMERCE_DLL.is_file() or not API_DLL.is_file() or not NATIVE_DLL.is_file():
        raise RuntimeError("WALLET03_ACCEPTANCE_BUILD_REQUIRED")
    port = validate_port(args.port) if args.port is not None else free_port()
    with maintenance_window():
        run([str(DOTNET), str(COMMERCE_DLL), "--wallet03-http", str(ROOT), str(LOCAL), str(port)], timeout=300)
    print("PASS Wallet03 dedicated kohami-sql-test HTTP acceptance; fixture Maintenance and SecurityState restored")
    return 0


if __name__ == "__main__":
    def restore_on_termination(signum, _frame):
        raise KeyboardInterrupt(f"signal {signum}")

    signal.signal(signal.SIGTERM, restore_on_termination)
    try:
        raise SystemExit(main())
    except RuntimeError as error:
        print(str(error), file=sys.stderr)
        raise SystemExit(1)
