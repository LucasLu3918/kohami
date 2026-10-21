#!/usr/bin/env python3
"""Run Issue25 S4 local authorization/CSRF checks against a prebuilt API."""
from __future__ import annotations

import argparse
from dataclasses import dataclass
import http.cookies
import json
import os
from pathlib import Path
import re
import signal
import socket
import subprocess
import sys
import time
import urllib.error
import urllib.request

ROOT = Path(__file__).resolve().parents[2]
DOTNET = Path("/usr/local/share/dotnet/dotnet")
DEFAULT_API_DLL = ROOT / "apps/api/bin/Release/net10.0/Api.dll"
DEFAULT_RUNTIME_CONFIG = ROOT / ".local/kohami-sql-test/identity/fixture-private.json"
DEFAULT_CONTRACT = ROOT / "docs/contracts/runtime/Core.openapi.json"
NATIVE_DLL_RELATIVE = Path("tools/native-operations/bin/Release/net10.0/Native.dll")
API_PORTS = range(5080, 5100)
HIDDEN_ORDER_ID = "order_issue25_hidden_resource"


@dataclass(frozen=True)
class RequestCase:
    name: str
    method: str
    path: str
    expected_status: int
    expected_code: str | None = None
    headers: dict[str, str] | None = None
    body: dict | None = None
    handler_must_not_run: bool = False


def parse_connection_string(value: str) -> dict[str, str]:
    """Read only allowlisted connection attributes; never retain credential fields."""
    fields: dict[str, str] = {}
    parts: list[str] = []
    current: list[str] = []
    braced = False
    index = 0
    while index < len(value):
        char = value[index]
        if char == "{":
            braced = True
            current.append(char)
        elif char == "}" and braced:
            if index + 1 < len(value) and value[index + 1] == "}":
                current.extend(("}", "}"))
                index += 1
            else:
                braced = False
                current.append(char)
        elif char == ";" and not braced:
            parts.append("".join(current))
            current.clear()
        else:
            current.append(char)
        index += 1
    if braced:
        return {}
    parts.append("".join(current))
    aliases = {
        "server": "server", "datasource": "server", "address": "server", "addr": "server",
        "networkaddress": "server", "database": "database", "initialcatalog": "database",
        "encrypt": "encrypt", "trustservercertificate": "trust",
        "hostnameincertificate": "hostcert", "servercertificate": "servercertificate",
    }
    for part in parts:
        key, separator, item = part.partition("=")
        canonical = "".join(key.casefold().split())
        if not separator or canonical not in aliases:
            continue
        item = item.strip()
        if len(item) >= 2 and item[0] == "{" and item[-1] == "}":
            item = item[1:-1].replace("}}", "}")
        fields[aliases[canonical]] = item.strip()
    return fields


def inspect_runtime_config(path: Path) -> dict:
    if not path.is_absolute() or path.is_symlink() or not path.is_file():
        raise RuntimeError("BLOCKED: RUNTIME_CONFIG_UNAVAILABLE")
    if (path.resolve(strict=False) != path
            or path.parts[-4:] != (".local", "kohami-sql-test", "identity", "fixture-private.json")
            or not (path.parents[3] / ".git").exists()):
        raise RuntimeError("BLOCKED: CONFIG_PATH_INVALID")
    if path.stat().st_mode & 0o077:
        raise RuntimeError("BLOCKED: RUNTIME_CONFIG_PERMISSIONS_UNSAFE")
    try:
        config = json.loads(path.read_text())
    except (OSError, UnicodeError, json.JSONDecodeError):
        raise RuntimeError("BLOCKED: RUNTIME_CONFIG_INVALID") from None
    if (config.get("datasetId") != "kohami_local" or config.get("sqlServerName") != "localhost"
            or config.get("KohamiSqlTestDedicated") is not True or config.get("port") != 14333
            or not re.fullmatch(r"https://localhost:3[0-9]{3}", config.get("origin", ""))
            or config.get("identityNamespace") != "kohami.identity_access.v1"):
        raise RuntimeError("BLOCKED: CONFIG_SCOPE_INVALID")
    connection = parse_connection_string(config.get("connectionString", ""))
    server = connection.get("server", "")
    match = re.fullmatch(r"localhost,(\d+)", server, re.IGNORECASE)
    if (not match or int(match.group(1)) != 14333
            or connection.get("database", "").casefold() != "kohami"
            or connection.get("encrypt", "").casefold() != "strict"
            or connection.get("trust", "").casefold() != "false"
            or connection.get("hostcert", "localhost").casefold() != "localhost"):
        raise RuntimeError("BLOCKED: CONFIG_SCOPE_INVALID")
    for key, kind in (("sqlCertificatePath", "file"), ("sqlCaPath", "file"),
                      ("sessionCertificatePath", "file"), ("sessionRingPath", "dir")):
        raw = config.get(key, "")
        candidate = Path(raw).expanduser() if raw else Path("/")
        if (not raw or not candidate.is_absolute() or candidate.is_symlink()
                or (kind == "file" and not candidate.is_file())
                or (kind == "dir" and not candidate.is_dir())):
            raise RuntimeError("BLOCKED: RUNTIME_CRYPTO_MATERIAL_UNAVAILABLE")
    return config


def verify_fixture_marker(config_path: Path) -> None:
    repo_root = config_path.parents[3]
    native_dll = repo_root / NATIVE_DLL_RELATIVE
    if not DOTNET.is_file() or native_dll.is_symlink() or not native_dll.is_file():
        raise RuntimeError("BLOCKED: DEDICATED_MARKER_UNVERIFIED")
    env = os.environ.copy()
    env["KOHAMI_LOCAL_DIR"] = str(config_path.parent)
    env["KOHAMI_FIXTURE_PURPOSE"] = "kohami-sql-test"
    try:
        subprocess.run(
            [str(DOTNET), str(native_dll), "fixture-status", str(repo_root), "14333",
             "--local-dir", str(config_path.parent)],
            cwd=repo_root, env=env, stdin=subprocess.DEVNULL,
            stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
            check=True, timeout=30,
        )
    except (OSError, subprocess.SubprocessError):
        raise RuntimeError("BLOCKED: DEDICATED_MARKER_UNVERIFIED") from None


def write_config(path: Path, config: dict) -> None:
    """Test helper that writes synthetic config data to an isolated temporary file."""
    path.write_text(json.dumps(config))
    if os.name != "nt":
        path.chmod(0o600)


def port_is_free(port: int) -> bool:
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as listener:
        listener.settimeout(0.15)
        return listener.connect_ex(("127.0.0.1", port)) != 0


def choose_api_port() -> int:
    for port in API_PORTS:
        if port_is_free(port):
            return port
    raise RuntimeError("BLOCKED: API_PORT_UNAVAILABLE")


def acceptance_plan(origin: str) -> list[RequestCase]:
    return [
        RequestCase("API health", "GET", "/health", 200),
        RequestCase("API readiness", "GET", "/ready", 200),
        RequestCase("anonymous wallet read denied", "GET", "/api/v1/wallet/current", 401, "AUTHENTICATION_REQUIRED"),
        RequestCase("anonymous orders list denied", "GET", "/api/v1/orders", 401, "AUTHENTICATION_REQUIRED"),
        RequestCase("anonymous unknown order hidden", "GET", f"/api/v1/orders/{HIDDEN_ORDER_ID}", 404, "RESOURCE_NOT_FOUND"),
        RequestCase("anonymous unknown order receipts hidden", "GET", f"/api/v1/orders/{HIDDEN_ORDER_ID}/receipts", 404, "RESOURCE_NOT_FOUND"),
        RequestCase("API CSRF token and cookie issuance", "GET", "/api/v1/auth/csrf", 200),
    ]


class ApiClient:
    def __init__(self, base_url: str, origin: str):
        self.base_url = base_url.rstrip("/")
        self.origin = origin
        self.cookies: dict[str, str] = {}
        self.csrf_token: str | None = None

    def _merge_cookies(self, headers) -> None:
        for raw in headers.get_all("Set-Cookie") or []:
            parsed = http.cookies.SimpleCookie()
            parsed.load(raw)
            for name, morsel in parsed.items():
                self.cookies[name] = morsel.value

    def _cookie_header(self) -> str:
        return "; ".join(f"{name}={value}" for name, value in self.cookies.items())

    def request(self, method: str, path: str, *, headers: dict[str, str] | None = None,
                body: dict | None = None) -> tuple[int, dict]:
        request_headers = {"X-Forwarded-Proto": "https"}
        if self.cookies:
            request_headers["Cookie"] = self._cookie_header()
        if headers:
            request_headers.update(headers)
        data = None if body is None else json.dumps(body, separators=(",", ":")).encode()
        if data is not None:
            request_headers["Content-Type"] = "application/json"
        request = urllib.request.Request(self.base_url + path, data=data, headers=request_headers, method=method)
        try:
            response = urllib.request.urlopen(request, timeout=10)
        except urllib.error.HTTPError as error:
            self._merge_cookies(error.headers)
            try:
                payload = json.loads(error.read())
            except (OSError, UnicodeError, json.JSONDecodeError):
                payload = {}
            finally:
                error.close()
            return error.code, payload
        with response:
            self._merge_cookies(response.headers)
            try:
                raw = response.read()
                payload = json.loads(raw) if raw else {}
            except (UnicodeError, json.JSONDecodeError):
                payload = {}
            return response.status, payload

    def bootstrap_csrf(self) -> None:
        status, payload = self.request("GET", "/api/v1/auth/csrf")
        self.csrf_token = payload.get("requestToken") if isinstance(payload, dict) else None
        if status != 200 or not self.csrf_token or "kohami.csrf" not in self.cookies:
            raise RuntimeError("BLOCKED: CSRF_BOOTSTRAP_INCOMPLETE")
        if "kohami.session" in self.cookies:
            raise RuntimeError("BLOCKED: CSRF_BOOTSTRAP_CREATED_SESSION")

    def security_requests(self) -> list[RequestCase]:
        if not self.csrf_token or "kohami.csrf" not in self.cookies or "kohami.session" in self.cookies:
            raise RuntimeError("BLOCKED: CSRF_BOOTSTRAP_INCOMPLETE")
        target = f"/api/v1/orders/{HIDDEN_ORDER_ID}/returns"
        common = {"Content-Type": "application/json"}
        return [
            RequestCase("POST missing Origin rejected", "POST", target, 403, "CSRF_INVALID",
                        {**common}, {}, True),
            RequestCase("POST wrong Origin rejected", "POST", target, 403, "CSRF_INVALID",
                        {**common, "Origin": "https://invalid.example"}, {}, True),
            RequestCase("POST missing antiforgery token rejected", "POST", target, 403, "CSRF_INVALID",
                        {**common, "Origin": self.origin}, {}, True),
            RequestCase("POST wrong antiforgery token rejected", "POST", target, 403, "CSRF_INVALID",
                        {**common, "Origin": self.origin, "X-CSRF-TOKEN": "invalid-issue25-token"}, {}, True),
        ]


def _readiness_request(client: ApiClient, case: RequestCase) -> tuple[int, dict]:
    return client.request(case.method, case.path)


def check_case(client: ApiClient, case: RequestCase) -> tuple[bool, str]:
    headers = dict(case.headers or {})
    if case.method == "POST" and case.name == "POST wrong antiforgery token rejected":
        headers["X-CSRF-TOKEN"] = "invalid-issue25-token"
    elif case.method == "POST" and case.name == "POST missing antiforgery token rejected":
        headers.pop("X-CSRF-TOKEN", None)
    status, payload = client.request(case.method, case.path, headers=headers, body=case.body)
    code = payload.get("code") if isinstance(payload, dict) else None
    passed = status == case.expected_status and (case.expected_code is None or code == case.expected_code)
    if passed:
        return True, f"PASS {case.name}: HTTP {status}" + ("; middleware rejected before handler" if case.handler_must_not_run else "")
    return False, f"FAIL {case.name}: expected HTTP {case.expected_status} {case.expected_code or ''}; got HTTP {status} {code or 'NO_CODE'}"


def wait_for_listener(port: int, process: subprocess.Popen, timeout: float = 30) -> None:
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        if process.poll() is not None:
            raise RuntimeError("BLOCKED: API_PROCESS_EXITED_DURING_STARTUP")
        with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as client:
            client.settimeout(0.2)
            if client.connect_ex(("127.0.0.1", port)) == 0:
                return
        time.sleep(0.2)
    raise RuntimeError("BLOCKED: API_LISTENER_NOT_READY")


def stop_process(process: subprocess.Popen | None) -> None:
    if process is None or process.poll() is not None:
        return
    process.send_signal(signal.SIGTERM)
    try:
        process.wait(timeout=8)
    except subprocess.TimeoutExpired:
        process.kill()
        process.wait(timeout=3)


def run_acceptance(api_dll: Path, config_path: Path, contract_path: Path) -> int:
    if not DOTNET.is_file() or not api_dll.is_absolute() or api_dll.is_symlink() or not api_dll.is_file():
        raise RuntimeError("BLOCKED: PREBUILT_API_UNAVAILABLE")
    if not contract_path.is_absolute() or contract_path.is_symlink() or not contract_path.is_file():
        raise RuntimeError("BLOCKED: CONTRACT_UNAVAILABLE")
    config = inspect_runtime_config(config_path)
    verify_fixture_marker(config_path)
    port = choose_api_port()
    base_url = f"http://localhost:{port}"
    env = os.environ.copy()
    env["KOHAMI_CONFIG"] = str(config_path)
    env["KOHAMI_CONTRACT"] = str(contract_path)
    env["KOHAMI_API_PORT"] = str(port)
    env["DOTNET_ENVIRONMENT"] = "Production"
    process: subprocess.Popen | None = None
    failures = 0
    try:
        process = subprocess.Popen([str(DOTNET), str(api_dll)], cwd=ROOT, env=env,
                                   stdin=subprocess.DEVNULL, stdout=subprocess.DEVNULL,
                                   stderr=subprocess.DEVNULL, start_new_session=True)
        wait_for_listener(port, process)
        anonymous = ApiClient(base_url, config["origin"])
        csrf_client = ApiClient(base_url, config["origin"])
        for case in acceptance_plan(config["origin"]):
            if case.name == "API CSRF token and cookie issuance":
                status, payload = csrf_client.request(case.method, case.path)
                csrf_client.csrf_token = payload.get("requestToken") if isinstance(payload, dict) else None
                passed = status == 200 and bool(csrf_client.csrf_token) and "kohami.csrf" in csrf_client.cookies and "kohami.session" not in csrf_client.cookies
                message = f"{'PASS' if passed else 'FAIL'} {case.name}: HTTP {status}" if passed else f"FAIL {case.name}: token/cookie contract incomplete (HTTP {status})"
            else:
                passed, message = check_case(anonymous, case)
            print(message, flush=True)
            failures += int(not passed)
        for case in csrf_client.security_requests():
            passed, message = check_case(csrf_client, case)
            print(message, flush=True)
            failures += int(not passed)
        print(f"RESULT: {'PASS' if failures == 0 else 'FAIL'}; checks={len(acceptance_plan(config['origin'])) + 4}; failures={failures}")
        return 0 if failures == 0 else 1
    finally:
        stop_process(process)


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--api-dll", type=Path, default=DEFAULT_API_DLL)
    parser.add_argument("--runtime-config", type=Path, default=DEFAULT_RUNTIME_CONFIG)
    parser.add_argument("--contract", type=Path, default=DEFAULT_CONTRACT)
    args = parser.parse_args(argv)
    try:
        return run_acceptance(args.api_dll.expanduser(), args.runtime_config.expanduser(), args.contract.expanduser())
    except RuntimeError as error:
        print(str(error), file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
