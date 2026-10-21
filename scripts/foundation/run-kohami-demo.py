#!/usr/bin/env python3
"""Run API and HTTPS Storefront only against the marker-verified KOHAMI demo fixture."""
from contextlib import contextmanager
from pathlib import Path
import json
import os
import signal
import socket
import ssl
import subprocess
import sys
import tempfile
import time
import urllib.error
import urllib.request


ROOT = Path(__file__).resolve().parents[2]
DEFAULT_LOCAL = ROOT / ".local" / "kohami-demo" / "environment"
DEFAULT_API_PORT = 5087
DEFAULT_SITE_PORT = 3004
STOREFRONT_BUILD_DIR = ".nuxt"
NATIVE = ROOT / "tools/native-operations/bin/Release/net10.0/Native.dll"


def connection_properties(connection: str) -> dict[str, list[str]]:
    properties: dict[str, list[str]] = {}
    items: list[str] = []
    item: list[str] = []
    quote: str | None = None
    index = 0
    while index < len(connection):
        character = connection[index]
        if quote and character == quote:
            if index + 1 < len(connection) and connection[index + 1] == quote:
                item.extend((character, character))
                index += 2
                continue
            quote = None
        elif not quote and character in "\"'":
            quote = character
        elif not quote and character == ";":
            items.append("".join(item))
            item = []
            index += 1
            continue
        item.append(character)
        index += 1
    items.append("".join(item))
    for item in items:
        if not item.strip():
            continue
        key, separator, value = item.partition("=")
        if not separator:
            continue
        key = key.strip().casefold()
        value = value.strip()
        if len(value) >= 2 and value[0] in "\"'" and value[-1] == value[0]:
            quote = value[0]
            value = value[1:-1].replace(quote * 2, quote)
        properties.setdefault(key, []).append(value)
    return properties


def require_loopback_port(port: int, lower: int, upper: int, label: str) -> None:
    if not lower <= port <= upper:
        raise RuntimeError(f"{label}_PORT_INVALID")
    with socket.socket() as listener:
        listener.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        try:
            listener.bind(("127.0.0.1", port))
        except OSError as error:
            raise RuntimeError(f"{label}_PORT_UNAVAILABLE") from error


def validate_profile(local: Path) -> tuple[Path, dict]:
    local = local if local.is_absolute() else ROOT / local
    local = Path(os.path.abspath(local))
    allowed = ROOT / ".local"
    if not local.is_relative_to(allowed) or not allowed.is_dir() or allowed.is_symlink() or local.is_symlink() or not local.is_dir():
        raise RuntimeError("DEMO_PROFILE_OUTSIDE_LOCAL_ROOT")
    current = allowed
    for component in local.relative_to(allowed).parts:
        current /= component
        if current.is_symlink():
            raise RuntimeError("DEMO_PROFILE_SYMLINK_REFUSED")
    files = {name: local / name for name in (
        "fixture-private.json", "runtime-private.json", "browser/public.pem", "browser/private.key", "ca/public.pem",
    )}
    if any(path.is_symlink() or not path.is_file() for path in files.values()):
        raise RuntimeError("DEMO_PROFILE_REQUIRED_FILE_MISSING")
    config = json.loads(files["fixture-private.json"].read_text())
    if config.get("KohamiDemoDedicated") is not True or config.get("port") in (14349, 14350):
        raise RuntimeError("DEMO_PROFILE_MARKER_REQUIRED")
    runtime = json.loads(files["runtime-private.json"].read_text())
    port = config.get("port")
    if not isinstance(port, int) or not 14336 <= port <= 14399 or port in (14349, 14350):
        raise RuntimeError("DEMO_PROFILE_SQL_TARGET_INVALID")
    properties = connection_properties(runtime.get("connectionString", ""))
    data_sources = [value for key in ("data source", "server", "address", "addr", "network address") for value in properties.get(key, [])]
    catalogs = [value for key in ("initial catalog", "database", "db") for value in properties.get(key, [])]
    encrypt = properties.get("encrypt", [])
    trust_server_certificate = properties.get("trustservercertificate", [])
    if (
        runtime.get("datasetId") != "kohami_local"
        or len(data_sources) != 1
        or data_sources[0].casefold() != f"localhost,{port}"
        or len(catalogs) != 1
        or catalogs[0].casefold() != "kohami"
        or len(encrypt) != 1
        or encrypt[0].casefold() != "strict"
        or len(trust_server_certificate) != 1
        or trust_server_certificate[0].casefold() != "false"
    ):
        raise RuntimeError("DEMO_PROFILE_SQL_TARGET_INVALID")
    if any(path.stat().st_mode & 0o077 for path in files.values() if path.suffix == ".json" or path.name == "private.key"):
        raise RuntimeError("DEMO_PROFILE_PRIVATE_MODE_INVALID")
    return local, runtime


def verify_live_fixture(local: Path, port: int, env: dict[str, str]) -> None:
    if port in (14349, 14350) or not 14336 <= port <= 14399:
        raise RuntimeError("DEMO_PROFILE_SQL_TARGET_INVALID")
    if not NATIVE.is_file():
        raise RuntimeError("BUILD_NATIVE_OPERATIONS_RELEASE_FIRST")
    try:
        subprocess.run(
            ["/usr/local/share/dotnet/dotnet", str(NATIVE), "verify-local-demo-catalog", str(ROOT), str(port)],
            cwd=ROOT,
            env={**env, "KOHAMI_LOCAL_DIR": str(local), "KOHAMI_FIXTURE_PURPOSE": "local-demo"},
            check=True,
            timeout=60,
        )
    except (subprocess.CalledProcessError, subprocess.TimeoutExpired) as error:
        raise RuntimeError("DEMO_FIXTURE_MARKER_VERIFICATION_FAILED") from error


@contextmanager
def preview_config(source: Path, origin: str):
    payload = json.loads(source.read_text())
    payload["origin"] = origin
    fd, name = tempfile.mkstemp(prefix="runtime-demo-", suffix=".json", dir=source.parent)
    path = Path(name)
    try:
        os.fchmod(fd, 0o600)
        with os.fdopen(fd, "w") as stream:
            json.dump(payload, stream, separators=(",", ":"))
            stream.write("\n")
        yield path
    finally:
        path.unlink(missing_ok=True)


def request_json(url: str, timeout: float = 2) -> tuple[int, bytes]:
    request = urllib.request.Request(url, headers={"Accept": "application/json"})
    try:
        with urllib.request.urlopen(request, timeout=timeout) as response:
            return response.status, response.read()
    except urllib.error.HTTPError as error:
        return error.code, error.read()


def wait_database_ready(api_port: int, seconds: int = 45, children: list[subprocess.Popen] | None = None) -> None:
    deadline = time.monotonic() + seconds
    while time.monotonic() < deadline:
        if children and any(child.poll() is not None for child in children):
            raise RuntimeError("DEMO_API_EXITED_BEFORE_READY")
        try:
            health, health_body = request_json(f"http://127.0.0.1:{api_port}/health")
            ready, ready_body = request_json(f"http://127.0.0.1:{api_port}/ready")
            if health == 200 and json.loads(health_body).get("status") == "database-connected":
                if ready == 200 and json.loads(ready_body).get("status") == "ready":
                    return
                if ready == 503:
                    raise RuntimeError("DEMO_API_SCHEMA_NOT_READY")
        except (OSError, TimeoutError, json.JSONDecodeError):
            pass
        time.sleep(0.5)
    raise RuntimeError("DEMO_API_SQL_READINESS_TIMEOUT")


def wait_storefront_ready(site_port: int, ca_file: Path, seconds: int = 90, children: list[subprocess.Popen] | None = None) -> None:
    context = ssl.create_default_context(cafile=str(ca_file))
    deadline = time.monotonic() + seconds
    while time.monotonic() < deadline:
        if children and any(child.poll() is not None for child in children):
            raise RuntimeError("DEMO_CHILD_EXITED_BEFORE_READY")
        try:
            with urllib.request.urlopen(
                f"https://localhost:{site_port}/zh-TW", context=context, timeout=3
            ) as response:
                if response.status == 200:
                    return
        except (OSError, TimeoutError, urllib.error.URLError):
            pass
        time.sleep(0.5)
    raise RuntimeError("DEMO_STOREFRONT_HTTPS_READINESS_TIMEOUT")


def wait_for_children(children: list[subprocess.Popen], poll_interval: float = 0.25) -> int:
    while True:
        for child in children:
            result = child.poll()
            if result is not None:
                return result
        time.sleep(poll_interval)


def stop_children(children: list[subprocess.Popen]) -> None:
    for child in reversed(children):
        if child.poll() is None:
            child.terminate()
    for child in reversed(children):
        try:
            child.wait(timeout=8)
        except subprocess.TimeoutExpired:
            child.kill()
            child.wait(timeout=5)


def serve(local: Path, api_port: int, site_port: int) -> int:
    local, _runtime = validate_profile(local)
    require_loopback_port(api_port, 5080, 5099, "API")
    require_loopback_port(site_port, 3000, 3999, "SITE")
    if site_port in (3000, 3001):
        raise RuntimeError("SITE_PORT_RESERVED")
    api = ROOT / "apps/api/bin/Release/net10.0/Api.dll"
    if not api.is_file() or not NATIVE.is_file():
        raise RuntimeError("BUILD_API_RELEASE_FIRST")
    port = json.loads((local / "fixture-private.json").read_text())["port"]
    env = os.environ.copy()
    env.update({
        "KOHAMI_LOCAL_DIR": str(local),
        "KOHAMI_CONTRACT": str(ROOT / "docs/contracts/runtime/Core.openapi.json"),
        "Foundation_HTTPS_CERT": str(local / "browser/public.pem"),
        "Foundation_HTTPS_KEY": str(local / "browser/private.key"),
        "KOHAMI_API_PORT": str(api_port),
        "NUXT_API_ORIGIN": f"http://127.0.0.1:{api_port}",
        "NUXT_PORT": str(site_port),
        "NUXT_SITE_ORIGIN": f"https://localhost:{site_port}",
        "NUXT_BUILD_DIR": STOREFRONT_BUILD_DIR,
        "NODE_EXTRA_CA_CERTS": str(local / "ca/public.pem"),
    })
    verify_live_fixture(local, port, env)
    children: list[subprocess.Popen] = []
    old_sigterm = signal.signal(signal.SIGTERM, lambda *_: (_ for _ in ()).throw(KeyboardInterrupt()))
    try:
        with preview_config(local / "runtime-private.json", f"https://localhost:{site_port}") as config:
            env["KOHAMI_CONFIG"] = str(config)
            children.append(subprocess.Popen(["/usr/local/share/dotnet/dotnet", str(api)], cwd=ROOT, env=env))
            wait_database_ready(api_port, children=children)
            children.append(subprocess.Popen(["npm", "run", "dev", "--", "--port", str(site_port)], cwd=ROOT / "apps/storefront", env=env))
            wait_storefront_ready(site_port, local / "ca/public.pem", children=children)
            print(f"DEMO_P1_READY storefront=https://localhost:{site_port} api=http://127.0.0.1:{api_port} sql=marker-verified", flush=True)
            print("Use the browser trust flow in LOCAL_DEMO_RUNBOOK; this runner does not bypass TLS validation.", flush=True)
            return wait_for_children(children)
    except KeyboardInterrupt:
        return 0
    finally:
        signal.signal(signal.SIGTERM, old_sigterm)
        stop_children(children)


def main() -> int:
    import argparse
    parser = argparse.ArgumentParser(description="Run local Storefront/API against the isolated demo fixture.")
    parser.add_argument("--local-dir", type=Path, default=DEFAULT_LOCAL)
    parser.add_argument("--api-port", type=int, default=DEFAULT_API_PORT)
    parser.add_argument("--site-port", type=int, default=DEFAULT_SITE_PORT)
    args = parser.parse_args()
    try:
        return serve(args.local_dir, args.api_port, args.site_port)
    except Exception as error:
        print("DEMO_P1_START_BLOCKED:" + str(error).replace(" ", "_"), file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
