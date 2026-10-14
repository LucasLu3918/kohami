#!/usr/bin/env python3
"""Run real storefront/API aftercare acceptance, then remove this disposable fixture."""
from __future__ import annotations

import json
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
import urllib.request

ROOT = Path(__file__).resolve().parents[2]
DOTNET = Path('/usr/local/share/dotnet/dotnet')
LOCAL = ROOT / '.local' / 'm0'
SOURCE_CONFIG = LOCAL / 'fixture-private.json'
WORKER_CONFIG = LOCAL / 'worker-private.json'
RUNTIME_CONFIG = LOCAL / 'issue02-runtime-private.json'
RUNTIME_WORKER_CONFIG = LOCAL / 'issue02-worker-private.json'
LOG_DIR = LOCAL / 'issue02-logs'
API_DLL = ROOT / 'apps' / 'api' / 'bin' / 'Release' / 'net10.0' / 'Api.dll'
NATIVE_DLL = ROOT / 'tools' / 'native-operations' / 'bin' / 'Release' / 'net10.0' / 'Native.dll'
PLAYWRIGHT = ROOT / 'apps' / 'storefront' / 'node_modules' / '.bin' / 'playwright'
COMPOSE = ROOT / 'infra' / 'm0' / 'compose.yaml'


def fail(message: str) -> None:
    raise RuntimeError(message)


def run(args: list[str], *, env: dict[str, str] | None = None, timeout: int = 60) -> None:
    subprocess.run(args, cwd=ROOT, env=env, check=True, timeout=timeout)


def parse_connection_string(value: str) -> dict[str, str]:
    """Parse SQL client pairs, preserving semicolons inside braced values."""
    parts: list[str] = []
    current: list[str] = []
    in_braces = False
    index = 0
    while index < len(value):
        char = value[index]
        if char == '{':
            in_braces = True
            current.append(char)
        elif char == '}' and in_braces:
            if index + 1 < len(value) and value[index + 1] == '}':
                current.extend(('}', '}'))
                index += 1
            else:
                in_braces = False
                current.append(char)
        elif char == ';' and not in_braces:
            parts.append(''.join(current))
            current.clear()
        else:
            current.append(char)
        index += 1
    if in_braces:
        return {}
    parts.append(''.join(current))

    aliases = {
        'datasource': 'server', 'server': 'server', 'address': 'server', 'addr': 'server',
        'networkaddress': 'server', 'initialcatalog': 'database', 'database': 'database',
        'encrypt': 'encrypt', 'trustservercertificate': 'trustservercertificate',
        'hostnameincertificate': 'hostnameincertificate', 'servercertificate': 'servercertificate',
    }
    fields: dict[str, str] = {}
    for part in parts:
        key, separator, item = part.partition('=')
        canonical = ''.join(key.casefold().split())
        if not separator or canonical not in aliases:
            continue
        item = item.strip()
        if len(item) >= 2 and item[0] == '{' and item[-1] == '}':
            item = item[1:-1].replace('}}', '}')
        fields[aliases[canonical]] = item.strip()
    return fields


def connection_targets_fixture(connection: str, expected_certificate: str) -> bool:
    fields = parse_connection_string(connection)
    expected = Path(expected_certificate)
    try:
        pin_matches = (
            expected.is_file()
            and Path(fields.get('servercertificate', '')).resolve(strict=True) == expected.resolve(strict=True)
        )
    except (OSError, RuntimeError):
        pin_matches = False
    return (
        fields.get('server', '').casefold() == 'localhost,14336'
        and fields.get('database', '').casefold() == 'kohamim0'
        and fields.get('encrypt', '').casefold() == 'strict'
        and fields.get('trustservercertificate', '').casefold() == 'false'
        and fields.get('hostnameincertificate', '').casefold() == 'localhost'
        and pin_matches
    )


def require_free_port(port: int) -> None:
    with socket.socket() as listener:
        listener.settimeout(0.25)
        if listener.connect_ex(('127.0.0.1', port)) == 0:
            fail(f'BLOCKED: localhost port {port} is in use; no process was stopped')


def wait_url(url: str, *, tls: bool = False, timeout: float = 60) -> None:
    opener = urllib.request.build_opener(
        urllib.request.ProxyHandler({}),
        urllib.request.HTTPSHandler(context=ssl._create_unverified_context()) if tls else urllib.request.HTTPHandler(),
    )
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        try:
            with opener.open(url, timeout=2) as response:
                if response.status == 200:
                    return
        except Exception:
            time.sleep(0.5)
    fail(f'BLOCKED: local service did not become ready: {url.split(":")[0]}')


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
    if not DOTNET.is_file() or not NATIVE_DLL.is_file() or not SOURCE_CONFIG.is_file() or not WORKER_CONFIG.is_file():
        fail('BLOCKED: Issue 02 disposable fixture or native verifier is missing')
    for path in (SOURCE_CONFIG, WORKER_CONFIG):
        if path.is_symlink() or path.stat().st_mode & 0o077:
            fail('BLOCKED: private fixture config must be mode 0600 and not a symlink')
    project_path = LOCAL / 'issue02-compose-project'
    if project_path.is_symlink() or not project_path.is_file() or project_path.stat().st_mode & 0o077:
        fail('BLOCKED: owned compose project marker is missing or unsafe')
    project = project_path.read_text().strip()
    if not re.fullmatch(r'kohami-issue02-[0-9a-f]{12}', project):
        fail('BLOCKED: compose project marker is invalid')

    config = json.loads(SOURCE_CONFIG.read_text())
    if config.get('IdentitySqlTestDedicated') is not True or config.get('DevelopmentFixtureBootstrapEnabled') is not True or config.get('datasetId') != 'demo_m0':
        fail('BLOCKED: dedicated development fixture marker is missing')
    certificate = config.get('sqlCertificatePath', '')
    connection = config.get('connectionString', '')
    if not connection_targets_fixture(connection, certificate):
        fail('BLOCKED: runtime connection is outside the pinned localhost fixture')
    if config.get('origin') != 'https://localhost:3000' or config.get('sqlServerName') != 'localhost':
        fail('BLOCKED: fixture origin or SQL TLS identity is invalid')
    worker = json.loads(WORKER_CONFIG.read_text())
    worker_connection = config.get('workerConnectionString', '')
    if not connection_targets_fixture(worker_connection, certificate):
        fail('BLOCKED: worker connection is outside the dedicated SQL fixture')
    if not all(path.is_file() for path in (API_DLL, PLAYWRIGHT)):
        fail('BLOCKED: build API Release and install locked storefront dependencies first')

    env = os.environ.copy()
    env['DOTNET_ENVIRONMENT'] = 'Development'
    run([str(DOTNET), str(NATIVE_DLL), 'development-fixture-bootstrap-verify', str(SOURCE_CONFIG)], env=env)
    return config, project


def main() -> int:
    config, project = inspect_fixture()
    for port in (3000, 5080):
        require_free_port(port)

    LOCAL.mkdir(mode=0o700, parents=True, exist_ok=True)
    LOG_DIR.mkdir(mode=0o700, exist_ok=True)
    for path in (LOCAL, LOG_DIR):
        path.chmod(0o700)
    stripped = (
        'MigratorConnectionString', 'BootstrapConnectionString', 'IdentitySqlTestDedicated',
        'DevelopmentFixtureBootstrapEnabled', 'DevelopmentFixtureId',
    )
    runtime = {key: value for key, value in config.items() if key not in stripped}
    RUNTIME_CONFIG.write_text(json.dumps(runtime, separators=(',', ':')) + '\n')
    RUNTIME_CONFIG.chmod(0o600)
    worker['workerConnectionString'] = worker_connection
    RUNTIME_WORKER_CONFIG.write_text(json.dumps(worker, separators=(',', ':')) + '\n')
    RUNTIME_WORKER_CONFIG.chmod(0o600)

    env = os.environ.copy()
    env.update({
        'DOTNET_ENVIRONMENT': 'Development',
        'M0_CONFIG': str(RUNTIME_CONFIG),
        'M0_CONTRACT': str(ROOT / 'docs' / 'm0' / 'Core.openapi.json'),
        'M0_HTTPS_CERT': str(LOCAL / 'browser' / 'public.pem'),
        'M0_HTTPS_KEY': str(LOCAL / 'browser' / 'private.key'),
        'NO_PROXY': 'localhost,127.0.0.1,::1',
        'no_proxy': 'localhost,127.0.0.1,::1',
    })
    worker_env = env.copy()
    worker_env['M0_CONFIG'] = str(RUNTIME_WORKER_CONFIG)
    children: list[subprocess.Popen] = []
    handles = []
    failed = False
    fixture_removed = False
    try:
        commands = (
            ('api', [str(DOTNET), str(API_DLL)], env),
            ('worker', [str(DOTNET), str(API_DLL), '--worker-only'], worker_env),
            ('storefront', [str(PLAYWRIGHT.parent / 'nuxt'), 'dev', '--host', 'localhost', '--port', '3000'], env),
        )
        for name, command, child_env in commands:
            log = (LOG_DIR / f'{name}.log').open('w')
            os.chmod(LOG_DIR / f'{name}.log', 0o600)
            handles.append(log)
            cwd = ROOT / 'apps' / 'storefront' if name == 'storefront' else ROOT
            children.append(subprocess.Popen(command, cwd=cwd, env=child_env, stdout=log, stderr=subprocess.STDOUT, start_new_session=True))
        wait_url('http://127.0.0.1:5080/health')
        wait_url('https://localhost:3000/', tls=True)

        # The prompt is read with echo disabled inside Playwright. Trace, video and screenshot are off.
        subprocess.run([
            str(PLAYWRIGHT), 'test', '--config', str(ROOT / 'apps' / 'storefront' / 'playwright.config.ts'),
            '--grep', 'real API aftercare journey', '--reporter=line', '--trace=off',
        ], cwd=ROOT / 'apps' / 'storefront', env=env, check=True, timeout=900)
        print('PASS Issue 02 real storefront/API aftercare journey, authentic TOTP, SQL-backed refund request and DEMO receipt policy')
        print('PASS synthetic Owner permissions and closed fixture bootstrap verified before and after journey')
    except KeyboardInterrupt:
        failed = True
        print('INTERRUPTED: stopping only services started by this acceptance runner', file=sys.stderr)
    except subprocess.CalledProcessError as error:
        failed = True
        print(f'ISSUE02_ACCEPTANCE_FAILED: command exit {error.returncode}', file=sys.stderr)
    except Exception as error:
        failed = True
        print('ISSUE02_ACCEPTANCE_FAILED: ' + type(error).__name__ + ': ' + str(error), file=sys.stderr)
    finally:
        for process in reversed(children):
            stop_group(process)
        for handle in handles:
            handle.close()
        try:
            env = os.environ.copy()
            env['M0_SQL_PORT'] = '14336'
            subprocess.run(['docker', 'compose', '-p', project, '-f', str(COMPOSE), 'down', '--volumes'], cwd=ROOT, env=env, check=True, timeout=120)
            shutil.rmtree(LOCAL)
            fixture_removed = True
        except Exception as error:
            failed = True
            print('FIXTURE_TEARDOWN_REQUIRED: owned SQL project or private fixture remains; ' + type(error).__name__, file=sys.stderr)
    if fixture_removed:
        print('PASS disposable SQL volumes and private fixture materials removed')
    return 1 if failed else 0


if __name__ == '__main__':
    try:
        raise SystemExit(main())
    except Exception as error:
        print('ISSUE02_ACCEPTANCE_BLOCKED: ' + type(error).__name__ + ': ' + str(error), file=sys.stderr)
        raise SystemExit(1)
