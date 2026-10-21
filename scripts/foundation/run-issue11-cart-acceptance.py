#!/usr/bin/env python3
"""Run Issue11 cart acceptance against the canonical kohami-sql-test service."""
from __future__ import annotations

import json
import os
from pathlib import Path
import signal
import socket
import ssl
import subprocess
import sys
import tempfile
import uuid
import time
import urllib.error
import urllib.request

ROOT = Path(__file__).resolve().parents[2]
LOCAL = Path(os.environ.get("KOHAMI_LOCAL_DIR", str(ROOT / ".local" / "kohami-sql-test" / "identity"))).expanduser()
DOTNET = Path("/usr/local/share/dotnet/dotnet")
API_DLL = ROOT / "apps/api/bin/Release/net10.0/Api.dll"
COMMERCE_DLL = ROOT / "tests/Commerce/bin/Release/net10.0/Commerce.Tests.dll"
PLAYWRIGHT = ROOT / "apps/storefront/node_modules/.bin/playwright"
STOREFRONT = ROOT / "apps/storefront/node_modules/.bin/nuxt"
CONFIG = LOCAL / "fixture-private.json"
RUNTIME = LOCAL / "runtime-private.json"
EVIDENCE = LOCAL / "issue11-cart-evidence.jsonl"
INVENTORY_STATE = LOCAL / "issue11-inventory-state-private.json"
API_PORT = 5081
SITE_PORT = 3001


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


def connection_targets_canonical_test(connection: str, certificate: str) -> bool:
    properties = connection_properties(connection)
    server = [value for key in ("datasource", "server", "address", "addr", "networkaddress") for value in properties.get(key, [])]
    database = properties.get("initialcatalog", properties.get("database", []))
    encrypt = properties.get("encrypt", [])
    trust = properties.get("trustservercertificate", [])
    hostname = properties.get("hostnameincertificate", [])
    cert = properties.get("servercertificate", [])
    return (
        len(server) == 1 and server[0].casefold() == "localhost,14333"
        and len(database) == 1 and database[0].casefold() == "kohami"
        and len(encrypt) == 1 and encrypt[0].casefold() == "strict"
        and len(trust) == 1 and trust[0].casefold() in {"false", "no", "0"}
        and len(hostname) == 1 and hostname[0].casefold() == "localhost"
        and len(cert) == 1 and Path(cert[0]).resolve() == Path(certificate).resolve()
    )


def valid_fixture_config(config: dict) -> bool:
    certificate = config.get("sqlCertificatePath")
    if not isinstance(certificate, str) or not certificate:
        return False
    return (
        config.get("IdentitySqlTestDedicated") is True
        and config.get("KohamiSqlTestDedicated") is True
        and config.get("port") == 14333
        and config.get("datasetId") == "kohami_local"
        and config.get("origin") == "https://localhost:3000"
        and config.get("sqlServerName") == "localhost"
        and connection_targets_canonical_test(config.get("connectionString", ""), certificate)
        and connection_targets_canonical_test(config.get("workerConnectionString", ""), certificate)
    )


def validate_fixture_preflight(output: str) -> None:
    if "PASS Issue11 SQL target is marker-verified canonical kohami-sql-test" not in output:
        raise RuntimeError("ISSUE11_FIXTURE_PREFLIGHT_INVALID")
    if "ISSUE11_MAINTENANCE_REQUIRED" in output:
        raise RuntimeError("MAINTENANCE_REQUIRED")
    if "ISSUE11_MAINTENANCE_DISABLED" not in output:
        raise RuntimeError("ISSUE11_FIXTURE_PREFLIGHT_INVALID")


def resolve_profile(local: Path) -> Path:
    local = local.expanduser().absolute()
    if local.name != "identity" or local.parent.name != "kohami-sql-test" or local.parent.parent.name != ".local":
        raise RuntimeError("ISSUE11_CANONICAL_PROFILE_PATH_REQUIRED")
    current = Path(local.anchor)
    for component in local.parts[1:]:
        current /= component
        if current.is_symlink():
            raise RuntimeError("ISSUE11_PROFILE_SYMLINK_REFUSED")
    if not local.is_dir() or local.stat().st_mode & 0o077:
        raise RuntimeError("ISSUE11_PRIVATE_PROFILE_REQUIRED")
    required = (CONFIG, RUNTIME, LOCAL / "browser/public.pem", LOCAL / "browser/private.key", LOCAL / "ca/public.pem")
    if any(path.is_symlink() or not path.is_file() for path in required):
        raise RuntimeError("ISSUE11_PROFILE_FILES_MISSING")
    for path in (CONFIG, RUNTIME, LOCAL / "browser/private.key"):
        if path.stat().st_mode & 0o077:
            raise RuntimeError("ISSUE11_PROFILE_FILE_PERMISSIONS_INVALID")
    config = json.loads(CONFIG.read_text())
    if not valid_fixture_config(config):
        raise RuntimeError("ISSUE11_CANONICAL_FIXTURE_CONFIG_REQUIRED")
    return local


def local_port(name: str, default: int, minimum: int, maximum: int, environ: dict[str, str]) -> int:
    value = environ.get(name)
    if value is None:
        return default
    try:
        port = int(value)
    except ValueError as error:
        raise RuntimeError("ISSUE11_LOCAL_PORT_INVALID") from error
    if not minimum <= port <= maximum:
        raise RuntimeError("ISSUE11_LOCAL_PORT_INVALID")
    return port


def require_free_port(port: int) -> None:
    with socket.socket() as listener:
        try:
            listener.bind(("127.0.0.1", port))
        except OSError as error:
            raise RuntimeError(f"ISSUE11_LOCAL_PORT_IN_USE:{port}") from error


def select_port(name: str, default: int, minimum: int, maximum: int, environ: dict[str, str]) -> int:
    requested = environ.get(name)
    if requested is not None:
        port = local_port(name, default, minimum, maximum, environ)
        require_free_port(port)
        return port
    candidates = [default, *(port for port in range(minimum, maximum + 1) if port != default)]
    for port in candidates:
        try:
            require_free_port(port)
            return port
        except RuntimeError:
            continue
    raise RuntimeError("ISSUE11_NO_FREE_LOOPBACK_PORT")


def create_private_file(path: Path) -> Path:
    if path.exists() or path.is_symlink():
        raise RuntimeError("ISSUE11_PRIVATE_PATH_EXISTS")
    descriptor = os.open(path, os.O_CREAT | os.O_EXCL | os.O_WRONLY, 0o600)
    os.close(descriptor)
    return path


def create_runtime_override(site_port: int) -> Path:
    override = LOCAL / f"issue11-runtime-{uuid.uuid4().hex}-private.json"
    create_private_file(override)
    try:
        runtime = json.loads(RUNTIME.read_text())
        runtime["origin"] = f"https://localhost:{site_port}"
        override.write_text(json.dumps(runtime, separators=(",", ":")))
        os.chmod(override, 0o600)
        return override
    except BaseException:
        override.unlink(missing_ok=True)
        raise


def wait_http(url: str, *, tls_context: ssl.SSLContext | None = None, process: subprocess.Popen | None = None) -> None:
    deadline = time.monotonic() + 60
    while time.monotonic() < deadline:
        if process is not None and process.poll() is not None:
            raise RuntimeError("ISSUE11_LOCAL_SERVICE_EXITED")
        try:
            with urllib.request.urlopen(url, timeout=2, context=tls_context) as response:
                if response.status == 200:
                    return
        except Exception:
            time.sleep(0.4)
    raise RuntimeError("ISSUE11_LOCAL_SERVICE_NOT_READY")


def stop_group(process: subprocess.Popen | None) -> None:
    if process is None or process.poll() is not None:
        return
    try:
        os.killpg(process.pid, signal.SIGTERM)
        process.wait(timeout=8)
    except (ProcessLookupError, subprocess.TimeoutExpired):
        try:
            os.killpg(process.pid, signal.SIGKILL)
        except ProcessLookupError:
            pass
        process.wait(timeout=3)


def run_helper(command: str, *arguments: str, timeout: int = 60) -> None:
    subprocess.run([str(DOTNET), str(COMMERCE_DLL), command, str(CONFIG), *arguments], cwd=ROOT, check=True, timeout=timeout)


def run_fixture_preflight() -> None:
    result = subprocess.run(
        [str(DOTNET), str(COMMERCE_DLL), "issue11-fixture-check", str(CONFIG)],
        cwd=ROOT,
        check=True,
        capture_output=True,
        text=True,
        timeout=60,
    )
    validate_fixture_preflight(result.stdout)
    print(result.stdout, end="")


def main() -> None:
    parser = __import__("argparse").ArgumentParser(description=__doc__)
    parser.add_argument("--api-port", default=None)
    parser.add_argument("--site-port", default=None)
    args = parser.parse_args()
    resolve_profile(LOCAL)
    api_port = select_port("KOHAMI_ISSUE11_API_PORT", API_PORT, API_PORT, API_PORT, {"KOHAMI_ISSUE11_API_PORT": args.api_port} if args.api_port else os.environ)
    site_port = select_port("KOHAMI_ISSUE11_SITE_PORT", SITE_PORT, SITE_PORT, SITE_PORT, {"KOHAMI_ISSUE11_SITE_PORT": args.site_port} if args.site_port else os.environ)
    for path in (DOTNET, API_DLL, COMMERCE_DLL, PLAYWRIGHT, STOREFRONT):
        if not path.is_file():
            raise RuntimeError("ISSUE11_REQUIRED_RUNTIME_MISSING")
    if EVIDENCE.exists() or EVIDENCE.is_symlink() or INVENTORY_STATE.exists() or INVENTORY_STATE.is_symlink():
        raise RuntimeError("ISSUE11_PRIOR_RUN_STATE_REQUIRES_REVIEW")

    run_fixture_preflight()
    runtime_override = create_runtime_override(site_port)
    log_file = tempfile.TemporaryFile(mode="w+b")
    api = site = None
    primary_error: BaseException | None = None
    try:
        env = os.environ.copy()
        for key in ("http_proxy", "https_proxy", "HTTP_PROXY", "HTTPS_PROXY"):
            env.pop(key, None)
        env["NO_PROXY"] = ",".join(filter(None, [env.get("NO_PROXY", ""), "localhost", "127.0.0.1", "::1"]))
        env["no_proxy"] = env["NO_PROXY"]
        env.update({
            "KOHAMI_CONFIG": str(runtime_override),
            "KOHAMI_API_PORT": str(api_port),
        })
        api = subprocess.Popen([str(DOTNET), str(API_DLL)], cwd=ROOT, env=env, stdout=log_file, stderr=subprocess.STDOUT, start_new_session=True)
        wait_http(f"http://127.0.0.1:{api_port}/api/v1/products?limit=1", process=api)

        site_env = env.copy()
        site_env.update({
            "Foundation_HTTPS_CERT": str(LOCAL / "browser/public.pem"),
            "Foundation_HTTPS_KEY": str(LOCAL / "browser/private.key"),
            "NODE_EXTRA_CA_CERTS": str(LOCAL / "ca/public.pem"),
            "NUXT_API_ORIGIN": f"http://127.0.0.1:{api_port}",
            "NUXT_SITE_ORIGIN": f"https://localhost:{site_port}",
            "NUXT_PORT": str(site_port),
            "KOHAMI_STOREFRONT_ORIGIN": f"https://localhost:{site_port}",
        })
        site = subprocess.Popen([str(STOREFRONT), "dev", "--host", "localhost", "--port", str(site_port)], cwd=ROOT / "apps/storefront", env=site_env, stdout=log_file, stderr=subprocess.STDOUT, start_new_session=True)
        ca_context = ssl.create_default_context(cafile=str(LOCAL / "ca/public.pem"))
        wait_http(f"https://localhost:{site_port}/zh-TW", tls_context=ca_context, process=site)

        evidence = create_private_file(EVIDENCE)
        test_env = site_env.copy()
        test_env.update({
            "KOHAMI_ISSUE11_CART_EVIDENCE_PATH": str(evidence),
            "KOHAMI_ISSUE11_DOTNET": str(DOTNET),
            "KOHAMI_ISSUE11_COMMERCE_DLL": str(COMMERCE_DLL),
            "KOHAMI_ISSUE11_CONFIG_PATH": str(CONFIG),
            "KOHAMI_ISSUE11_INVENTORY_STATE_PATH": str(INVENTORY_STATE),
            "KOHAMI_ISSUE11_PLAYWRIGHT_OUTPUT": str(LOCAL / f"issue11-playwright-{uuid.uuid4().hex}"),
        })
        subprocess.run([
            str(PLAYWRIGHT), "test", "--config", "playwright.issue11.config.ts",
            "--grep", "Issue11", "--reporter=line", "--trace=off",
        ], cwd=ROOT / "apps/storefront", env=test_env, check=True, timeout=360)
        print("PASS Issue11 canonical kohami-sql-test cart, quote and load-recovery acceptance")
    except BaseException as error:
        primary_error = error
        raise
    finally:
        stop_group(site)
        stop_group(api)
        try:
            if INVENTORY_STATE.exists():
                run_helper("issue11-inventory-restore", str(INVENTORY_STATE))
            if EVIDENCE.exists() and EVIDENCE.stat().st_size:
                run_helper("issue11-cart-cleanup", str(EVIDENCE), timeout=90)
            elif EVIDENCE.exists():
                EVIDENCE.unlink()
            runtime_override.unlink(missing_ok=True)
            output_dir = Path(test_env["KOHAMI_ISSUE11_PLAYWRIGHT_OUTPUT"]) if "test_env" in locals() else None
            if output_dir is not None and output_dir.is_dir():
                import shutil
                shutil.rmtree(output_dir)
        except BaseException:
            if primary_error is None:
                raise
            print("ISSUE11_CLEANUP_REQUIRES_REVIEW", file=sys.stderr)


if __name__ == "__main__":
    try:
        main()
    except subprocess.CalledProcessError as error:
        print(f"ISSUE11_ACCEPTANCE_FAILED:COMMAND_EXIT_{error.returncode}", file=sys.stderr)
        raise SystemExit(error.returncode or 1)
    except (OSError, RuntimeError, json.JSONDecodeError, urllib.error.URLError) as error:
        print("ISSUE11_ACCEPTANCE_BLOCKED:" + str(error).replace("\n", "_"), file=sys.stderr)
        raise SystemExit(1)
