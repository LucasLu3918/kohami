import importlib.util
from pathlib import Path
import tempfile
import unittest

SCRIPT = Path(__file__).resolve().parents[2] / "scripts/m0/run-issue12-checkout-acceptance.py"
SPEC = importlib.util.spec_from_file_location("issue12_checkout", SCRIPT)
RUNNER = importlib.util.module_from_spec(SPEC)
assert SPEC and SPEC.loader
SPEC.loader.exec_module(RUNNER)


class Issue12CheckoutAcceptanceTests(unittest.TestCase):
    def test_accepts_only_dedicated_localhost_strict_tls_fixture(self):
        with tempfile.NamedTemporaryFile() as certificate:
            connection = (
                "Server=localhost,14336;Database=KohamiM0;Encrypt=Strict;TrustServerCertificate=False;"
                "HostNameInCertificate=localhost;Server Certificate=" + certificate.name
            )
            config = {
                "IdentitySqlTestDedicated": True,
                "datasetId": "demo_m0",
                "origin": "https://localhost:3000",
                "sqlServerName": "localhost",
                "sqlCertificatePath": certificate.name,
                "connectionString": connection,
                "workerConnectionString": connection,
            }
            self.assertTrue(RUNNER.valid_fixture_config(config))
            config["IdentitySqlTestDedicated"] = False
            self.assertFalse(RUNNER.valid_fixture_config(config))

    def test_expected_error_requires_exact_status_and_code(self):
        self.assertTrue(RUNNER.matches_error(409, {"code": "STOCK_CHANGED"}, 409, "STOCK_CHANGED"))
        self.assertFalse(RUNNER.matches_error(403, {"code": "STOCK_CHANGED"}, 409, "STOCK_CHANGED"))
        self.assertFalse(RUNNER.matches_error(409, {"code": "QUOTE_CHANGED"}, 409, "STOCK_CHANGED"))

    def test_checkout_payload_requires_confirmation_and_synthetic_email(self):
        payload = RUNNER.checkout_payload("quote_" + "a" * 32, "v1", RUNNER.synthetic_email("buyer"), "mock_home", "demo_supported")
        self.assertTrue(payload["confirmed"])
        self.assertEqual(payload["quoteId"], "quote_" + "a" * 32)
        self.assertEqual(payload["contact"]["email"], "buyer@" + RUNNER.SYNTHETIC_EMAIL_DOMAIN)
        self.assertEqual(payload["delivery"], {"method": "mock_home", "region": "demo_supported"})

    def test_api_client_records_idempotency_key_before_request(self):
        self.assertIn("Idempotency-Key", RUNNER.MUTATION_HEADERS)
        self.assertTrue(RUNNER.cleanup_only_recorded_ids)
        self.assertFalse(RUNNER.runs_fixture_migrations)

if __name__ == "__main__":
    unittest.main()
