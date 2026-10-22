"""Run the bounded Issue25 Analyst report-scope acceptance on a released fixture."""

from __future__ import annotations

import argparse
from dataclasses import dataclass
import http.cookies
import json
from pathlib import Path
import subprocess
from urllib.error import HTTPError, URLError
from urllib.parse import urlparse
from urllib.request import Request, urlopen

ROOT = Path(__file__).resolve().parents[2]
HELPER_SOURCE = ROOT / "tests/Commerce/Issue25AnalystReportsAcceptance.cs"
COMMERCE_PROJECT = ROOT / "tests/Commerce/Commerce.Tests.csproj"
COMMERCE = ROOT / "tests/Commerce/bin/Release/net10.0/Commerce.Tests.dll"
STATE_NAME = "issue25-analyst-reports-private.json"
MAINTENANCE_STATE_NAME = "issue11-maintenance-state-private.json"
REPORT_PATH = "/api/v1/admin/reports?from=2026-10-22&to=2026-10-22"


@dataclass(frozen=True)
class RequestCase:
    name: str
    method: str
    path: str
    expected_status: int
    expected_code: str | None = None


def authorization_cases() -> list[RequestCase]:
    return [
        RequestCase("exact Analyst report scope allowed", "GET", REPORT_PATH, 200),
        RequestCase("wrong Analyst report scope denied", "GET", REPORT_PATH, 403, "PERMISSION_CHANGED"),
        RequestCase("revoked Analyst report grant denied in active session", "GET", REPORT_PATH, 403, "PERMISSION_CHANGED"),
    ]


class ApiClient:
    def __init__(self, api_base: str, origin: str):
        self.api_base = api_base.rstrip("/")
        self.origin = origin
        self.cookies: dict[str, str] = {}
        self.csrf_token: str | None = None

    def _cookie_header(self) -> str:
        return "; ".join(f"{name}={value}" for name, value in self.cookies.items())

    def _merge_cookies(self, headers) -> None:
        for raw in headers.get_all("Set-Cookie") or []:
            parsed = http.cookies.SimpleCookie()
            parsed.load(raw)
            for name, morsel in parsed.items():
                self.cookies[name] = morsel.value

    def request(self, method: str, path: str, *, headers: dict[str, str] | None = None,
                body: dict | None = None) -> tuple[int, dict]:
        request_headers = {"X-Forwarded-Proto": "https", "Cookie": self._cookie_header()}
        if headers:
            request_headers.update(headers)
        data = None if body is None else json.dumps(body, separators=(",", ":")).encode()
        if data is not None:
            request_headers["Content-Type"] = "application/json"
        request = Request(self.api_base + path, data=data, headers=request_headers, method=method)
        try:
            response = urlopen(request, timeout=10)
        except HTTPError as error:
            self._merge_cookies(error.headers)
            try:
                payload = json.loads(error.read())
            except (OSError, UnicodeError, json.JSONDecodeError):
                payload = {}
            return error.code, payload
        except (OSError, URLError):
            raise RuntimeError("API_REQUEST_FAILED") from None
        with response:
            self._merge_cookies(response.headers)
            try:
                raw = response.read()
                payload = json.loads(raw) if raw else {}
            except (UnicodeError, json.JSONDecodeError):
                payload = {}
            return response.status, payload

    def login(self, email: str, password: str) -> None:
        status, payload = self.request("GET", "/api/v1/auth/csrf")
        self.csrf_token = payload.get("requestToken") if isinstance(payload, dict) else None
        if status != 200 or not self.csrf_token:
            raise RuntimeError("CSRF_BOOTSTRAP_FAILED")
        status, payload = self.request("POST", "/api/v1/auth/sessions", headers={
            "Origin": self.origin,
            "X-CSRF-TOKEN": self.csrf_token,
        }, body={"email": email, "password": password})
        permissions = payload.get("permissions", []) if isinstance(payload, dict) else []
        if (status != 200 or payload.get("state") != "AdminAuthenticated"
                or "reports.read" not in permissions or "stock.adjust" in permissions):
            state = payload.get("state") if isinstance(payload, dict) else None
            code = payload.get("code") if isinstance(payload, dict) else None
            has_reports = isinstance(permissions, list) and "reports.read" in permissions
            has_stock_adjust = isinstance(permissions, list) and "stock.adjust" in permissions
            raise RuntimeError(f"SYNTHETIC_ANALYST_LOGIN_INVALID:status={status}:state={state}:code={code}:reports={has_reports}:stock_adjust={has_stock_adjust}")


def check_case(client: ApiClient, case: RequestCase) -> None:
    status, payload = client.request(case.method, case.path)
    code = payload.get("code") if isinstance(payload, dict) else None
    if status != case.expected_status or (case.expected_code is not None and code != case.expected_code):
        raise RuntimeError(f"{case.name.replace(' ', '_').upper()}_FAILED")


def invoke_sql_helper(action: str, config_path: Path, state_path: Path) -> None:
    if action not in {"seed", "verify-authorized", "scope-wrong", "verify-wrong", "scope-restore",
                      "revoke", "verify-revoked", "cleanup", "maintenance-open", "maintenance-restore"}:
        raise RuntimeError("SQL_HELPER_ACTION_INVALID")
    if not HELPER_SOURCE.is_file() or not COMMERCE_PROJECT.is_file() or not COMMERCE.is_file():
        raise RuntimeError("SQL_HELPER_SOURCE_UNAVAILABLE")
    try:
        subprocess.run([str(Path("/usr/local/share/dotnet/dotnet")), str(COMMERCE),
                        "--issue25-analyst-reports", action, str(config_path), str(state_path)],
                       cwd=ROOT, check=True, stdin=subprocess.DEVNULL,
                       stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=60)
    except (OSError, subprocess.SubprocessError):
        raise RuntimeError(f"SQL_HELPER_{action.upper()}_FAILED") from None


def run_acceptance(api_base: str, origin: str, config_path: Path) -> None:
    state_path = config_path.with_name(STATE_NAME)
    maintenance_path = config_path.with_name(MAINTENANCE_STATE_NAME)
    if any(path.exists() or path.is_symlink() for path in (state_path, maintenance_path)):
        raise RuntimeError("PRIVATE_STATE_ALREADY_EXISTS")
    client = ApiClient(api_base, origin)
    window_open = False
    try:
        invoke_sql_helper("maintenance-open", config_path, maintenance_path)
        window_open = True
        invoke_sql_helper("seed", config_path, state_path)
        state = json.loads(state_path.read_text(encoding="utf-8"))
        client.login(state["Email"], state["Password"])
        cases = authorization_cases()

        check_case(client, cases[0])
        invoke_sql_helper("verify-authorized", config_path, state_path)

        invoke_sql_helper("scope-wrong", config_path, state_path)
        check_case(client, cases[1])
        invoke_sql_helper("verify-wrong", config_path, state_path)

        invoke_sql_helper("scope-restore", config_path, state_path)
        invoke_sql_helper("revoke", config_path, state_path)
        check_case(client, cases[2])
        invoke_sql_helper("verify-revoked", config_path, state_path)
    finally:
        try:
            if state_path.exists():
                invoke_sql_helper("cleanup", config_path, state_path)
        finally:
            if window_open and maintenance_path.exists():
                invoke_sql_helper("maintenance-restore", config_path, maintenance_path)


def require_loopback(url: str, schemes: set[str]) -> None:
    parsed = urlparse(url)
    if parsed.scheme not in schemes or parsed.hostname not in {"localhost", "127.0.0.1", "::1"}:
        raise RuntimeError("LOOPBACK_URL_REQUIRED")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--sql-config", type=Path, required=True)
    parser.add_argument("--api-base", required=True)
    parser.add_argument("--origin", default="https://localhost:3000")
    parser.add_argument("--approved-fixture-run", action="store_true",
                        help="Required acknowledgement before this runner writes synthetic rows to kohami-sql-test.")
    parser.add_argument("--approved-maintenance-window", action="store_true",
                        help="Required acknowledgement before this runner temporarily opens the dedicated fixture gate.")
    args = parser.parse_args()
    if not args.approved_fixture_run:
        parser.error("--approved-fixture-run is required before dedicated fixture writes")
    if not args.approved_maintenance_window:
        parser.error("--approved-maintenance-window is required before opening the dedicated fixture gate")
    require_loopback(args.api_base, {"http", "https"})
    require_loopback(args.origin, {"https"})
    if args.origin != "https://localhost:3000":
        parser.error("--origin must match the canonical kohami-sql-test origin")
    try:
        run_acceptance(args.api_base, args.origin, args.sql_config.resolve(strict=True))
    except RuntimeError as error:
        print(f"BLOCKED_OR_FAILED: {error}")
        return 1
    print("RESULT: PASS; authenticated Analyst scope cases=3; SQL readback and cleanup=PASS")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
