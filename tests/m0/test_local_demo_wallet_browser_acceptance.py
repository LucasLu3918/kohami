import importlib.util
from pathlib import Path
import unittest

SCRIPT = Path(__file__).resolve().parents[2] / "scripts/m0/run-local-demo-wallet-browser-acceptance.py"
SPEC = importlib.util.spec_from_file_location("local_demo_wallet_browser_runner", SCRIPT)
RUNNER = importlib.util.module_from_spec(SPEC)
assert SPEC and SPEC.loader
SPEC.loader.exec_module(RUNNER)


class LocalDemoWalletBrowserRunnerTests(unittest.TestCase):
    def test_accepts_only_unreserved_loopback_storefront_ports(self):
        self.assertEqual(RUNNER.validate_site_port(3225), 3225)
        for port in (3000, 3001, 3002 - 1, 4000, 0, -1, "3225"):
            with self.subTest(port=port):
                self.assertRaises(RuntimeError, RUNNER.validate_site_port, port)

    def test_reuses_http_profile_and_api_port_guard(self):
        http_runner = RUNNER.load_http_runner()
        self.assertEqual(http_runner.validate_api_port(5088), 5088)
        for port in (5079, 5100, 0, -1, "5088"):
            with self.subTest(port=port):
                self.assertRaises(RuntimeError, http_runner.validate_api_port, port)


if __name__ == "__main__":
    unittest.main()
