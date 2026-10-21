import importlib.util
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

SCRIPT = Path(__file__).resolve().parents[2] / "scripts/foundation/run-issue11-cart-acceptance.py"
SPEC = importlib.util.spec_from_file_location("issue11_cart", SCRIPT)
RUNNER = importlib.util.module_from_spec(SPEC)
assert SPEC and SPEC.loader
SPEC.loader.exec_module(RUNNER)


class Issue11CartRunnerTests(unittest.TestCase):
    def test_accepts_only_the_canonical_sql_test_profile(self):
        config = {
            "IdentitySqlTestDedicated": True,
            "KohamiSqlTestDedicated": True,
            "port": 14333,
            "datasetId": "kohami_local",
            "origin": "https://localhost:3000",
            "sqlServerName": "localhost",
            "sqlCertificatePath": "/private/ca.pem",
            "connectionString": "Server=localhost,14333;Database=Kohami;Encrypt=Strict;TrustServerCertificate=False;HostNameInCertificate=localhost;Server Certificate=/private/ca.pem",
            "workerConnectionString": "Server=localhost,14333;Database=Kohami;Encrypt=Strict;TrustServerCertificate=False;HostNameInCertificate=localhost;Server Certificate=/private/ca.pem",
        }
        self.assertTrue(RUNNER.valid_fixture_config(config))
        for key, value in (("KohamiSqlTestDedicated", False), ("port", 14336), ("datasetId", "demo_m0")):
            candidate = dict(config)
            candidate[key] = value
            self.assertFalse(RUNNER.valid_fixture_config(candidate), key)

    def test_rejects_non_strict_tls_or_any_noncanonical_sql_target(self):
        config = {
            "IdentitySqlTestDedicated": True, "KohamiSqlTestDedicated": True, "port": 14333,
            "datasetId": "kohami_local", "origin": "https://localhost:3000", "sqlServerName": "localhost",
            "sqlCertificatePath": "/private/ca.pem",
            "connectionString": "Server=localhost,14333;Database=Kohami;Encrypt=Strict;TrustServerCertificate=False;HostNameInCertificate=localhost;Server Certificate=/private/ca.pem",
            "workerConnectionString": "Server=localhost,14333;Database=Kohami;Encrypt=Strict;TrustServerCertificate=False;HostNameInCertificate=localhost;Server Certificate=/private/ca.pem",
        }
        for field, old, new in (
            ("connectionString", "localhost,14333", "localhost,14349"),
            ("workerConnectionString", "localhost,14333", "localhost,14336"),
            ("connectionString", "Encrypt=Strict", "Encrypt=Optional"),
        ):
            candidate = dict(config)
            candidate[field] = candidate[field].replace(old, new)
            self.assertFalse(RUNNER.valid_fixture_config(candidate))

    def test_accepts_dotnet_connection_string_keys_with_spaces(self):
        config = {
            "IdentitySqlTestDedicated": True, "KohamiSqlTestDedicated": True, "port": 14333,
            "datasetId": "kohami_local", "origin": "https://localhost:3000", "sqlServerName": "localhost",
            "sqlCertificatePath": "/private/ca.pem",
            "connectionString": "Server=localhost,14333;Database=Kohami;Encrypt=Strict;Trust Server Certificate=False;Host Name In Certificate=localhost;Server Certificate=/private/ca.pem",
            "workerConnectionString": "Server=localhost,14333;Database=Kohami;Encrypt=Strict;Trust Server Certificate=False;Host Name In Certificate=localhost;Server Certificate=/private/ca.pem",
        }
        self.assertTrue(RUNNER.valid_fixture_config(config))

    def test_fixture_preflight_fails_closed_when_maintenance_is_required(self):
        marker = "PASS Issue11 SQL target is marker-verified canonical kohami-sql-test"
        with self.assertRaisesRegex(RuntimeError, "MAINTENANCE_REQUIRED"):
            RUNNER.validate_fixture_preflight(f"{marker}\\nISSUE11_MAINTENANCE_REQUIRED\\n")
        RUNNER.validate_fixture_preflight(f"{marker}\\nISSUE11_MAINTENANCE_DISABLED\\n")
        with self.assertRaisesRegex(RuntimeError, "ISSUE11_FIXTURE_PREFLIGHT_INVALID"):
            RUNNER.validate_fixture_preflight(marker)

    def test_loopback_ports_are_range_checked_and_must_be_free(self):
        self.assertEqual(RUNNER.local_port("KOHAMI_API_PORT", 5080, 5080, 5099, {}), 5080)
        for value in ("5079", "5100", "abc", "5081;echo unsafe"):
            with self.subTest(value=value), self.assertRaisesRegex(RuntimeError, "ISSUE11_LOCAL_PORT_INVALID"):
                RUNNER.local_port("KOHAMI_API_PORT", 5080, 5080, 5099, {"KOHAMI_API_PORT": value})

    def test_runner_uses_only_isolated_api_and_site_ports(self):
        self.assertEqual((RUNNER.API_PORT, RUNNER.SITE_PORT), (5081, 3001))
        with patch.object(RUNNER, "require_free_port") as require_free_port:
            self.assertEqual(RUNNER.select_port("KOHAMI_ISSUE11_API_PORT", 5081, 5081, 5081, {}), 5081)
            self.assertEqual(RUNNER.select_port("KOHAMI_ISSUE11_SITE_PORT", 3001, 3001, 3001, {}), 3001)
        self.assertEqual([call.args[0] for call in require_free_port.call_args_list], [5081, 3001])
        with self.assertRaisesRegex(RuntimeError, "ISSUE11_LOCAL_PORT_INVALID"):
            RUNNER.local_port("KOHAMI_ISSUE11_API_PORT", 5081, 5081, 5081, {"KOHAMI_ISSUE11_API_PORT": "5083"})

    def test_port_selection_skips_busy_default_without_signaling_existing_process(self):
        def reserve_only_second(port):
            if port == 3000:
                raise RuntimeError("ISSUE11_LOCAL_PORT_IN_USE:3000")

        with patch.object(RUNNER, "require_free_port", side_effect=reserve_only_second):
            self.assertEqual(RUNNER.select_port("SITE", 3000, 3000, 3002, {}), 3001)

    def test_evidence_and_state_paths_are_unique_private_runner_files(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            evidence = RUNNER.create_private_file(root / "evidence.jsonl")
            state = RUNNER.create_private_file(root / "state.json")
            self.assertEqual(evidence.stat().st_mode & 0o777, 0o600)
            self.assertEqual(state.stat().st_mode & 0o777, 0o600)
            with self.assertRaisesRegex(RuntimeError, "ISSUE11_PRIVATE_PATH_EXISTS"):
                RUNNER.create_private_file(state)


if __name__ == "__main__":
    unittest.main()
