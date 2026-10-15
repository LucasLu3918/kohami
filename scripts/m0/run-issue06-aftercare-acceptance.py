#!/usr/bin/env python3
"""Run Issue 06 real HTTP/API/SQL aftercare using a synthetic pre-auth session."""
from __future__ import annotations

import importlib.util
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
import urllib.request
import uuid

ROOT = Path(__file__).resolve().parents[2]
LOCAL = ROOT / '.local' / 'm0'
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
    'fixture', 'synthetic_session', 'catalog', 'checkout', 'payment', 'scenario_authorization',
    'shipment', 'return_inspection', 'refund', 'browser_ui', 'complete', 'diagnostic_unavailable',
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


def inspect_fixture() -> tuple[dict, str]:
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
    return config, project


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


def assert_equal(actual, expected, code: str) -> None:
    if actual != expected:
        raise RuntimeError(code)


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
    status = api.call('GET', f'/checkouts/{checkout["checkoutId"]}/status')
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
    status = api.call('GET', f'/checkouts/{checkout["checkoutId"]}/status')
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
    config, _project = inspect_fixture()
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
    children: list[subprocess.Popen] = []
    handles = []
    failed = False
    failure_stage = 'fixture'
    try:
        run([str(DOTNET), str(NATIVE_DLL), 'fixture-status', str(ROOT)], env=env, timeout=30)
        run([str(DOTNET), str(COMMERCE_DLL), 'issue06-owner-seed', str(SOURCE_CONFIG), str(ARTIFACT)], env=env, timeout=30)
        set_stage('synthetic_session')
        failure_stage = 'synthetic_session'
        worker_env = env.copy()
        worker_env['M0_CONFIG'] = str(RUNTIME_WORKER_CONFIG)
        for name, command, child_env in (
            ('api', [str(DOTNET), str(API_DLL)], env),
            ('worker', [str(DOTNET), str(API_DLL), '--worker-only'], worker_env),
            ('storefront', [str(STOREFRONT), 'dev', '--host', 'localhost', '--port', '3000'], env),
        ):
            log = (LOG_DIR / f'{name}.log').open('w')
            os.chmod(LOG_DIR / f'{name}.log', 0o600)
            handles.append(log)
            cwd = ROOT / 'apps/storefront' if name == 'storefront' else ROOT
            children.append(subprocess.Popen(command, cwd=cwd, env=child_env, stdout=log, stderr=subprocess.STDOUT, start_new_session=True))
        wait_url('http://127.0.0.1:5080/health')
        wait_url('https://localhost:3000/', tls=True)
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
            run([str(DOTNET), str(COMMERCE_DLL), 'issue06-owner-cleanup', str(SOURCE_CONFIG), str(ARTIFACT)], timeout=60)
        except Exception as error:
            failed = True
            print('SYNTHETIC_ROW_CLEANUP_REQUIRED: ' + type(error).__name__, file=sys.stderr)
        for path in (RUNTIME_CONFIG, RUNTIME_WORKER_CONFIG):
            path.unlink(missing_ok=True)
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
