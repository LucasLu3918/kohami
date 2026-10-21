#!/usr/bin/env python3
"""Run the P5 synthetic-member browser checkout on a marker-verified local demo fixture."""
from __future__ import annotations

import argparse
import importlib.util
import os
from pathlib import Path
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]
DOTNET = Path("/usr/local/share/dotnet/dotnet")
COMMERCE_DLL = ROOT / "tests/Commerce/bin/Release/net10.0/Commerce.Tests.dll"
HTTP_RUNNER = ROOT / "scripts/m0/run-local-demo-wallet-http-acceptance.py"


def load_http_runner():
    spec = importlib.util.spec_from_file_location("local_demo_wallet_http_runner", HTTP_RUNNER)
    if spec is None or spec.loader is None:
        raise RuntimeError("LOCAL_DEMO_HTTP_RUNNER_UNAVAILABLE")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def validate_site_port(port: int) -> int:
    if type(port) is not int or not 3002 <= port <= 3999:
        raise RuntimeError("LOCAL_DEMO_SITE_PORT_INVALID")
    return port


def run(local_dir: Path, api_port: int, site_port: int) -> int:
    http_runner = load_http_runner()
    local = http_runner.validate_profile(ROOT, local_dir)
    api_port = http_runner.validate_api_port(api_port)
    site_port = validate_site_port(site_port)
    if not DOTNET.is_file() or not COMMERCE_DLL.is_file():
        print("LOCAL_DEMO_BROWSER_ACCEPTANCE_RELEASE_BUILD_REQUIRED", file=sys.stderr)
        return 2

    env = os.environ.copy()
    env["KOHAMI_M0_LOCAL_DIR"] = str(local)
    env["KOHAMI_M0_FIXTURE_PURPOSE"] = "local-demo"
    env["KOHAMI_M0_TEST_CONFIG"] = str(local / "fixture-private.json")
    port = __import__("json").loads((local / "fixture-private.json").read_text())["port"]
    verify = subprocess.run(
        [str(DOTNET), str(ROOT / "tools/native-operations/bin/Release/net10.0/Native.dll"),
         "verify-local-demo-catalog", str(ROOT), str(port), "--local-dir", str(local)],
        cwd=ROOT, env=env, check=False,
    )
    if verify.returncode:
        return verify.returncode
    return subprocess.run(
        [str(DOTNET), str(COMMERCE_DLL), "--local-demo-wallet-browser", str(ROOT), str(local), str(api_port), str(site_port)],
        cwd=ROOT, env=env, check=False,
    ).returncode


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--local-dir", required=True, type=Path)
    parser.add_argument("--api-port", type=int, default=5088)
    parser.add_argument("--site-port", type=int, default=3225)
    args = parser.parse_args()
    try:
        return run(args.local_dir, args.api_port, args.site_port)
    except RuntimeError as error:
        print(str(error), file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
