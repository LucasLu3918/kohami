#!/usr/bin/env python3
"""Live P1 smoke: SQL readiness, API catalog, trilingual SSR, stop and restart."""
from pathlib import Path
import html
import json
import signal
import ssl
import subprocess
import sys
import tempfile
import time
import urllib.error
import urllib.request

ROOT = Path(__file__).resolve().parents[2]
RUNNER = ROOT / "scripts/m0/run-kohami-demo.py"


def fetch(url: str, *, cafile: Path | None = None, timeout: float = 2) -> tuple[int, bytes]:
    context = ssl.create_default_context(cafile=str(cafile)) if cafile else None
    try:
        with urllib.request.urlopen(url, context=context, timeout=timeout) as response:
            return response.status, response.read()
    except urllib.error.HTTPError as error:
        return error.code, error.read()


def wait_ready(process: subprocess.Popen, api_port: int, site_port: int, log: Path) -> None:
    deadline = time.monotonic() + 90
    while time.monotonic() < deadline:
        if process.poll() is not None:
            raise RuntimeError("DEMO_SERVICE_EXITED:" + log.read_text(errors="replace")[-800:])
        if "DEMO_P1_READY" in log.read_text(errors="replace"):
            return
        time.sleep(0.25)
    raise RuntimeError("DEMO_SERVICE_START_TIMEOUT")


def verify_catalog(api_port: int, site_port: int, ca: Path) -> None:
    status, body = fetch(f"http://127.0.0.1:{api_port}/api/v1/products?locale=zh-TW")
    payload = json.loads(body)
    if status != 200 or len(payload.get("items", [])) != 45:
        raise RuntimeError("DEMO_API_PRODUCT_CATALOG_INVALID")
    translations = payload["items"][0].get("translations", [])
    by_locale = {item.get("locale"): item.get("name") for item in translations}
    if set(by_locale) != {"zh-TW", "ja", "en"}:
        raise RuntimeError("DEMO_API_TRILINGUAL_PRODUCT_INVALID")
    for locale in ("zh-TW", "ja", "en"):
        page_status, page = fetch(f"https://localhost:{site_port}/{locale}", cafile=ca, timeout=12)
        document = html.unescape(page.decode("utf-8"))
        expected = "lang=\"" + locale + "\""
        if page_status != 200 or expected not in document or (by_locale[locale] and by_locale[locale] not in document):
            raise RuntimeError("DEMO_SSR_LOCALE_OR_PRODUCT_INVALID:" + locale)
    print("PASS live API returned all 45 SKUs and three translations; HTTPS SSR rendered each locale through the fixture CA")


def start(api_port: int, site_port: int, log: Path) -> subprocess.Popen:
    stream = log.open("w")
    process = subprocess.Popen(
        [sys.executable, str(RUNNER), "--api-port", str(api_port), "--site-port", str(site_port)],
        cwd=ROOT,
        stdout=stream,
        stderr=subprocess.STDOUT,
    )
    # Keep the stream open until the child exits; it is stored on the process for cleanup.
    process._demo_log_stream = stream
    return process


def stop(process: subprocess.Popen) -> None:
    if process.poll() is None:
        process.send_signal(signal.SIGTERM)
        try:
            process.wait(timeout=15)
        except subprocess.TimeoutExpired:
            process.kill()
            process.wait(timeout=5)
    process._demo_log_stream.close()


def main() -> int:
    api_port, site_port = 5087, 3004
    local = ROOT / ".local/kohami-demo/environment"
    ca = local / "ca/public.pem"
    for cycle in (1, 2):
        with tempfile.TemporaryDirectory(prefix="kohami-demo-p1-") as temporary:
            log = Path(temporary) / "service.log"
            process = start(api_port, site_port, log)
            try:
                wait_ready(process, api_port, site_port, log)
                verify_catalog(api_port, site_port, ca)
                print(f"PASS demo environment start cycle {cycle}")
            finally:
                stop(process)
        time.sleep(0.5)
        try:
            fetch(f"http://127.0.0.1:{api_port}/health", timeout=1)
        except (OSError, TimeoutError):
            pass
        else:
            raise RuntimeError("DEMO_API_REMAINS_RUNNING_AFTER_STOP")
        print(f"PASS demo environment stop cycle {cycle}")
    print("PASS demo P1 stop/restart lifecycle, API-to-SQL catalog and trilingual HTTPS SSR")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as error:
        print("DEMO_P1_ACCEPTANCE_FAILED:" + str(error).replace(" ", "_"), file=sys.stderr)
        raise SystemExit(1)
