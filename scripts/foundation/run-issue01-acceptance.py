#!/usr/bin/env python3
"""Run the Issue 01 browser checkout acceptance against only the dedicated 14336 fixture."""
from __future__ import annotations

import json
import os
from pathlib import Path
import signal
import socket
import ssl
import subprocess
import sys
import time
import urllib.request

ROOT = Path(__file__).resolve().parents[2]
DOTNET = Path('/usr/local/share/dotnet/dotnet')
LOCAL = ROOT / '.local' / 'foundation'
SOURCE_CONFIG = LOCAL / 'fixture-private.json'
RUNTIME_CONFIG = LOCAL / 'issue01-runtime-private.json'
EVIDENCE = LOCAL / 'issue01-browser-evidence.json'
LOG_DIR = LOCAL / 'issue01-logs'
API_DLL = ROOT / 'apps' / 'api' / 'bin' / 'Release' / 'net10.0' / 'Api.dll'
HARNESS_DLL = ROOT / 'tests' / 'Commerce' / 'bin' / 'Release' / 'net10.0' / 'Commerce.Tests.dll'
PLAYWRIGHT = ROOT / 'apps' / 'storefront' / 'node_modules' / '.bin' / 'playwright'


def fail(message: str) -> None:
    raise RuntimeError(message)


def require_fixture() -> dict:
    if not DOTNET.is_file() or not SOURCE_CONFIG.is_file():
        fail('BLOCKED: local .NET runtime or dedicated fixture config is missing')
    if SOURCE_CONFIG.is_symlink() or (SOURCE_CONFIG.stat().st_mode & 0o077):
        fail('BLOCKED: fixture config must be a private, non-symlink file')
    config = json.loads(SOURCE_CONFIG.read_text())
    if config.get('IdentitySqlTestDedicated') is not True or config.get('datasetId') != 'kohami_local':
        fail('BLOCKED: dedicated synthetic fixture marker is missing')
    for key in ('connectionString', 'workerConnectionString', 'MigratorConnectionString'):
        connection = config.get(key, '').lower().replace(' ', '')
        if 'localhost,14336' not in connection or 'initialcatalog=kohami' not in connection:
            fail('BLOCKED: every fixture principal must target localhost:14336/Kohami')
    if config.get('origin') != 'https://localhost:3000' or config.get('sqlServerName') != 'localhost':
        fail('BLOCKED: fixture origin or SQL TLS identity does not match Issue 01')
    if not API_DLL.is_file() or not PLAYWRIGHT.is_file():
        fail('BLOCKED: build the API and install locked storefront dependencies first')
    return config


def require_free_port(port: int) -> None:
    with socket.socket() as listener:
        listener.settimeout(0.25)
        if listener.connect_ex(('127.0.0.1', port)) == 0:
            fail(f'BLOCKED: localhost port {port} is already in use; no process was stopped')


def wait_url(url: str, *, tls: bool = False, timeout: float = 60) -> None:
    opener = urllib.request.build_opener(urllib.request.ProxyHandler({}), urllib.request.HTTPSHandler(context=ssl._create_unverified_context()) if tls else urllib.request.HTTPHandler())
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        try:
            with opener.open(url, timeout=2) as response:
                if response.status == 200:
                    return
        except Exception:
            time.sleep(0.5)
    fail(f'BLOCKED: local service did not become ready: {url.split(":")[0]}')


def fixture_window(action: str, window_id: str | None = None) -> str:
    env = os.environ.copy()
    env['KOHAMI_TEST_CONFIG'] = str(SOURCE_CONFIG)
    args = [str(DOTNET), str(HARNESS_DLL), 'fixture-window', action, str(ROOT)]
    if window_id:
        args.append(window_id)
    result = subprocess.run(args, cwd=ROOT, env=env, text=True, capture_output=True, timeout=30)
    if result.returncode:
        fail(f'FIXTURE_WINDOW_{action.upper()}_FAILED: see test runner diagnostics (exit {result.returncode})')
    return result.stdout.strip()


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


def main() -> int:
    config = require_fixture()
    for port in (3000, 5080):
        require_free_port(port)
    LOCAL.mkdir(mode=0o700, parents=True, exist_ok=True)
    LOG_DIR.mkdir(mode=0o700, exist_ok=True)
    for path in (LOCAL, LOG_DIR):
        path.chmod(0o700)

    # Runtime config contains only the API and worker principals, never the fixture admin.
    runtime = {key: value for key, value in config.items() if key not in ('MigratorConnectionString', 'IdentitySqlTestDedicated')}
    RUNTIME_CONFIG.write_text(json.dumps(runtime, separators=(',', ':')) + '\n')
    RUNTIME_CONFIG.chmod(0o600)
    EVIDENCE.unlink(missing_ok=True)

    env = os.environ.copy()
    env.update({
        'KOHAMI_CONFIG': str(RUNTIME_CONFIG),
        'KOHAMI_CONTRACT': str(ROOT / 'docs' / 'contracts' / 'runtime' / 'Core.openapi.json'),
        'Foundation_HTTPS_CERT': str(LOCAL / 'browser' / 'public.pem'),
        'Foundation_HTTPS_KEY': str(LOCAL / 'browser' / 'private.key'),
        'KOHAMI_ISSUE01_EVIDENCE_PATH': str(EVIDENCE),
        'NO_PROXY': 'localhost,127.0.0.1,::1',
        'no_proxy': 'localhost,127.0.0.1,::1',
    })
    children: list[subprocess.Popen] = []
    handles = []
    window_id: str | None = None
    failed = False
    gate_restored = True
    try:
        commands = [
            [str(DOTNET), str(API_DLL)],
            [str(DOTNET), str(API_DLL), '--worker-only'],
            [str(PLAYWRIGHT.parent / 'nuxt'), 'dev', '--host', 'localhost', '--port', '3000'],
        ]
        for name, command in zip(('api', 'worker', 'storefront'), commands, strict=True):
            log = (LOG_DIR / f'{name}.log').open('w')
            os.chmod(LOG_DIR / f'{name}.log', 0o600)
            handles.append(log)
            children.append(subprocess.Popen(command, cwd=ROOT if name != 'storefront' else ROOT / 'apps' / 'storefront', env=env, stdout=log, stderr=subprocess.STDOUT, start_new_session=True))
        wait_url('http://127.0.0.1:5080/health')
        wait_url('https://localhost:3000/', tls=True)

        gate_restored = False
        opened = fixture_window('open')
        marker = 'ISSUE01_FIXTURE_WINDOW_OPEN '
        if not opened.startswith(marker):
            fail('FIXTURE_WINDOW_OPEN_DID_NOT_RETURN_A_WINDOW_ID')
        window_id = opened.removeprefix(marker).strip()
        if len(window_id) != 36:
            fail('FIXTURE_WINDOW_ID_INVALID')
        env['KOHAMI_ISSUE01_WINDOW_ID'] = window_id

        result = subprocess.run([str(PLAYWRIGHT), 'test', '--config', str(ROOT / 'apps' / 'storefront' / 'playwright.config.ts')], cwd=ROOT / 'apps' / 'storefront', env=env, timeout=540)
        if result.returncode:
            failed = True
            fail(f'PLAYWRIGHT_ACCEPTANCE_FAILED: exit {result.returncode}')
        if not EVIDENCE.is_file():
            failed = True
            fail('PLAYWRIGHT_EVIDENCE_MISSING')
        print('PASS: Issue 01 browser checkout and unknown-payment recovery')
        print('Evidence saved under ignored .local/foundation/issue01-browser-evidence.json')
    except KeyboardInterrupt:
        failed = True
        print('INTERRUPTED: restoring the fixture maintenance gate', file=sys.stderr)
    except Exception as error:
        failed = True
        print(str(error), file=sys.stderr)
    finally:
        if window_id:
            try:
                print(fixture_window('close', window_id))
                gate_restored = True
            except Exception as error:
                failed = True
                print(f'CRITICAL: fixture gate restoration failed: {error}', file=sys.stderr)
        for process in reversed(children):
            stop_group(process)
        for handle in handles:
            handle.close()
        if gate_restored:
            RUNTIME_CONFIG.unlink(missing_ok=True)
    return 1 if failed else 0


if __name__ == '__main__':
    raise SystemExit(main())
