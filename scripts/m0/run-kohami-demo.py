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
    if runtime.get("datasetId") != "demo_m0" or "localhost," + str(config.get("port")) not in runtime.get("connectionString", ""):
        raise RuntimeError("DEMO_PROFILE_SQL_TARGET_INVALID")
    if "Encrypt=Strict" not in runtime.get("connectionString", "") or "TrustServerCertificate=False" not in runtime.get("connectionString", ""):
        raise RuntimeError("DEMO_PROFILE_TLS_REQUIRED")
    if any(path.stat().st_mode & 0o077 for path in files.values() if path.suffix == ".json" or path.name == "private.key"):
        raise RuntimeError("DEMO_PROFILE_PRIVATE_MODE_INVALID")
    return local, runtime


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


def wait_database_ready(api_port: int, seconds: int = 45) -> None:
    deadline = time.monotonic() + seconds
    while time.monotonic() < deadline:
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


def wait_storefront_ready(site_port: int, ca_file: Path, seconds: int = 90) -> None:
    context = ssl.create_default_context(cafile=str(ca_file))
    deadline = time.monotonic() + seconds
    while time.monotonic() < deadline:
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


def serve(local: Path, api_port: int, site_port: int) -> int:
    local, _runtime = validate_profile(local)
    require_loopback_port(api_port, 5080, 5099, "API")
    require_loopback_port(site_port, 3000, 3999, "SITE")
    if site_port in (3000, 3001):
        raise RuntimeError("SITE_PORT_RESERVED")
    api = ROOT / "apps/api/bin/Release/net10.0/Api.dll"
    if not api.is_file():
        raise RuntimeError("BUILD_API_RELEASE_FIRST")
    env = os.environ.copy()
    env.update({
        "KOHAMI_M0_LOCAL_DIR": str(local),
        "M0_CONTRACT": str(ROOT / "docs/contracts/runtime/Core.openapi.json"),
        "M0_HTTPS_CERT": str(local / "browser/public.pem"),
        "M0_HTTPS_KEY": str(local / "browser/private.key"),
        "KOHAMI_API_PORT": str(api_port),
        "NUXT_API_ORIGIN": f"http://127.0.0.1:{api_port}",
        "NUXT_PORT": str(site_port),
        "NUXT_SITE_ORIGIN": f"https://localhost:{site_port}",
        "NUXT_BUILD_DIR": STOREFRONT_BUILD_DIR,
        "NODE_EXTRA_CA_CERTS": str(local / "ca/public.pem"),
    })
    children: list[subprocess.Popen] = []
    old_sigterm = signal.signal(signal.SIGTERM, lambda *_: (_ for _ in ()).throw(KeyboardInterrupt()))
    try:
        with preview_config(local / "runtime-private.json", f"https://localhost:{site_port}") as config:
            env["M0_CONFIG"] = str(config)
            children.append(subprocess.Popen(["/usr/local/share/dotnet/dotnet", str(api)], cwd=ROOT, env=env))
            wait_database_ready(api_port)
            children.append(subprocess.Popen(["npm", "run", "dev", "--", "--port", str(site_port)], cwd=ROOT / "apps/storefront", env=env))
            wait_storefront_ready(site_port, local / "ca/public.pem")
            print(f"DEMO_P1_READY storefront=https://localhost:{site_port} api=http://127.0.0.1:{api_port} sql=marker-verified", flush=True)
            print("Use the browser trust flow in LOCAL_DEMO_RUNBOOK; this runner does not bypass TLS validation.", flush=True)
            for child in children:
                child.wait()
            return next((child.returncode for child in children if child.returncode), 0)
    except KeyboardInterrupt:
        return 0
    finally:
        signal.signal(signal.SIGTERM, old_sigterm)
        for child in reversed(children):
            if child.poll() is None:
                child.terminate()
        for child in reversed(children):
            try:
                child.wait(timeout=8)
            except subprocess.TimeoutExpired:
                child.kill()
                child.wait(timeout=5)


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
