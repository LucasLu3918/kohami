#!/usr/bin/env python3
"""Run Issue 06 real HTTP/API/SQL aftercare using a synthetic pre-auth session."""
from __future__ import annotations

import importlib.util
import argparse
import json
from http.cookies import SimpleCookie
import os
from pathlib import Path
import re
import shutil
import signal
import socket
import ssl
import subprocess
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid

ROOT = Path(__file__).resolve().parents[2]
LOCAL = Path(os.environ.get('KOHAMI_M0_LOCAL_DIR', str(ROOT / '.local' / 'm0'))).expanduser()
FIXTURE_ROOT = Path(os.environ.get('KOHAMI_M0_FIXTURE_ROOT', str(ROOT))).expanduser()
SOURCE_CONFIG = LOCAL / 'fixture-private.json'
WORKER_CONFIG = LOCAL / 'worker-private.json'
RUNTIME_CONFIG = LOCAL / 'issue06-aftercare-runtime-private.json'
RUNTIME_WORKER_CONFIG = LOCAL / 'issue06-aftercare-worker-private.json'
ARTIFACT = LOCAL / 'issue06-synthetic-session-private.json'
LOG_DIR = LOCAL / 'issue06-aftercare-logs'
TEST_OUTPUT = LOCAL / 'issue06-playwright-artifacts'
DOTNET = Path('/usr/local/share/dotnet/dotnet')
API_DLL = ROOT / 'apps/api/bin/Release/net10.0/Api.dll'
NATIVE_DLL = ROOT / 'tools/native-operations/bin/Release/net10.0/Native.dll'
COMMERCE_DLL = ROOT / 'tests/Commerce/bin/Release/net10.0/Commerce.Tests.dll'
PLAYWRIGHT = ROOT / 'apps/storefront/node_modules/.bin/playwright'
STOREFRONT = ROOT / 'apps/storefront/node_modules/.bin/nuxt'
ACCEPTANCE_STAGES = frozenset({
    'fixture', 'synthetic_session', 'security_guards', 'catalog', 'checkout', 'payment', 'scenario_authorization',
    'shipment', 'return_inspection', 'refund', 'browser_ui', 'm0_ac02', 'stock', 'catalog_search', 'cart', 'complete', 'diagnostic_unavailable',
})
CURRENT_STAGE = 'fixture'


def set_stage(stage: str) -> None:
    global CURRENT_STAGE
    CURRENT_STAGE = stage if stage in ACCEPTANCE_STAGES else 'diagnostic_unavailable'


def valid_issue06_project(project: str) -> bool:
    return bool(re.fullmatch(r'kohami-issue06-[0-9a-f]{12}', project))


def connection_targets_issue06_fixture(connection: str, certificate_path: str) -> bool:
    parser_path = ROOT / 'scripts/m0/run-issue02-acceptance.py'
    spec = importlib.util.spec_from_file_location('issue02_acceptance_parser', parser_path)
    module = importlib.util.module_from_spec(spec)
    assert spec and spec.loader
    spec.loader.exec_module(module)
    return module.connection_targets_fixture(connection, certificate_path)


def issue06_fixture_port(connection: str) -> int:
    parser_path = ROOT / 'scripts/m0/run-issue02-acceptance.py'
    spec = importlib.util.spec_from_file_location('issue02_acceptance_parser', parser_path)
    module = importlib.util.module_from_spec(spec)
    assert spec and spec.loader
    spec.loader.exec_module(module)
    fields = module.parse_connection_string(connection)
    match = re.fullmatch(r'localhost,(\d+)', fields.get('server', ''), re.IGNORECASE)
    port = int(match.group(1)) if match else 0
    if not 14336 <= port <= 14399:
        fail('BLOCKED: fixture port is outside the dedicated loopback range')
    return port


def fail(message: str) -> None:
    raise RuntimeError(message)


def require_free_port(port: int) -> None:
    with socket.socket() as listener:
        listener.settimeout(0.25)
        if listener.connect_ex(('127.0.0.1', port)) == 0:
            fail(f'BLOCKED: localhost port {port} is in use; no process was stopped')


def wait_url(url: str, timeout: float = 60, *, tls: bool = False) -> None:
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        try:
            context = ssl._create_unverified_context() if tls else None
            with urllib.request.urlopen(url, timeout=2, context=context) as response:
                if response.status == 200:
                    return
        except Exception:
            time.sleep(0.5)
    fail('BLOCKED: local API did not become ready')


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


def inspect_fixture() -> tuple[dict, str, int]:
    if not LOCAL.is_absolute() or LOCAL.is_symlink() or not LOCAL.is_dir():
        fail('BLOCKED: private fixture directory must be an existing, non-symlink directory')
    if not FIXTURE_ROOT.is_absolute() or FIXTURE_ROOT.is_symlink() or not (FIXTURE_ROOT / '.local' / 'm0').is_dir():
        fail('BLOCKED: fixture provisioning root must contain its private fixture files')
    if not DOTNET.is_file() or not SOURCE_CONFIG.is_file() or not WORKER_CONFIG.is_file():
        fail('BLOCKED: Issue06 fixture config or local runtime is missing')
    for path in (SOURCE_CONFIG, WORKER_CONFIG, LOCAL / 'issue06-compose-project'):
        if path.is_symlink() or path.stat().st_mode & 0o077:
            fail('BLOCKED: private Issue06 fixture material is unsafe')
    project = (LOCAL / 'issue06-compose-project').read_text().strip()
    if not valid_issue06_project(project):
        fail('BLOCKED: owned Issue06 compose marker is invalid')
    config = json.loads(SOURCE_CONFIG.read_text())
    if config.get('IdentitySqlTestDedicated') is not True or config.get('DevelopmentFixtureBootstrapEnabled') is not True or config.get('datasetId') != 'demo_m0':
        fail('BLOCKED: dedicated Issue06 fixture marker is missing')
    certificate = config.get('sqlCertificatePath', '')
    fixture_port = issue06_fixture_port(config.get('connectionString', ''))
    if not connection_targets_issue06_fixture(config.get('connectionString', ''), certificate):
        fail('BLOCKED: API connection is outside the pinned Issue06 SQL fixture')
    if not connection_targets_issue06_fixture(config.get('workerConnectionString', ''), certificate):
        fail('BLOCKED: worker connection is outside the pinned Issue06 SQL fixture')
    if config.get('origin') != 'https://localhost:3000' or config.get('sqlServerName') != 'localhost':
        fail('BLOCKED: fixture origin or SQL TLS identity is invalid')
    if not all(path.is_file() for path in (API_DLL, NATIVE_DLL, COMMERCE_DLL, PLAYWRIGHT, STOREFRONT,
            LOCAL / 'browser' / 'public.pem', LOCAL / 'browser' / 'private.key')):
        fail('BLOCKED: required Release binaries are missing')
    if any(path.exists() or path.is_symlink() for path in (RUNTIME_CONFIG, RUNTIME_WORKER_CONFIG, ARTIFACT, LOG_DIR, TEST_OUTPUT)):
        fail('BLOCKED: an Issue06 aftercare runner-owned private path already exists')
    json.loads(WORKER_CONFIG.read_text())
    return config, project, fixture_port


class ApiAcceptance:
    def __init__(self, artifact_path: Path):
        artifact = json.loads(artifact_path.read_text())
        self.cookies = {
            'kohami.session': artifact['sessionCookie'],
            'kohami.browser': artifact['browserCookie'],
        }
        self.origin = 'http://127.0.0.1:5080/api/v1'
        self.csrf_token: str | None = None

    def _cookie_header(self) -> str:
        return '; '.join(f'{name}={value}' for name, value in self.cookies.items())

    def _merge_cookies(self, headers) -> None:
        for value in headers.get_all('Set-Cookie') or []:
            parsed = SimpleCookie()
            parsed.load(value)
            for name, morsel in parsed.items():
                self.cookies[name] = morsel.value

    def _raw(self, method: str, path: str, body=None, headers=None):
        request_headers = {'Cookie': self._cookie_header(), 'X-Forwarded-Proto': 'https'}
        if headers:
            request_headers.update(headers)
        data = None if body is None else json.dumps(body, separators=(',', ':')).encode()
        if data is not None:
            request_headers['Content-Type'] = 'application/json'
        request = urllib.request.Request(self.origin + path, data=data, headers=request_headers, method=method)
        try:
            response = urllib.request.urlopen(request, timeout=20)
        except urllib.error.HTTPError as error:
            self._merge_cookies(error.headers)
            try:
                payload = json.loads(error.read())
                code = payload.get('code', 'UNKNOWN_API_ERROR')
            except Exception:
                code = 'UNKNOWN_API_ERROR'
            raise RuntimeError(f'API_{method}_{path.split("/")[1]}_HTTP_{error.code}:{code}') from None
        with response:
            self._merge_cookies(response.headers)
            payload = json.loads(response.read()) if response.status != 204 else None
            return response.status, payload

    def expect_error(self, method: str, path: str, expected_status: int, expected_code: str) -> None:
        request = urllib.request.Request(
            self.origin + path,
            headers={'Cookie': self._cookie_header(), 'X-Forwarded-Proto': 'https'},
            method=method,
        )
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
            if error.code != expected_status or payload.get('code') != expected_code:
                raise RuntimeError('API_EXPECTED_DENIAL_UNEXPECTED_RESPONSE') from None
            return
        else:
            response.close()
            raise RuntimeError('API_EXPECTED_DENIAL_ACCEPTED')

    def _csrf(self) -> None:
        if self.csrf_token is None:
            _, payload = self._raw('GET', '/auth/csrf')
            self.csrf_token = payload['requestToken']

    def call(self, method: str, path: str, body=None, if_match: str | None = None, expected_status: int | None = None):
        headers = {}
        if method not in ('GET', 'HEAD', 'DELETE'):
            self._csrf()
            headers.update({
                'Origin': 'https://localhost:3000',
                'X-CSRF-TOKEN': self.csrf_token or '',
                'Idempotency-Key': 'issue06-' + uuid.uuid4().hex,
            })
        if if_match is not None:
            headers['If-Match'] = f'"{if_match}"'
        status, payload = self._raw(method, path, body, headers)
        if expected_status is not None and status != expected_status:
            raise RuntimeError(f'API_{method}_UNEXPECTED_STATUS:{status}')
        return payload

    def assert_security_guards(self) -> None:
        self._csrf()
        body = json.dumps({'email': 'blocked@example.test', 'password': 'synthetic-only'}, separators=(',', ':')).encode()

        def rejected(headers: dict[str, str]) -> None:
            request_headers = {
                'Cookie': self._cookie_header(), 'X-Forwarded-Proto': 'https',
                'Content-Type': 'application/json', **headers,
            }
            request = urllib.request.Request(self.origin + '/auth/sessions', data=body, headers=request_headers, method='POST')
            try:
                response = urllib.request.urlopen(request, timeout=20)
            except urllib.error.HTTPError as error:
                try:
                    payload = json.loads(error.read())
                except Exception:
                    payload = {}
                if error.code == 403 and payload.get('code') == 'CSRF_INVALID':
                    return
                raise RuntimeError('API_SECURITY_GUARD_REJECTED_WITH_UNEXPECTED_RESPONSE') from None
            else:
                response.close()
                raise RuntimeError('API_SECURITY_GUARD_ACCEPTED_UNPROTECTED_REQUEST')

        rejected({'Origin': 'https://attacker.invalid', 'X-CSRF-TOKEN': self.csrf_token or ''})
        rejected({'Origin': 'https://localhost:3000'})
        print('PASS API rejects wrong Origin and missing antiforgery token before Identity handlers')


def assert_equal(actual, expected, code: str) -> None:
    if actual != expected:
        raise RuntimeError(code)


def assert_checkout_owner_scope(api: ApiAcceptance, artifact_path: Path, checkout_id: str):
    status = api.call('GET', f'/checkouts/{checkout_id}/status')
    other_browser = ApiAcceptance(artifact_path)
    other_browser.cookies = {}
    other_browser._raw('GET', '/auth/csrf')
    other_browser.expect_error(
        'GET', f'/checkouts/{checkout_id}/status', 404, 'RESOURCE_NOT_FOUND',
    )
    print('PASS checkout status is readable by its owner browser and hidden from a different signed browser')
    return status


def assert_checkout_ssr_scope(api: ApiAcceptance, artifact_path: Path, checkout_status: dict) -> None:
    checkout_id = checkout_status['checkoutId']
    total = checkout_status['snapshot']['totalTwd']
    url = 'https://localhost:3000/checkout/' + urllib.parse.quote(checkout_id, safe='') + '?lang=en'
    tls = ssl._create_unverified_context()

    def read_page(cookie_header: str) -> tuple[int, str]:
        request = urllib.request.Request(url, headers={'Cookie': cookie_header})
        with urllib.request.urlopen(request, timeout=30, context=tls) as response:
            return response.status, response.read().decode('utf-8', errors='replace')

    owner_status, owner_html = read_page(api._cookie_header())
    if owner_status != 200 or checkout_id not in owner_html or f'TWD {total}' not in owner_html or 'Mock mode' not in owner_html:
        raise RuntimeError('SSR_OWNER_CHECKOUT_SUMMARY_INVALID')

    other_browser = ApiAcceptance(artifact_path)
    other_browser.cookies = {}
    other_browser._raw('GET', '/auth/csrf')
    denied_status, denied_html = read_page(other_browser._cookie_header())
    if (denied_status != 200 or 'This checkout result is unavailable' not in denied_html
            or f'<code>{checkout_id}</code>' in denied_html or f'TWD {total}' in denied_html
            or 'sku_demo_' in denied_html or 'traceId' in denied_html):
        raise RuntimeError('SSR_CROSS_BROWSER_CHECKOUT_NOT_HIDDEN')
    print('PASS live Nuxt SSR shows checkout summary to its owner and only a generic unavailable page to another signed browser')


def run_api_journey(artifact_path: Path) -> tuple[str, str, str]:
    set_stage('catalog')
    api = ApiAcceptance(artifact_path)
    session = api.call('GET', '/auth/session')
    if session.get('state') != 'AdminAuthenticated' or not {'demo.control', 'orders.read'}.issubset(session.get('permissions', [])):
        raise RuntimeError('SYNTHETIC_OWNER_SESSION_NOT_AUTHORIZED')
    print('PASS synthetic pre-authenticated Owner session accepted by API; no login or TOTP was performed')
    catalog = api.call('GET', '/products')
    products = [item for item in catalog['items'] if item.get('availability') == 'InStock']
    if not products:
        raise RuntimeError('CATALOG_NO_IN_STOCK_ITEM')
    print(f'PASS API catalog returned {len(catalog["items"])} synthetic products')

    set_stage('checkout')
    cart = api.call('POST', '/carts', {}, expected_status=201)
    cart = api.call('PUT', f'/carts/current/items/{products[0]["skuId"]}', {'quantity': 1}, cart['version'], 200)
    quote = api.call('POST', '/checkout-quotes', {
        'cartVersion': cart['version'], 'deliveryMethod': 'mock_home', 'region': 'demo_supported',
    }, cart['version'], 201)
    checkout = api.call('POST', '/checkouts', {
        'quoteId': quote['id'], 'cartVersion': cart['version'],
        'contact': {'email': 'issue06-buyer@example.test'},
        'delivery': {'method': 'mock_home', 'region': 'demo_supported'}, 'confirmed': True,
    }, cart['version'], 201)
    set_stage('payment')
    status = assert_checkout_owner_scope(api, artifact_path, checkout['checkoutId'])
    assert_checkout_ssr_scope(api, artifact_path, status)
    payment = api.call('POST', f'/checkouts/{checkout["checkoutId"]}/payment-attempts', {
        'expectedVersion': status['version'],
    }, status['version'], 202)
    print('PASS API checkout and mock payment attempt created')

    set_stage('scenario_authorization')
    scenario = api.call('POST', '/admin/demo/scenarios', {
        'targetKind': 'PaymentAttempt', 'targetId': payment['id'], 'scenario': 'success',
        'expectedVersion': payment['version'],
    }, payment['version'], 202)
    deadline = time.monotonic() + 30
    while time.monotonic() < deadline:
        status = api.call('GET', f'/checkouts/{checkout["checkoutId"]}/status')
        if status['orderState'] == 'Confirmed' and status['paymentState'] == 'Succeeded':
            break
        time.sleep(0.25)
    else:
        run([str(DOTNET), str(COMMERCE_DLL), 'issue06-owner-worker-state', str(SOURCE_CONFIG), checkout['checkoutId'], payment['id'], scenario['id']], timeout=20)
        raise RuntimeError('MOCK_PAYMENT_SUCCESS_TIMEOUT:' + str(status.get('orderState')) + '/' + str(status.get('paymentState')))

    run([str(DOTNET), str(COMMERCE_DLL), 'issue06-owner-worker-state', str(SOURCE_CONFIG), checkout['checkoutId'], payment['id'], scenario['id']], timeout=20)
    order_id = checkout['orderId']
    run([str(DOTNET), str(COMMERCE_DLL), 'issue06-owner-capabilities', str(SOURCE_CONFIG), str(artifact_path), order_id], timeout=30)
    artifact = json.loads(artifact_path.read_text())
    api.cookies['kohami.guest.read'] = artifact['guestReadCookie']
    api.cookies['kohami.guest.action'] = artifact['guestActionCookie']
    order = api.call('GET', f'/orders/{order_id}')
    receipts = api.call('GET', f'/orders/{order_id}/receipts')
    assert_equal(receipts['taxEffect'], 'none', 'DEMO_RECEIPT_TAX_POLICY_INVALID')
    set_stage('shipment')
    shipment = api.call('POST', f'/admin/orders/{order_id}/shipments', {
        'expectedVersion': order['version'],
    }, order['version'], 201)
    for state in ('Shipped', 'InTransit', 'Delivered'):
        shipment = api.call('POST', f'/admin/shipments/{shipment["id"]}/transitions', {
            'state': state, 'eventId': 'issue06_' + uuid.uuid4().hex, 'expectedVersion': shipment['version'],
        }, shipment['version'], 200)
    print('PASS API shipment reached Delivered; receipt policy remains DEMO/no-tax')

    set_stage('return_inspection')
    order = api.call('GET', f'/orders/{order_id}')
    line_id = order['snapshot']['lines'][0]['orderLineId']
    ret = api.call('POST', f'/orders/{order_id}/returns', {
        'orderLineId': line_id, 'quantity': 1, 'reason': 'Synthetic Issue06 API acceptance',
    }, expected_status=201)
    ret = api.call('POST', f'/admin/returns/{ret["id"]}/transitions', {
        'state': 'Accepted', 'expectedVersion': ret['version'],
    }, ret['version'], 200)
    ret = api.call('POST', f'/admin/returns/{ret["id"]}/transitions', {
        'state': 'Received', 'expectedVersion': ret['version'],
    }, ret['version'], 200)
    inspection = api.call('POST', f'/admin/returns/{ret["id"]}/inspections', {
        'resellableQuantity': 1, 'reason': 'Synthetic item inspected', 'expectedVersion': ret['version'],
    }, ret['version'], 200)
    case = inspection['inspection']['refundCaseId']
    refund_amount = sum(unit['paidTwd'] for unit in inspection['inspection']['refundUnits'])
    if refund_amount <= 0:
        raise RuntimeError('REFUND_AMOUNT_INVALID')
    print('PASS API return request, accept, receive, and inspection created a refund case')

    set_stage('refund')
    run([str(DOTNET), str(COMMERCE_DLL), 'issue06-owner-proof', str(SOURCE_CONFIG), str(artifact_path), case], timeout=30)
    operation = api.call('POST', f'/admin/refund-cases/{case}/operations', {
        'lines': [{'orderLineId': line_id, 'quantity': 1}], 'refundShipping': False,
        'amountTwd': refund_amount, 'reason': 'Synthetic Issue06 API acceptance',
        'expectedVersion': inspection['inspection']['refundCaseVersion'],
    }, inspection['inspection']['refundCaseVersion'], 202)
    if operation.get('mode') != 'mock' or operation.get('amountTwd') != refund_amount:
        raise RuntimeError('REFUND_API_RESPONSE_INVALID')
    run([str(DOTNET), str(COMMERCE_DLL), 'issue06-owner-verify', str(SOURCE_CONFIG), str(artifact_path), order_id, operation['id']], timeout=30)
    print('PASS API refund request and SQL order/shipment/return/refund rows verified')
    return order_id, case, operation['id']


def prepare_browser_order(artifact_path: Path) -> str:
    """Create a confirmed synthetic order; the browser owns all aftercare actions."""
    set_stage('catalog')
    api = ApiAcceptance(artifact_path)
    session = api.call('GET', '/auth/session')
    if session.get('state') != 'AdminAuthenticated' or not {'demo.control', 'orders.read'}.issubset(session.get('permissions', [])):
        raise RuntimeError('SYNTHETIC_OWNER_SESSION_NOT_AUTHORIZED')
    catalog = api.call('GET', '/products')
    products = [item for item in catalog['items'] if item.get('availability') == 'InStock']
    if not products:
        raise RuntimeError('CATALOG_NO_IN_STOCK_ITEM')

    set_stage('checkout')
    cart = api.call('POST', '/carts', {}, expected_status=201)
    cart = api.call('PUT', f'/carts/current/items/{products[0]["skuId"]}', {'quantity': 1}, cart['version'], 200)
    quote = api.call('POST', '/checkout-quotes', {
        'cartVersion': cart['version'], 'deliveryMethod': 'mock_home', 'region': 'demo_supported',
    }, cart['version'], 201)
    checkout = api.call('POST', '/checkouts', {
        'quoteId': quote['id'], 'cartVersion': cart['version'],
        'contact': {'email': 'issue06-browser@example.test'},
        'delivery': {'method': 'mock_home', 'region': 'demo_supported'}, 'confirmed': True,
    }, cart['version'], 201)
    set_stage('payment')
    status = assert_checkout_owner_scope(api, artifact_path, checkout['checkoutId'])
    assert_checkout_ssr_scope(api, artifact_path, status)
    payment = api.call('POST', f'/checkouts/{checkout["checkoutId"]}/payment-attempts', {
        'expectedVersion': status['version'],
    }, status['version'], 202)

    set_stage('scenario_authorization')
    scenario = api.call('POST', '/admin/demo/scenarios', {
        'targetKind': 'PaymentAttempt', 'targetId': payment['id'], 'scenario': 'success',
        'expectedVersion': payment['version'],
    }, payment['version'], 202)
    deadline = time.monotonic() + 30
    while time.monotonic() < deadline:
        status = api.call('GET', f'/checkouts/{checkout["checkoutId"]}/status')
        if status['orderState'] == 'Confirmed' and status['paymentState'] == 'Succeeded':
            break
        time.sleep(0.25)
    else:
        run([str(DOTNET), str(COMMERCE_DLL), 'issue06-owner-worker-state', str(SOURCE_CONFIG), checkout['checkoutId'], payment['id'], scenario['id']], timeout=20)
        raise RuntimeError('MOCK_PAYMENT_SUCCESS_TIMEOUT:' + str(status.get('orderState')) + '/' + str(status.get('paymentState')))
    run([str(DOTNET), str(COMMERCE_DLL), 'issue06-owner-worker-state', str(SOURCE_CONFIG), checkout['checkoutId'], payment['id'], scenario['id']], timeout=20)
    run([str(DOTNET), str(COMMERCE_DLL), 'issue06-owner-capabilities', str(SOURCE_CONFIG), str(artifact_path), checkout['orderId']], timeout=30)
    print('PASS synthetic browser order reached Confirmed/Succeeded; browser owns shipment, return, inspection and refund UI actions')
    return checkout['orderId']


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument('--identity-only', action='store_true', help='Run only Identity Origin/CSRF and SSR logout acceptance on the dedicated fixture')
    parser.add_argument('--ac02-only', action='store_true', help='Run only browser cart idempotency/CSRF acceptance on the dedicated fixture')
    parser.add_argument('--catalog-only', action='store_true', help='Run Issue08 Catalog Owner/publication acceptance on the dedicated fixture')
    parser.add_argument('--stock-only', action='store_true', help='Run Issue09 stock Owner/storefront acceptance on the dedicated fixture')
    parser.add_argument('--search-only', action='store_true', help='Run Issue10 public catalog search/filter browser acceptance on the dedicated fixture')
    parser.add_argument('--cart-only', action='store_true', help='Run Issue11 SKU selection/cart/quote browser acceptance on the dedicated fixture')
    args = parser.parse_args()
    if sum((args.identity_only, args.ac02_only, args.catalog_only, args.stock_only, args.search_only, args.cart_only)) > 1:
        fail('BLOCKED: acceptance modes are mutually exclusive')
    config, _project, fixture_port = inspect_fixture()
    for port in (3000, 5080):
        require_free_port(port)
    LOCAL.mkdir(mode=0o700, parents=True, exist_ok=True)
    LOG_DIR.mkdir(mode=0o700)
    runtime = {key: value for key, value in config.items() if key not in {
        'MigratorConnectionString', 'BootstrapConnectionString', 'IdentitySqlTestDedicated',
        'DevelopmentFixtureBootstrapEnabled', 'DevelopmentFixtureId',
    }}
    RUNTIME_CONFIG.write_text(json.dumps(runtime, separators=(',', ':')) + '\n')
    RUNTIME_CONFIG.chmod(0o600)
    worker = json.loads(WORKER_CONFIG.read_text())
    worker['workerConnectionString'] = config['workerConnectionString']
    RUNTIME_WORKER_CONFIG.write_text(json.dumps(worker, separators=(',', ':')) + '\n')
    RUNTIME_WORKER_CONFIG.chmod(0o600)

    env = os.environ.copy()
    env.update({
        'DOTNET_ENVIRONMENT': 'Development', 'M0_CONFIG': str(RUNTIME_CONFIG),
        'M0_CONTRACT': str(ROOT / 'docs/contracts/runtime/Core.openapi.json'),
        'M0_HTTPS_CERT': str(LOCAL / 'browser' / 'public.pem'),
        'M0_HTTPS_KEY': str(LOCAL / 'browser' / 'private.key'),
        'NO_PROXY': 'localhost,127.0.0.1,::1', 'no_proxy': 'localhost,127.0.0.1,::1',
    })
    ac02_key = 'm0ac02-' + uuid.uuid4().hex
    ac02_evidence = LOCAL / ('m0-ac02-evidence-' + uuid.uuid4().hex + '.json')
    ac02_state = LOCAL / ('m0-ac02-state-' + uuid.uuid4().hex + '.json')
    catalog_evidence = LOCAL / ('issue08-catalog-evidence-' + uuid.uuid4().hex + '.json')
    catalog_failure_evidence = LOCAL / ('issue08-catalog-failure-evidence-' + uuid.uuid4().hex + '.json')
    search_stock_state = LOCAL / ('issue10-search-stock-private-' + uuid.uuid4().hex + '.json')
    issue11_inventory_state = LOCAL / 'issue11-inventory-state-private.json'
    save_fault_active = False
    read_fault_active = False
    search_stock_active = False
    if args.cart_only and (issue11_inventory_state.exists() or issue11_inventory_state.is_symlink()):
        fail('BLOCKED: Issue11 synthetic inventory state already exists')
    if args.ac02_only:
        if any(path.exists() or path.is_symlink() for path in (ac02_evidence, ac02_state)):
            fail('BLOCKED: M0-AC02 evidence path already exists')
        env.update({
            'KOHAMI_M0_AC02_IDEMPOTENCY_KEY': ac02_key,
            'KOHAMI_M0_AC02_WRONG_TOKEN_KEY': ac02_key + '-wrong-token',
            'KOHAMI_M0_AC02_WRONG_ORIGIN_KEY': ac02_key + '-wrong-origin',
            'KOHAMI_M0_AC02_EVIDENCE_PATH': str(ac02_evidence),
        })
    children: list[subprocess.Popen] = []
    handles = []
    failed = False
    failure_stage = 'fixture'
    try:
        if args.catalog_only or args.stock_only or args.search_only or args.cart_only:
            run([str(DOTNET), str(NATIVE_DLL), 'migrate-dedicated-fixture', str(FIXTURE_ROOT), str(fixture_port)], env=env, timeout=60)
        run([str(DOTNET), str(NATIVE_DLL), 'fixture-status', str(FIXTURE_ROOT), str(fixture_port)], env=env, timeout=30)
        if args.ac02_only:
            run([str(DOTNET), str(COMMERCE_DLL), 'm0-ac02-open', str(SOURCE_CONFIG), ac02_key, str(ac02_state)], timeout=30)
        if not args.ac02_only:
            run([str(DOTNET), str(COMMERCE_DLL), 'issue06-owner-seed', str(SOURCE_CONFIG), str(ARTIFACT)], env=env, timeout=30)
            if args.catalog_only:
                run([str(DOTNET), str(COMMERCE_DLL), 'issue08-catalog-owner-grant', str(SOURCE_CONFIG), str(ARTIFACT)], env=env, timeout=30)
            if args.stock_only:
                run([str(DOTNET), str(COMMERCE_DLL), 'issue08-catalog-owner-grant', str(SOURCE_CONFIG), str(ARTIFACT)], env=env, timeout=30)
                run([str(DOTNET), str(COMMERCE_DLL), 'issue09-stock-owner-grant', str(SOURCE_CONFIG), str(ARTIFACT)], env=env, timeout=30)
            set_stage('synthetic_session')
            failure_stage = 'synthetic_session'
        worker_env = env.copy()
        worker_env['M0_CONFIG'] = str(RUNTIME_WORKER_CONFIG)
        services = [
            ('api', [str(DOTNET), str(API_DLL)], env),
            ('storefront', [str(STOREFRONT), 'dev', '--host', 'localhost', '--port', '3000'], env),
        ]
        if not args.identity_only and not args.ac02_only and not args.catalog_only and not args.search_only and not args.cart_only:
            services.insert(1, ('worker', [str(DOTNET), str(API_DLL), '--worker-only'], worker_env))
        for name, command, child_env in services:
            log = (LOG_DIR / f'{name}.log').open('w')
            os.chmod(LOG_DIR / f'{name}.log', 0o600)
            handles.append(log)
            cwd = ROOT / 'apps/storefront' if name == 'storefront' else ROOT
            children.append(subprocess.Popen(command, cwd=cwd, env=child_env, stdout=log, stderr=subprocess.STDOUT, start_new_session=True))
        wait_url('http://127.0.0.1:5080/health')
        wait_url('https://localhost:3000/', tls=True)
        if args.catalog_only:
            set_stage('catalog')
            failure_stage = 'catalog'
            browser_env = env.copy()
            browser_env.update({
                'KOHAMI_ISSUE08_SESSION_ARTIFACT': str(ARTIFACT),
                'KOHAMI_ISSUE08_EVIDENCE_PATH': str(catalog_evidence),
            })
            run([str(DOTNET), str(COMMERCE_DLL), 'issue08-catalog-fault', str(SOURCE_CONFIG), 'save-on'], timeout=30)
            save_fault_active = True
            failure_browser_env = browser_env.copy()
            failure_browser_env['KOHAMI_ISSUE08_FAILURE_EVIDENCE_PATH'] = str(catalog_failure_evidence)
            run([str(PLAYWRIGHT), 'test', '--config', str(ROOT / 'apps/storefront/playwright.config.ts'),
                 '--grep', 'Issue08 Catalog save failure', '--reporter=line', '--trace=off', '--output', str(TEST_OUTPUT)],
                env=failure_browser_env, timeout=180)
            run([str(DOTNET), str(COMMERCE_DLL), 'issue08-catalog-save-failure-verify', str(SOURCE_CONFIG), str(catalog_failure_evidence)], timeout=30)
            run([str(DOTNET), str(COMMERCE_DLL), 'issue08-catalog-fault', str(SOURCE_CONFIG), 'save-off'], timeout=30)
            save_fault_active = False
            run([str(PLAYWRIGHT), 'test', '--config', str(ROOT / 'apps/storefront/playwright.config.ts'),
                 '--grep', 'Issue08 Owner publishes', '--reporter=line', '--trace=off', '--output', str(TEST_OUTPUT)],
                env=browser_env, timeout=240)
            run([str(DOTNET), str(COMMERCE_DLL), 'issue08-catalog-fault', str(SOURCE_CONFIG), 'read-on'], timeout=30)
            read_fault_active = True
            run([str(PLAYWRIGHT), 'test', '--config', str(ROOT / 'apps/storefront/playwright.config.ts'),
                 '--grep', 'Issue08 Catalog public read failure', '--reporter=line', '--trace=off', '--output', str(TEST_OUTPUT)],
                env=browser_env, timeout=180)
            run([str(DOTNET), str(COMMERCE_DLL), 'issue08-catalog-fault', str(SOURCE_CONFIG), 'read-off'], timeout=30)
            read_fault_active = False
            run([str(PLAYWRIGHT), 'test', '--config', str(ROOT / 'apps/storefront/playwright.config.ts'),
                 '--grep', 'Issue08 Owner unpublishes after public read failure recovery probe', '--reporter=line', '--trace=off', '--output', str(TEST_OUTPUT)],
                env=browser_env, timeout=180)
            run([str(DOTNET), str(COMMERCE_DLL), 'issue08-catalog-verify', str(SOURCE_CONFIG), str(catalog_evidence)], timeout=30)
            set_stage('complete')
            failure_stage = 'complete'
            print('PASS Issue08 sanitized SQL save/read failures, atomic rollback, and Owner create/update/publish/unpublish browser/API/SQL acceptance on the dedicated synthetic fixture')
            print('PASS public product snapshot excludes private media rights references; no real product/photo rights are claimed')
        elif args.stock_only:
            set_stage('stock')
            failure_stage = 'stock'
            browser_env = env.copy()
            browser_env['KOHAMI_ISSUE09_SESSION_ARTIFACT'] = str(ARTIFACT)
            run([str(PLAYWRIGHT), 'test', '--config', str(ROOT / 'apps/storefront/playwright.config.ts'),
                 '--grep', 'Issue09 Owner adjusts sellable stock', '--reporter=line', '--trace=off', '--output', str(TEST_OUTPUT)],
                env=browser_env, timeout=240)
            set_stage('complete')
            failure_stage = 'complete'
            print('PASS Issue09 Owner stock adjustment to public out-of-stock browser journey on the dedicated synthetic fixture')
        elif args.search_only:
            run([str(DOTNET), str(COMMERCE_DLL), 'issue10-stock-zero', str(SOURCE_CONFIG), str(search_stock_state)], timeout=30)
            search_stock_active = True
            stock_state = json.loads(search_stock_state.read_text())
            set_stage('catalog_search')
            failure_stage = 'catalog_search'
            browser_env = env.copy()
            browser_env['KOHAMI_ISSUE10_OUT_OF_STOCK_SKU'] = stock_state['SkuId']
            run([str(PLAYWRIGHT), 'test', '--config', str(ROOT / 'apps/storefront/playwright.config.ts'),
                 '--grep', 'Issue10', '--reporter=line', '--trace=off', '--output', str(TEST_OUTPUT)],
                env=browser_env, timeout=240)
            set_stage('complete')
            failure_stage = 'complete'
            print('PASS Issue10 public search, same-SKU filters, pagination and mobile homepage journey on the dedicated synthetic fixture')
        elif args.cart_only:
            evidence_path = LOCAL / ('issue11-cart-evidence-' + uuid.uuid4().hex + '.jsonl')
            if evidence_path.exists() or evidence_path.is_symlink():
                fail('BLOCKED: Issue11 evidence path already exists')
            set_stage('cart')
            failure_stage = 'cart'
            browser_env = env.copy()
            browser_env['KOHAMI_ISSUE11_CART_EVIDENCE_PATH'] = str(evidence_path)
            browser_env.update({
                'KOHAMI_ISSUE11_DOTNET': str(DOTNET),
                'KOHAMI_ISSUE11_COMMERCE_DLL': str(COMMERCE_DLL),
                'KOHAMI_ISSUE11_CONFIG_PATH': str(SOURCE_CONFIG),
                'KOHAMI_ISSUE11_INVENTORY_STATE_PATH': str(issue11_inventory_state),
            })
            run([str(PLAYWRIGHT), 'test', '--config', str(ROOT / 'apps/storefront/playwright.config.ts'),
                 '--grep', 'Issue11', '--reporter=line', '--trace=off', '--output', str(TEST_OUTPUT)],
                env=browser_env, timeout=300)
            set_stage('complete')
            failure_stage = 'complete'
            print('PASS Issue11 product SKU selection, cart mutation/idempotency/ETag and server quote browser acceptance on the dedicated synthetic fixture')
        elif args.ac02_only:
            set_stage('m0_ac02')
            failure_stage = 'm0_ac02'
            run([str(PLAYWRIGHT), 'test', '--config', str(ROOT / 'apps/storefront/playwright.config.ts'),
                 '--grep', 'M0-AC02', '--reporter=line', '--trace=off', '--output', str(TEST_OUTPUT)],
                env=env, timeout=180)
            evidence = json.loads(ac02_evidence.read_text())
            cart_id = evidence.get('cartId', '')
            if not re.fullmatch(r'cart_[0-9a-f]{32}', cart_id):
                raise RuntimeError('M0_AC02_EVIDENCE_INVALID')
            env['KOHAMI_M0_AC02_EXPECTED_CART_ID'] = cart_id
            run([str(DOTNET), str(COMMERCE_DLL), 'm0-ac02-verify', str(SOURCE_CONFIG), ac02_key,
                 cart_id, env['KOHAMI_M0_AC02_WRONG_TOKEN_KEY'],
                 env['KOHAMI_M0_AC02_WRONG_ORIGIN_KEY']], timeout=30)
            set_stage('complete')
            failure_stage = 'complete'
            print('PASS M0-AC02 live browser/API/SQL cart idempotency and CSRF/Origin rejection')
        elif args.identity_only:
            set_stage('security_guards')
            ApiAcceptance(ARTIFACT).assert_security_guards()
            set_stage('browser_ui')
            failure_stage = 'browser_ui'
            browser_env = env.copy()
            browser_env['KOHAMI_ISSUE06_SESSION_ARTIFACT'] = str(ARTIFACT)
            run([str(PLAYWRIGHT), 'test', '--config', str(ROOT / 'apps/storefront/playwright.config.ts'),
                 '--grep', 'Issue06 Identity logout rejects replay', '--reporter=line', '--trace=off', '--output', str(TEST_OUTPUT)],
                env=browser_env, timeout=180)
            set_stage('complete')
            failure_stage = 'complete'
            print('PASS M0-AC08/09 Identity API guard and SSR logout acceptance on the dedicated synthetic fixture')
        else:
            set_stage('security_guards')
            ApiAcceptance(ARTIFACT).assert_security_guards()
            order_id = prepare_browser_order(ARTIFACT)
            set_stage('browser_ui')
            failure_stage = 'browser_ui'
            browser_env = env.copy()
            browser_env.update({
                'KOHAMI_ISSUE06_ORDER_ID': order_id,
                'KOHAMI_ISSUE06_SESSION_ARTIFACT': str(ARTIFACT),
                'KOHAMI_ISSUE06_SOURCE_CONFIG': str(SOURCE_CONFIG),
                'KOHAMI_ISSUE06_COMMERCE_DLL': str(COMMERCE_DLL),
                'KOHAMI_ISSUE06_DOTNET': str(DOTNET),
            })
            run([str(PLAYWRIGHT), 'test', '--config', str(ROOT / 'apps/storefront/playwright.config.ts'),
                 '--grep', 'Issue06 browser UI aftercare', '--reporter=line', '--trace=off', '--output', str(TEST_OUTPUT)],
                env=browser_env, timeout=900)
            set_stage('complete')
            failure_stage = 'complete'
            print('PASS Issue06 real storefront/API/SQL aftercare journey with synthetic pre-authenticated Owner session')
            print('PASS no login endpoint or real TOTP/MFA was used; refund reauthentication UI used a scoped synthetic SQL proof')
    except KeyboardInterrupt:
        failed = True
        print('INTERRUPTED: stopping only Issue06 acceptance services', file=sys.stderr)
    except Exception as error:
        failed = True
        failure_stage = CURRENT_STAGE
        message = str(error)
        print('ISSUE06_AFTERCARE_FAILED: ' + type(error).__name__ + (':' + message if len(message) < 120 and 'cookie' not in message.lower() else ''), file=sys.stderr)
    finally:
        for process in reversed(children):
            stop_group(process)
        for handle in handles:
            handle.close()
        try:
            if issue11_inventory_state.exists():
                run([str(DOTNET), str(COMMERCE_DLL), 'issue11-inventory-restore', str(SOURCE_CONFIG), str(issue11_inventory_state)], timeout=30)
            if search_stock_active or search_stock_state.exists():
                run([str(DOTNET), str(COMMERCE_DLL), 'issue10-stock-restore', str(SOURCE_CONFIG), str(search_stock_state)], timeout=30)
                search_stock_active = False
            if args.catalog_only:
                if read_fault_active:
                    run([str(DOTNET), str(COMMERCE_DLL), 'issue08-catalog-fault', str(SOURCE_CONFIG), 'read-off'], timeout=30)
                    read_fault_active = False
                if save_fault_active:
                    run([str(DOTNET), str(COMMERCE_DLL), 'issue08-catalog-fault', str(SOURCE_CONFIG), 'save-off'], timeout=30)
                    save_fault_active = False
            elif args.ac02_only:
                run([str(DOTNET), str(COMMERCE_DLL), 'm0-ac02-cleanup', str(SOURCE_CONFIG), ac02_key, str(ac02_state)], timeout=60)
            elif args.cart_only:
                if 'evidence_path' in locals():
                    run([str(DOTNET), str(COMMERCE_DLL), 'issue11-cart-cleanup', str(SOURCE_CONFIG), str(evidence_path)], timeout=60)
                run([str(DOTNET), str(COMMERCE_DLL), 'issue06-owner-cleanup', str(SOURCE_CONFIG), str(ARTIFACT)], timeout=60)
            else:
                run([str(DOTNET), str(COMMERCE_DLL), 'issue06-owner-cleanup', str(SOURCE_CONFIG), str(ARTIFACT)], timeout=60)
        except Exception as error:
            failed = True
            print('SYNTHETIC_FIXTURE_CLEANUP_REQUIRED: ' + type(error).__name__, file=sys.stderr)
        if args.catalog_only:
            try:
                run([str(DOTNET), str(COMMERCE_DLL), 'issue08-catalog-cleanup', str(SOURCE_CONFIG), str(catalog_evidence)], timeout=60)
                run([str(DOTNET), str(COMMERCE_DLL), 'issue06-owner-cleanup', str(SOURCE_CONFIG), str(ARTIFACT)], timeout=60)
                if catalog_evidence.exists():
                    catalog_evidence.unlink()
            except Exception as error:
                failed = True
                print('SYNTHETIC_ROW_CLEANUP_REQUIRED: ' + type(error).__name__, file=sys.stderr)
            catalog_failure_evidence.unlink(missing_ok=True)
        for path in (RUNTIME_CONFIG, RUNTIME_WORKER_CONFIG):
            path.unlink(missing_ok=True)
        if args.ac02_only:
            ac02_evidence.unlink(missing_ok=True)
            ac02_state.unlink(missing_ok=True)
        if args.cart_only and 'evidence_path' in locals():
            evidence_path.unlink(missing_ok=True)
        shutil.rmtree(LOG_DIR, ignore_errors=True)
        shutil.rmtree(TEST_OUTPUT, ignore_errors=True)
    if failed:
        print('ISSUE06_FAILURE_STAGE: ' + (failure_stage if failure_stage in ACCEPTANCE_STAGES else 'diagnostic_unavailable'), file=sys.stderr)
    return 1 if failed else 0


def run(args: list[str], *, env: dict[str, str] | None = None, timeout: int = 60) -> None:
    subprocess.run(args, cwd=ROOT, env=env, check=True, timeout=timeout)


cleanup_fixture_after_run = False
cleanup_only_synthetic_rows = True

if __name__ == '__main__':
    try:
        raise SystemExit(main())
    except Exception as error:
        print('ISSUE06_AFTERCARE_BLOCKED: ' + type(error).__name__ + (':' + str(error) if len(str(error)) < 120 else ''), file=sys.stderr)
        raise SystemExit(1)
