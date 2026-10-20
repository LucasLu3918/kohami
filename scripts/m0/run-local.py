"""Run the local mock API and HTTPS Nuxt shell; no provisioning or Owner bypass."""
from contextlib import contextmanager
from pathlib import Path
import json
import errno
import os
import signal
import socket
import subprocess
import tempfile


def resolve_local_dir(root: Path, environ: dict[str, str]) -> Path:
    configured = environ.get("KOHAMI_M0_LOCAL_DIR")
    return Path(configured).expanduser() if configured else root / ".local" / "kohami-sql" / "m0"


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
    root = Path(__file__).resolve().parents[2]
    environ = os.environ.copy()
    local = resolve_local_dir(root, environ)
    try:
        site_port = configured_port(environ, "KOHAMI_SITE_PORT", 3003, 3000, 3999)
        api_port = configured_port(environ, "KOHAMI_API_PORT", 5083, 5080, 5099)
        if site_port == 3000 or site_port == 3001:
            raise ValueError("KOHAMI_SITE_PORT_RESERVED")
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

    env = environ.copy()
    env.update(
        M0_CONTRACT=str(root / "docs/contracts/runtime/Core.openapi.json"),
        M0_HTTPS_CERT=str(local / "browser/public.pem"),
        M0_HTTPS_KEY=str(local / "browser/private.key"),
        KOHAMI_API_PORT=str(api_port),
        NUXT_API_ORIGIN=f"http://127.0.0.1:{api_port}",
        NUXT_PORT=str(site_port),
        NUXT_SITE_ORIGIN=f"https://localhost:{site_port}",
        NUXT_BUILD_DIR=f"/private/tmp/kohami-storefront-{site_port}",
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
            env["M0_CONFIG"] = str(preview_config)
            children.append(subprocess.Popen(["/usr/local/share/dotnet/dotnet", str(api)], cwd=root, env=env))
            children.append(subprocess.Popen(["npm", "run", "dev"], cwd=root / "apps/storefront", env=env))
            print(
                f"Local mock: https://localhost:{site_port} — API http://127.0.0.1:{api_port}; "
                "native activation remains required for protected writes",
                flush=True,
            )
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
