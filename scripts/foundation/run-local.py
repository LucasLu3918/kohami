"""Run the local mock API and HTTPS Nuxt shell against the selected local profile."""
from contextlib import contextmanager
import argparse
from pathlib import Path
import json
import errno
import os
import signal
import socket
import subprocess
import tempfile
from environment import load_profile


def resolve_local_dir(root: Path, environ: dict[str, str]) -> Path:
    configured = environ.get("KOHAMI_LOCAL_DIR")
    expected = root / ".local" / "kohami-sql" / "identity"
    if configured and Path(configured).expanduser().resolve() != expected.resolve():
        raise RuntimeError("LOCAL_RUNNER_REQUIRES_KOHAMI_SQL_PROFILE")
    return expected


def configured_port(environ: dict[str, str], name: str, default: int, minimum: int, maximum: int) -> int:
    value = environ.get(name)
    if value is None:
        return default
    try:
        port = int(value)
    except ValueError as error:
        raise ValueError(f"{name}_INVALID") from error
    if not minimum <= port <= maximum:
        raise ValueError(f"{name}_INVALID")
    return port


def configure_loopback_proxy_bypass(environ: dict[str, str]) -> None:
    """Keep local API traffic out of inherited system HTTP proxies."""
    exclusions: list[str] = []
    for key in ("NO_PROXY", "no_proxy"):
        for item in environ.get(key, "").split(","):
            value = item.strip()
            if value and value.casefold() not in {entry.casefold() for entry in exclusions}:
                exclusions.append(value)
    for host in ("127.0.0.1", "localhost", "::1"):
        if host.casefold() not in {entry.casefold() for entry in exclusions}:
            exclusions.append(host)
    value = ",".join(exclusions)
    environ["NO_PROXY"] = value
    environ["no_proxy"] = value


def require_free_loopback_port(port: int) -> None:
    with socket.socket() as listener:
        listener.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        try:
            listener.bind(("127.0.0.1", port))
        except OSError as error:
            if error.errno == errno.EADDRINUSE:
                raise RuntimeError(f"LOCAL_PORT_IN_USE:{port}") from error
            reason = errno.errorcode.get(error.errno, "SOCKET_ERROR")
            raise RuntimeError(f"LOCAL_PORT_CHECK_BLOCKED:{port}:{reason}") from error


def lsof_process_matches(output: str, root: Path, expected_cwd: Path, marker: str) -> bool:
    """Confirm a listener belongs to this checkout and expected service files."""
    lines = output.splitlines()
    cwd = None
    paths: list[str] = []
    for index, line in enumerate(lines):
        if line == "fcwd" and index + 1 < len(lines) and lines[index + 1].startswith("n"):
            cwd = lines[index + 1][1:]
        elif line.startswith("n"):
            paths.append(line[1:])

    if cwd is None or Path(cwd).resolve() != expected_cwd.resolve():
        return False

    marker_path = str((root / marker.rstrip("/")).resolve())
    if marker.endswith("/"):
        return any(path.startswith(marker_path + os.sep) for path in paths)
    return marker_path in paths


def project_service_is_listening(port: int, root: Path, expected_cwd: Path, marker: str) -> bool:
    """Identify an existing listener without stopping or signaling its process."""
    listeners = subprocess.run(
        ["lsof", "-nP", "-t", f"-iTCP:{port}", "-sTCP:LISTEN"],
        capture_output=True,
        text=True,
        check=False,
    )
    if listeners.returncode != 0:
        return False
    for pid in listeners.stdout.splitlines():
        details = subprocess.run(
            ["lsof", "-a", "-p", pid, "-Fn"],
            capture_output=True,
            text=True,
            check=False,
        )
        if details.returncode == 0 and lsof_process_matches(details.stdout, root, expected_cwd, marker):
            return True
    return False


@contextmanager
def preview_runtime_config(source: Path, site_port: int):
    """Create an ephemeral private config for the selected local preview origin."""
    payload = json.loads(source.read_text())
    payload["origin"] = f"https://localhost:{site_port}"
    descriptor, name = tempfile.mkstemp(prefix="runtime-preview-", suffix=".json", dir=source.parent)
    preview = Path(name)
    try:
        os.fchmod(descriptor, 0o600)
        with os.fdopen(descriptor, "w") as stream:
            json.dump(payload, stream, separators=(",", ":"))
            stream.write("\n")
        yield preview
    finally:
        preview.unlink(missing_ok=True)


def main() -> int:
    parser = argparse.ArgumentParser(description="啟動本機 KOHAMI API 與 Storefront")
    parser.add_argument("--with-worker", action="store_true", help="同時啟動本機 worker")
    arguments = parser.parse_args()
    root = Path(__file__).resolve().parents[2]
    environ = os.environ.copy()
    try:
        profile = load_profile(root)
        environ.update(profile)
    except (OSError, RuntimeError, ValueError) as error:
        raise SystemExit(f"BLOCKED: {error}") from error
    if environ.get("KOHAMI_ENVIRONMENT") != "local":
        raise SystemExit("BLOCKED: this runner serves only the local profile")
    local = resolve_local_dir(root, environ)
    try:
        site_port = configured_port(environ, "KOHAMI_SITE_PORT", 3003, 3000, 3999)
        api_port = configured_port(environ, "KOHAMI_API_PORT", 5083, 5080, 5099)
        if site_port == 3000 or site_port == 3001:
            raise ValueError("KOHAMI_SITE_PORT_RESERVED")
        worker_enabled = arguments.with_worker or environ.get("KOHAMI_WORKER_ENABLED", "false").lower() == "true"
        api_running = project_service_is_listening(
            api_port, root, root, "apps/api/bin/Release/net10.0/Api.dll"
        )
        site_running = project_service_is_listening(
            site_port, root, root / "apps/storefront", "apps/storefront/app/"
        )
        if api_running and site_running:
            if worker_enabled:
                raise RuntimeError("LOCAL_SERVICES_ALREADY_RUNNING_WITH_WORKER_REQUESTED")
            print(
                f"Local development already running: https://localhost:{site_port} — "
                f"API http://127.0.0.1:{api_port}",
                flush=True,
            )
            return 0
        require_free_loopback_port(api_port)
        require_free_loopback_port(site_port)
    except (RuntimeError, ValueError) as error:
        raise SystemExit(f"BLOCKED: {error}") from error

    required = ["runtime-private.json", "browser/public.pem", "browser/private.key"]
    if any(not (local / item).is_file() for item in required):
        raise SystemExit(f"BLOCKED: required private local material is missing under {local}")

    api = root / "apps/api/bin/Release/net10.0/Api.dll"
    if not api.is_file():
        raise SystemExit("BLOCKED: build API Release first")
    worker = root / "apps/worker/bin/Release/net10.0/Worker.dll"
    worker_config = local / "worker-private.json"
    if worker_enabled and (not worker.is_file() or not worker_config.is_file()):
        raise SystemExit("BLOCKED: worker Release and private worker profile are required")

    env = environ.copy()
    configure_loopback_proxy_bypass(env)
    env.update(
        KOHAMI_CONTRACT=str(root / "docs/contracts/runtime/Core.openapi.json"),
        Foundation_HTTPS_CERT=str(local / "browser/public.pem"),
        Foundation_HTTPS_KEY=str(local / "browser/private.key"),
        KOHAMI_API_PORT=str(api_port),
        NUXT_API_ORIGIN=f"http://127.0.0.1:{api_port}",
        NUXT_PORT=str(site_port),
        NUXT_SITE_ORIGIN=f"https://localhost:{site_port}",
        NUXT_BUILD_DIR=f"/private/tmp/kohami-storefront-{site_port}",
        NUXT_PUBLIC_LOCAL_DEMO="true",
    )
    children = []

    def stop(*_args):
        for process in children:
            if process.poll() is None:
                process.terminate()

    for sig in (signal.SIGINT, signal.SIGTERM):
        signal.signal(sig, stop)

    try:
        with preview_runtime_config(local / "runtime-private.json", site_port) as preview_config:
            env["KOHAMI_CONFIG"] = str(preview_config)
            children.append(subprocess.Popen(["/usr/local/share/dotnet/dotnet", str(api)], cwd=root, env=env))
            if worker_enabled:
                worker_env = env.copy()
                worker_env["KOHAMI_CONFIG"] = str(worker_config)
                children.append(subprocess.Popen(["/usr/local/share/dotnet/dotnet", str(worker)], cwd=root, env=worker_env))
            children.append(subprocess.Popen(["npm", "run", "dev"], cwd=root / "apps/storefront", env=env))
            activated = (local / "local-development-credentials.json").is_file()
            state = "local synthetic credentials available; verify /health and /ready" if activated else "run local-development-bootstrap to enable checkout writes"
            print(f"Local development: https://localhost:{site_port} — API http://127.0.0.1:{api_port}; {state}", flush=True)
            while all(process.poll() is None for process in children):
                try:
                    children[0].wait(timeout=1)
                except subprocess.TimeoutExpired:
                    pass
    finally:
        stop()
        for process in children:
            try:
                process.wait(timeout=5)
            except subprocess.TimeoutExpired:
                process.kill()
    return next((process.returncode for process in children if process.returncode), 0)


if __name__ == "__main__":
    raise SystemExit(main())
