#!/usr/bin/env python3
"""Exercise password-only admin catalog access against the verified local demo fixture."""
from __future__ import annotations

import http.cookies
import json
import os
from pathlib import Path
import socket
import subprocess
import time
import urllib.error
import urllib.request

ROOT = Path(__file__).resolve().parents[2]
DEFAULT_LOCAL = ROOT / ".local" / "kohami-demo" / "environment"
DOTNET = Path("/usr/local/share/dotnet/dotnet")
NATIVE = ROOT / "tools" / "native-operations" / "bin" / "Release" / "net10.0" / "Native.dll"
API = ROOT / "apps" / "api" / "bin" / "Release" / "net10.0" / "Api.dll"
CONTRACT = ROOT / "docs" / "contracts" / "runtime" / "Core.openapi.json"


def free_api_port() -> int:
    for port in range(5080, 5100):
        with socket.socket() as listener:
            listener.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
            try:
                listener.bind(("127.0.0.1", port))
            except OSError:
                continue
        return port
    raise RuntimeError("ADMIN_HTTP_API_PORT_UNAVAILABLE")


class Client:
    def __init__(self, base: str, origin: str):
        self.base = base
        self.origin = origin
        self.cookies: dict[str, str] = {}

    def request(self, method: str, path: str, body=None, *, csrf: str | None = None):
        headers = {"Accept": "application/json", "X-Forwarded-Proto": "https"}
        if self.cookies:
            headers["Cookie"] = "; ".join(f"{key}={value}" for key, value in self.cookies.items())
        if method not in {"GET", "HEAD"}:
            headers["Origin"] = self.origin
            headers["X-CSRF-TOKEN"] = csrf or ""
        data = None if body is None else json.dumps(body, separators=(",", ":")).encode()
        if data is not None:
            headers["Content-Type"] = "application/json"
        request = urllib.request.Request(self.base + path, data=data, headers=headers, method=method)
        try:
            response = urllib.request.urlopen(request, timeout=5)
            status, raw, response_headers = response.status, response.read(), response.headers
        except urllib.error.HTTPError as error:
            status, raw, response_headers = error.code, error.read(), error.headers
        for value in response_headers.get_all("Set-Cookie") or []:
            parsed = http.cookies.SimpleCookie()
            parsed.load(value)
            for name, morsel in parsed.items():
                self.cookies[name] = morsel.value
        try:
            payload = json.loads(raw) if raw else {}
        except (UnicodeError, json.JSONDecodeError):
            payload = {}
        return status, payload


def run(local: Path = DEFAULT_LOCAL) -> None:
    local = local if local.is_absolute() else ROOT / local
    local = Path(os.path.abspath(local))
    allowed = ROOT / ".local"
    if not local.is_relative_to(allowed) or allowed.is_symlink() or not allowed.is_dir():
        raise RuntimeError("ADMIN_HTTP_PRIVATE_PROFILE_INVALID")
    current = allowed
    for component in local.relative_to(allowed).parts:
        current /= component
        if current.is_symlink():
            raise RuntimeError("ADMIN_HTTP_PRIVATE_PROFILE_INVALID")
    if not DOTNET.is_file() or not NATIVE.is_file() or not API.is_file() or not CONTRACT.is_file():
        raise RuntimeError("ADMIN_HTTP_RELEASE_ARTIFACTS_REQUIRED")
    for path in (local / "fixture-private.json", local / "runtime-private.json", local / "local-demo-admin-credentials.json"):
        if path.is_symlink() or not path.is_file() or path.stat().st_mode & 0o077:
            raise RuntimeError("ADMIN_HTTP_PRIVATE_PROFILE_INVALID")
    profile = json.loads((local / "fixture-private.json").read_text())
    runtime = json.loads((local / "runtime-private.json").read_text())
    credentials = json.loads((local / "local-demo-admin-credentials.json").read_text())
    port = profile.get("port")
    if profile.get("KohamiDemoDedicated") is not True or not isinstance(port, int) or not 14336 <= port <= 14399 or port in {14349, 14350}:
        raise RuntimeError("ADMIN_HTTP_FIXTURE_SCOPE_INVALID")
    if runtime.get("datasetId") != "demo_m0" or runtime.get("sqlServerName") != "localhost":
        raise RuntimeError("ADMIN_HTTP_RUNTIME_SCOPE_INVALID")
    if credentials.get("mode") != "local-demo" or credentials.get("datasetId") != "demo_m0":
        raise RuntimeError("ADMIN_HTTP_CREDENTIAL_SCOPE_INVALID")

    env = os.environ.copy()
    env["KOHAMI_M0_LOCAL_DIR"] = str(local)
    env["KOHAMI_M0_FIXTURE_PURPOSE"] = "local-demo"
    subprocess.run([str(DOTNET), str(NATIVE), "verify-local-demo-catalog", str(ROOT), str(port)], cwd=ROOT, env=env, check=True, timeout=60, stdout=subprocess.DEVNULL)

    api_port = free_api_port()
    env["M0_CONFIG"] = str(local / "runtime-private.json")
    env["M0_CONTRACT"] = str(CONTRACT)
    env["KOHAMI_API_PORT"] = str(api_port)
    env["DOTNET_ENVIRONMENT"] = "Production"
    process = subprocess.Popen([str(DOTNET), str(API)], cwd=ROOT, env=env, stdin=subprocess.DEVNULL, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    try:
        client = Client(f"http://localhost:{api_port}", runtime["origin"])
        deadline = time.monotonic() + 30
        while time.monotonic() < deadline:
            if process.poll() is not None:
                raise RuntimeError("ADMIN_HTTP_API_EXITED")
            try:
                health, _ = client.request("GET", "/health")
                ready, _ = client.request("GET", "/ready")
                if health == 200 and ready == 200:
                    break
            except (OSError, TimeoutError):
                time.sleep(0.25)
        else:
            raise RuntimeError("ADMIN_HTTP_API_NOT_READY")

        status, _ = client.request("GET", "/api/v1/admin/products")
        if status != 401:
            raise RuntimeError("ADMIN_HTTP_ANONYMOUS_READ_NOT_DENIED")
        status, csrf = client.request("GET", "/api/v1/auth/csrf")
        token = csrf.get("requestToken")
        if status != 200 or not token or "kohami.csrf" not in client.cookies:
            raise RuntimeError("ADMIN_HTTP_CSRF_BOOTSTRAP_FAILED")
        status, session = client.request("POST", "/api/v1/auth/sessions", {"email": credentials["email"], "password": credentials["password"]}, csrf=token)
        if status != 200 or session.get("state") != "AdminAuthenticated" or "kohami.session" not in client.cookies:
            raise RuntimeError("ADMIN_HTTP_PASSWORD_LOGIN_FAILED")
        status, listing = client.request("GET", "/api/v1/admin/products")
        items = listing.get("items", [])
        if status != 200 or len(items) != 15 or any("draftJson" in item for item in items):
            raise RuntimeError("ADMIN_HTTP_PRODUCT_LIST_FAILED")
        product_id = items[0].get("id")
        status, detail = client.request("GET", "/api/v1/admin/products/" + product_id)
        if status != 200 or detail.get("id") != product_id or "draftJson" in detail:
            raise RuntimeError("ADMIN_HTTP_PRODUCT_DETAIL_FAILED")
        status, _ = client.request("POST", "/api/v1/admin/products", {}, csrf=token)
        if status != 403:
            raise RuntimeError("ADMIN_HTTP_WRITE_NOT_DENIED")
        status, _ = client.request("DELETE", "/api/v1/auth/session", csrf=token)
        if status != 204:
            raise RuntimeError("ADMIN_HTTP_LOGOUT_FAILED")
        status, _ = client.request("GET", "/api/v1/admin/products")
        if status != 401:
            raise RuntimeError("ADMIN_HTTP_POST_LOGOUT_READ_NOT_DENIED")
        print("PASS local-demo admin HTTP: login/session cookie, 15-item list, published-only detail, write denial, logout and revoked-session denial")
    finally:
        process.terminate()
        try:
            process.wait(timeout=8)
        except subprocess.TimeoutExpired:
            process.kill()
            process.wait(timeout=3)


if __name__ == "__main__":
    import argparse
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--local-dir", type=Path, default=DEFAULT_LOCAL, help="existing marker-verified local-demo profile under this checkout's .local/")
    arguments = parser.parse_args()
    try:
        run(arguments.local_dir)
    except Exception as error:
        print("FAIL local-demo admin HTTP: " + (str(error) if str(error).startswith("ADMIN_HTTP_") else type(error).__name__))
        raise SystemExit(1)
