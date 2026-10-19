#!/usr/bin/env python3
"""Run Issue12 guest checkout acceptance over the public API and dedicated SQL fixture."""
from __future__ import annotations

import argparse
from concurrent.futures import ThreadPoolExecutor
import importlib.util
import json
import os
from pathlib import Path
import re
import shutil
import signal
import socket
import subprocess
import sys
import threading
import time
import urllib.error
import urllib.request
import uuid

ROOT = Path(__file__).resolve().parents[2]
LOCAL = ROOT / ".local" / "m0"
SOURCE_CONFIG = LOCAL / "fixture-private.json"
WORKER_CONFIG = LOCAL / "worker-private.json"
ISSUE02_MARKER = LOCAL / "issue02-compose-project"
ISSUE06_MARKER = LOCAL / "issue06-compose-project"
RUNTIME_CONFIG = LOCAL / "issue12-api-runtime-private.json"
LOG_DIR = LOCAL / "issue12-checkout-logs"
STATE_PATH = LOCAL / "issue12-inventory-state-private.json"
EVIDENCE_PATH = LOCAL / "issue12-api-evidence-private.json"
DOTNET = Path("/usr/local/share/dotnet/dotnet")
API_DLL = ROOT / "apps/api/bin/Release/net10.0/Api.dll"
NATIVE_DLL = ROOT / "tools/native-operations/bin/Release/net10.0/Native.dll"
COMMERCE_DLL = ROOT / "tests/Commerce/bin/Release/net10.0/Commerce.Tests.dll"
MUTATION_HEADERS = frozenset({"Origin", "X-CSRF-TOKEN", "Idempotency-Key"})
SYNTHETIC_EMAIL_DOMAIN = "example" + ".test"
cleanup_only_recorded_ids = True
runs_fixture_migrations = False

PARSER_PATH = ROOT / "scripts/m0/run-issue02-acceptance.py"
PARSER_SPEC = importlib.util.spec_from_file_location("issue02_fixture_parser", PARSER_PATH)
FIXTURE_PARSER = importlib.util.module_from_spec(PARSER_SPEC)
assert PARSER_SPEC and PARSER_SPEC.loader
PARSER_SPEC.loader.exec_module(FIXTURE_PARSER)


def valid_fixture_config(config: dict) -> bool:
    if (config.get("IdentitySqlTestDedicated") is not True
            or config.get("datasetId") != "demo_m0"
            or config.get("origin") != "https://localhost:3000"
            or config.get("sqlServerName") != "localhost"):
        return False
    certificate = config.get("sqlCertificatePath", "")
    if not certificate or not Path(certificate).is_file():
        return False
    for name in ("connectionString", "workerConnectionString"):
        try:
            fields = FIXTURE_PARSER.parse_connection_string(config.get(name, ""))
        except Exception:
            return False
        match = re.fullmatch(r"localhost,(\d+)", fields.get("server", ""), re.IGNORECASE)
        if (not match or not 14336 <= int(match.group(1)) <= 14399
                or fields.get("database", "").casefold() != "kohamim0"
                or fields.get("encrypt", "").casefold() != "strict"
                or fields.get("trustservercertificate", "").casefold() != "false"
                or fields.get("hostnameincertificate", "localhost").casefold() != "localhost"
                or fields.get("server certificate", certificate) != certificate):
            return False
    return True


def local_port(name: str, default: int, minimum: int, maximum: int, environ=os.environ) -> int:
    value = environ.get(name)
    if value is None:
        return default
    if not re.fullmatch(r"[0-9]{1,5}", value):
        raise RuntimeError("ISSUE12_LOCAL_PORT_INVALID")
    port = int(value)
    if not minimum <= port <= maximum:
        raise RuntimeError("ISSUE12_LOCAL_PORT_INVALID")
    return port


def valid_fixture_project(project: str) -> bool:
    return re.fullmatch(r"kohami-issue(?:02|06)-[0-9a-f]{12}", project) is not None


def matches_error(status: int, payload: dict, expected_status: int, expected_code: str) -> bool:
    return status == expected_status and payload.get("code") == expected_code


def checkout_payload(quote_id: str, cart_version: str, email: str, method: str, region: str) -> dict:
    return {
        "quoteId": quote_id,
        "cartVersion": cart_version,
        "contact": {"email": email},
        "delivery": {"method": method, "region": region},
        "confirmed": True,
    }


def synthetic_email(label: str) -> str:
    return f"{label}@{SYNTHETIC_EMAIL_DOMAIN}"


def require_free_port(port: int) -> None:
    with socket.socket() as listener:
        listener.settimeout(0.2)
        if listener.connect_ex(("127.0.0.1", port)) == 0:
            raise RuntimeError("ISSUE12_API_PORT_IN_USE")


class EvidenceStore:
    def __init__(self, path: Path):
        self.path = path
        self.lock = threading.RLock()
        self.data = {
            "CartIds": [], "QuoteIds": [], "Checkouts": [], "IdempotencyKeys": [],
            "RollbackCartId": "", "RollbackQuoteId": "", "Completed": False,
        }
        self.save()

    def save(self) -> None:
        with self.lock:
            temp = self.path.with_suffix(".tmp")
            temp.write_text(json.dumps(self.data, separators=(",", ":")) + "\n")
            if not os.name == "nt":
                temp.chmod(0o600)
            os.replace(temp, self.path)
            if not os.name == "nt":
                self.path.chmod(0o600)

    def add(self, name: str, value) -> None:
        with self.lock:
            if value not in self.data[name]:
                self.data[name].append(value)
            self.save()

    def set(self, name: str, value) -> None:
        with self.lock:
            self.data[name] = value
            self.save()

    def record_checkout(self, payload: dict, sku: str) -> None:
        row = {"CheckoutId": payload["checkoutId"], "OrderId": payload["orderId"], "SkuId": sku}
        with self.lock:
            if row not in self.data["Checkouts"]:
                self.data["Checkouts"].append(row)
            self.save()


class GuestApi:
    def __init__(self, config: dict, evidence: EvidenceStore, api_port: int):
        self.cookies: dict[str, str] = {}
        self.origin = f"http://127.0.0.1:{api_port}/api/v1"
        self.site_origin = config["origin"]
        self.csrf_token: str | None = None
        self.evidence = evidence

    def _cookie_header(self) -> str:
        return "; ".join(f"{name}={value}" for name, value in self.cookies.items())

    def _merge_cookies(self, headers) -> None:
        for value in headers.get_all("Set-Cookie") or []:
            first = value.split(";", 1)[0]
            if "=" in first:
                name, cookie = first.split("=", 1)
                self.cookies[name] = cookie

    def _request(self, method: str, path: str, body=None, *, if_match: str | None = None,
                 idempotency_key: str | None = None) -> tuple[int, dict]:
        headers = {"Cookie": self._cookie_header(), "X-Forwarded-Proto": "https"}
        data = None if body is None else json.dumps(body, separators=(",", ":")).encode()
        if data is not None:
            headers["Content-Type"] = "application/json"
        if method not in ("GET", "HEAD", "DELETE"):
            self._csrf()
            headers["Cookie"] = self._cookie_header()
            key = idempotency_key or "issue12-" + uuid.uuid4().hex
            self.evidence.add("IdempotencyKeys", key)
            headers.update({"Origin": self.site_origin, "X-CSRF-TOKEN": self.csrf_token or "", "Idempotency-Key": key})
        if if_match is not None:
            headers["If-Match"] = f'"{if_match}"'
        request = urllib.request.Request(self.origin + path, data=data, headers=headers, method=method)
        try:
            response = urllib.request.urlopen(request, timeout=20)
        except urllib.error.HTTPError as error:
            self._merge_cookies(error.headers)
            try:
                payload = json.loads(error.read())
            except Exception:
                payload = {}
            finally:
                error.close()
            return error.code, payload
        with response:
            self._merge_cookies(response.headers)
            raw = response.read()
            return response.status, json.loads(raw) if raw else {}

    def _csrf(self) -> None:
        if self.csrf_token is None:
            status, payload = self._request("GET", "/auth/csrf")
            if status != 200 or not payload.get("requestToken"):
                raise RuntimeError("ISSUE12_CSRF_BOOTSTRAP_FAILED")
            self.csrf_token = payload["requestToken"]

    def call(self, method: str, path: str, body=None, *, if_match: str | None = None,
             idempotency_key: str | None = None, expected_status: int | None = None) -> dict:
        status, payload = self._request(method, path, body, if_match=if_match, idempotency_key=idempotency_key)
        if expected_status is not None and status != expected_status:
            raise RuntimeError(f"ISSUE12_API_{method}_UNEXPECTED_STATUS_{status}_{payload.get('code', 'UNKNOWN')}")
        if status >= 400:
            raise RuntimeError("ISSUE12_API_UNEXPECTED_ERROR_" + str(payload.get("code", "UNKNOWN")))
        if "kohami.session" in self.cookies:
            raise RuntimeError("ISSUE12_GUEST_RECEIVED_AUTHENTICATED_SESSION")
        return payload

    def expect_error(self, method: str, path: str, body, expected_status: int, expected_code: str,
                     *, if_match: str | None = None, idempotency_key: str | None = None) -> None:
        status, payload = self._request(method, path, body, if_match=if_match, idempotency_key=idempotency_key)
        if not matches_error(status, payload, expected_status, expected_code):
            actual_code = payload.get("code", "UNKNOWN")
            raise RuntimeError(f"ISSUE12_API_EXPECTED_ERROR_MISMATCH status={status} code={actual_code}")
        if "kohami.session" in self.cookies:
            raise RuntimeError("ISSUE12_GUEST_RECEIVED_AUTHENTICATED_SESSION")


def _record_resources(evidence: EvidenceStore, cart: dict, quote: dict) -> None:
    if not re.fullmatch(r"cart_[0-9a-f]{32}", cart.get("id", "")):
        raise RuntimeError("ISSUE12_CART_RESPONSE_INVALID")
    if not re.fullmatch(r"quote_[0-9a-f]{32}", quote.get("id", "")):
        raise RuntimeError("ISSUE12_QUOTE_RESPONSE_INVALID")
    evidence.add("CartIds", cart["id"])
    evidence.add("QuoteIds", quote["id"])


def make_quote(api: GuestApi, evidence: EvidenceStore, skus: list[str], email: str) -> tuple[dict, dict]:
    cart = api.call("POST", "/carts", {}, expected_status=201)
    evidence.add("CartIds", cart["id"])
    for sku in skus:
        cart = api.call("PUT", f"/carts/current/items/{sku}", {"quantity": 1}, if_match=cart["version"], expected_status=200)
    quote = api.call("POST", "/checkout-quotes", {
        "cartVersion": cart["version"], "deliveryMethod": "mock_home", "region": "demo_supported",
    }, if_match=cart["version"], expected_status=201)
    _record_resources(evidence, cart, quote)
    if len(quote.get("snapshot", {}).get("lines", [])) != len(skus):
        raise RuntimeError("ISSUE12_QUOTE_LINES_INVALID")
    return cart, quote


def record_success(api: GuestApi, evidence: EvidenceStore, result: dict, sku: str, expected_version: str) -> None:
    if result.get("mode") != "mock" or result.get("capabilities") != ["checkout.read", "checkout.pay"]:
        raise RuntimeError("ISSUE12_CHECKOUT_RESPONSE_INVALID")
    if not re.fullmatch(r"checkout_[0-9a-f]{32}", result.get("checkoutId", "")) or not re.fullmatch(r"order_[0-9a-f]{32}", result.get("orderId", "")):
        raise RuntimeError("ISSUE12_CHECKOUT_RESPONSE_INVALID")
    evidence.record_checkout(result, sku)
    status = api.call("GET", f"/checkouts/{result['checkoutId']}/status", expected_status=200)
    if status.get("orderState") != "AwaitingPayment" or status.get("paymentState") != "Created" or status.get("mode") != "mock":
        raise RuntimeError("ISSUE12_CHECKOUT_STATUS_INVALID")
    other = GuestApi.__new__(GuestApi)
    other.cookies = {}
    other.origin = api.origin
    other.site_origin = api.site_origin
    other.csrf_token = None
    other.evidence = evidence
    other._csrf()
    other.expect_error("GET", f"/checkouts/{result['checkoutId']}/status", None, 404, "RESOURCE_NOT_FOUND")
    print("PASS guest checkout is awaiting mock payment and status is private to its creating browser")


def inspect_fixture() -> tuple[dict, int]:
    for path in (SOURCE_CONFIG, WORKER_CONFIG):
        if path.is_symlink() or not path.is_file() or path.stat().st_mode & 0o077:
            raise RuntimeError("ISSUE12_PRIVATE_FIXTURE_MATERIAL_UNSAFE")
    if not LOCAL.is_dir() or LOCAL.is_symlink():
        raise RuntimeError("ISSUE12_PRIVATE_FIXTURE_DIRECTORY_INVALID")
    config = json.loads(SOURCE_CONFIG.read_text())
    if not valid_fixture_config(config):
        raise RuntimeError("ISSUE12_DEDICATED_FIXTURE_MARKER_INVALID")
    markers = [path for path in (ISSUE02_MARKER, ISSUE06_MARKER) if path.exists()]
    if len(markers) != 1:
        raise RuntimeError("ISSUE12_DEDICATED_COMPOSE_MARKER_INVALID")
    marker = markers[0]
    if marker.is_symlink() or not marker.is_file() or marker.stat().st_mode & 0o077:
        raise RuntimeError("ISSUE12_PRIVATE_FIXTURE_MATERIAL_UNSAFE")
    project = marker.read_text().strip()
    if not valid_fixture_project(project):
        raise RuntimeError("ISSUE12_DEDICATED_COMPOSE_MARKER_INVALID")
    match = re.fullmatch(r"localhost,(\d+)", FIXTURE_PARSER.parse_connection_string(config["connectionString"]).get("server", ""), re.IGNORECASE)
    port = int(match.group(1)) if match else 0
    for path in (RUNTIME_CONFIG, LOG_DIR, STATE_PATH, EVIDENCE_PATH):
        if path.exists() or path.is_symlink():
            raise RuntimeError("ISSUE12_RUNNER_PRIVATE_PATH_ALREADY_EXISTS")
    for path in (API_DLL, NATIVE_DLL, COMMERCE_DLL):
        if not path.is_file():
            raise RuntimeError("ISSUE12_RELEASE_BINARY_MISSING")
    return config, port


def write_runtime_config(config: dict, site_origin: str) -> None:
    runtime = {key: value for key, value in config.items() if key not in {
        "MigratorConnectionString", "IdentitySqlTestDedicated",
        "FixtureId",
    }}
    runtime["origin"] = site_origin
    RUNTIME_CONFIG.write_text(json.dumps(runtime, separators=(",", ":")) + "\n")
    if os.name != "nt":
        RUNTIME_CONFIG.chmod(0o600)


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


def wait_health(api_port: int, timeout: float = 60) -> None:
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        try:
            with urllib.request.urlopen(f"http://127.0.0.1:{api_port}/health", timeout=2) as response:
                data = json.loads(response.read())
                if response.status == 200 and data.get("datasetId") == "demo_m0" and data.get("mode") == "mock":
                    return
        except Exception:
            time.sleep(0.25)
    raise RuntimeError("ISSUE12_API_DID_NOT_BECOME_READY")


def run(args: list[str], *, env: dict[str, str] | None = None, timeout: int = 60) -> None:
    subprocess.run(args, cwd=ROOT, env=env, check=True, timeout=timeout)


def run_acceptance(config: dict, port: int, api_port: int, site_port: int) -> None:
    require_free_port(api_port)
    run([str(DOTNET), str(NATIVE_DLL), "fixture-status", str(ROOT), str(port)], timeout=30)
    evidence: EvidenceStore | None = None
    process: subprocess.Popen | None = None
    log_handle = None
    failure: BaseException | None = None
    try:
        evidence = EvidenceStore(EVIDENCE_PATH)
        run([str(DOTNET), str(COMMERCE_DLL), "issue12-fixture", "prepare", str(SOURCE_CONFIG), str(STATE_PATH)], timeout=30)
        site_origin = f"https://localhost:{site_port}"
        write_runtime_config(config, site_origin)
        LOG_DIR.mkdir(mode=0o700)
        log_path = LOG_DIR / "api.log"
        if os.name != "nt":
            log_path.touch(mode=0o600)
        env = os.environ.copy()
        env.update({
            "DOTNET_ENVIRONMENT": "Development", "M0_CONFIG": str(RUNTIME_CONFIG),
            "KOHAMI_API_PORT": str(api_port),
            "M0_CONTRACT": str(ROOT / "docs/contracts/runtime/Core.openapi.json"),
            "M0_HTTPS_CERT": str(LOCAL / "browser/public.pem"),
            "M0_HTTPS_KEY": str(LOCAL / "browser/private.key"),
            "NO_PROXY": "localhost,127.0.0.1,::1", "no_proxy": "localhost,127.0.0.1,::1",
        })
        log_handle = log_path.open("w")
        process = subprocess.Popen([str(DOTNET), str(API_DLL)], cwd=ROOT, env=env,
                                   stdout=log_handle, stderr=subprocess.STDOUT, start_new_session=True)
        wait_health(api_port)
        rows = json.loads(STATE_PATH.read_text())["Rows"]
        sku_a, sku_b = rows[0]["Sku"], rows[1]["Sku"]
        client = GuestApi({**config, "origin": site_origin}, evidence, api_port)

        invalid_delivery_cart = client.call("POST", "/carts", {}, expected_status=201)
        evidence.add("CartIds", invalid_delivery_cart["id"])
        client.expect_error("POST", "/checkout-quotes", {
            "cartVersion": invalid_delivery_cart["version"], "deliveryMethod": "mock_home", "region": "demo_unsupported",
        }, 409, "FEE_UNAVAILABLE", if_match=invalid_delivery_cart["version"])
        print("PASS unsupported delivery region is rejected before quote creation")

        # A stale Cart ETag must reject a quote made from the earlier cart version.
        cart, quote = make_quote(client, evidence, [sku_a], synthetic_email("issue12-version"))
        old_version = cart["version"]
        updated = client.call("PUT", "/carts/current/promotion", {"promotionCode": "DEMO15"},
                              if_match=old_version, expected_status=200)
        stale = checkout_payload(quote["id"], old_version, synthetic_email("issue12-version"), "mock_home", "demo_supported")
        client.expect_error("POST", "/checkouts", stale, 412, "VERSION_CHANGED", if_match=old_version)
        if updated["version"] == old_version:
            raise RuntimeError("ISSUE12_CART_VERSION_DID_NOT_ADVANCE")
        print("PASS stale cart ETag is rejected before checkout writes")

        # Force a later SKU out of stock after quote creation; the first decrement must roll back.
        rollback_client = GuestApi({**config, "origin": site_origin}, evidence, api_port)
        rollback_cart, rollback_quote = make_quote(rollback_client, evidence, [sku_a, sku_b], synthetic_email("issue12-rollback"))
        evidence.set("RollbackCartId", rollback_cart["id"])
        evidence.set("RollbackQuoteId", rollback_quote["id"])
        run([str(DOTNET), str(COMMERCE_DLL), "issue12-fixture", "set-second-zero", str(SOURCE_CONFIG), str(STATE_PATH)], timeout=30)
        rollback_body = checkout_payload(rollback_quote["id"], rollback_cart["version"], synthetic_email("issue12-rollback"), "mock_home", "demo_supported")
        rollback_client.expect_error("POST", "/checkouts", rollback_body, 409, "STOCK_CHANGED", if_match=rollback_cart["version"])
        run([str(DOTNET), str(COMMERCE_DLL), "issue12-fixture", "verify-rollback", str(SOURCE_CONFIG), str(STATE_PATH), str(EVIDENCE_PATH)], timeout=30)
        run([str(DOTNET), str(COMMERCE_DLL), "issue12-fixture", "set-one", str(SOURCE_CONFIG), str(STATE_PATH)], timeout=30)

        # A retry with the same key and identical request returns the exact checkout response.
        idem_client = GuestApi({**config, "origin": site_origin}, evidence, api_port)
        idem_cart, idem_quote = make_quote(idem_client, evidence, [sku_a], synthetic_email("issue12-idem"))
        idem_body = checkout_payload(idem_quote["id"], idem_cart["version"], synthetic_email("issue12-idem"), "mock_home", "demo_supported")
        idem_key = "issue12-" + uuid.uuid4().hex
        first = idem_client.call("POST", "/checkouts", idem_body, if_match=idem_cart["version"],
                                 idempotency_key=idem_key, expected_status=201)
        evidence.record_checkout(first, sku_a)
        replay = idem_client.call("POST", "/checkouts", idem_body, if_match=idem_cart["version"],
                                  idempotency_key=idem_key, expected_status=201)
        if replay != first:
            raise RuntimeError("ISSUE12_IDEMPOTENCY_REPLAY_CHANGED_RESPONSE")
        record_success(idem_client, evidence, first, sku_a, idem_cart["version"])
        print("PASS same-key checkout retry returns the original successful response")

        # Two browser identities race for the single remaining unit through public HTTP.
        race_clients = [GuestApi({**config, "origin": site_origin}, evidence, api_port), GuestApi({**config, "origin": site_origin}, evidence, api_port)]
        prepared = [make_quote(api, evidence, [sku_b], synthetic_email(f"issue12-race-{index}")) for index, api in enumerate(race_clients)]
        def submit(index: int):
            api = race_clients[index]
            cart_value, quote_value = prepared[index]
            body = checkout_payload(quote_value["id"], cart_value["version"], synthetic_email(f"issue12-race-{index}"), "mock_home", "demo_supported")
            return api._request("POST", "/checkouts", body, if_match=cart_value["version"])
        with ThreadPoolExecutor(max_workers=2) as pool:
            race_results = list(pool.map(submit, range(2)))
        successes = [payload for status, payload in race_results if status == 201]
        failures = [(status, payload) for status, payload in race_results if status != 201]
        if len(successes) != 1 or len(failures) != 1 or not matches_error(failures[0][0], failures[0][1], 409, "STOCK_CHANGED"):
            raise RuntimeError("ISSUE12_LAST_STOCK_RACE_RESULT_INVALID")
        winner_index = next(i for i, result in enumerate(race_results) if result[0] == 201)
        record_success(race_clients[winner_index], evidence, successes[0], sku_b, prepared[winner_index][0]["version"])
        evidence.set("Completed", True)
        run([str(DOTNET), str(COMMERCE_DLL), "issue12-fixture", "cleanup", str(SOURCE_CONFIG), str(STATE_PATH), str(EVIDENCE_PATH)], timeout=60)
    except BaseException as error:
        failure = error
    finally:
        stop_group(process)
        if log_handle is not None:
            log_handle.close()
        if STATE_PATH.exists() and EVIDENCE_PATH.exists():
            try:
                run([str(DOTNET), str(COMMERCE_DLL), "issue12-fixture", "cleanup", str(SOURCE_CONFIG), str(STATE_PATH), str(EVIDENCE_PATH)], timeout=60)
            except Exception:
                print("ISSUE12_CLEANUP_REQUIRED: preserve the private state/evidence files for recovery", file=sys.stderr)
                failure = failure or RuntimeError("ISSUE12_CLEANUP_REQUIRED")
        elif EVIDENCE_PATH.exists() and not STATE_PATH.exists():
            EVIDENCE_PATH.unlink()
        for path in (RUNTIME_CONFIG,):
            path.unlink(missing_ok=True)
        if failure is None:
            shutil.rmtree(LOG_DIR, ignore_errors=True)
        elif LOG_DIR.exists():
            print("ISSUE12_API_LOG_RETAINED: " + str(LOG_DIR / "api.log"), file=sys.stderr)
    if failure is not None:
        raise failure
    if STATE_PATH.exists() or EVIDENCE_PATH.exists():
        raise RuntimeError("ISSUE12_PRIVATE_EVIDENCE_REMAINS")
    print("PASS Issue12 guest API/SQL acceptance: rollback, stale version, idempotency, last-unit race and guest scope")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check-only", action="store_true", help="Validate fixture prerequisites and binaries without starting services or changing SQL")
    args = parser.parse_args()
    config, port = inspect_fixture()
    api_port = local_port("KOHAMI_API_PORT", 5080, 5080, 5099)
    site_port = local_port("KOHAMI_SITE_PORT", 3000, 3000, 3099)
    require_free_port(api_port)
    if args.check_only:
        print(f"PASS Issue12 dedicated fixture configuration and API binaries are ready; SQL was not changed; API port {api_port}, site origin https://localhost:{site_port}")
        return 0
    run_acceptance(config, port, api_port, site_port)
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as error:
        message = str(error)
        print("ISSUE12_API_ACCEPTANCE_FAILED: " + type(error).__name__ + (":" + message if len(message) < 100 else ""), file=sys.stderr)
        raise SystemExit(1)
