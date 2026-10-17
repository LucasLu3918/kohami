import importlib.util
import json
from pathlib import Path
import socket
import stat
import tempfile
import threading
import unittest

SCRIPT = Path(__file__).resolve().parents[2] / "scripts" / "m0" / "run-issue02-acceptance.py"
SPEC = importlib.util.spec_from_file_location("issue02_acceptance", SCRIPT)
ACCEPTANCE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(ACCEPTANCE)


class SqlConnectionTargetTests(unittest.TestCase):
    def test_failure_stage_accepts_only_fixed_diagnostic_labels(self):
        self.assertEqual(ACCEPTANCE.safe_failure_stage("login_submit"), "login_submit")
        self.assertEqual(ACCEPTANCE.safe_failure_stage("login_csrf_failed"), "login_csrf_failed")
        self.assertEqual(ACCEPTANCE.safe_failure_stage("login_session_request"), "login_session_request")
        self.assertEqual(ACCEPTANCE.safe_failure_stage("challenge-id-secret"), "diagnostic_unavailable")
        self.assertEqual(ACCEPTANCE.safe_failure_stage("login_submit\nsecret"), "diagnostic_unavailable")

    def test_accepts_runtime_builder_aliases_and_pinned_cert(self):
        with tempfile.NamedTemporaryFile() as certificate:
            value = (
                "Data Source=localhost,14336;Initial Catalog=KohamiM0;"
                "Encrypt=Strict;Trust Server Certificate=False;"
                "Host Name In Certificate=localhost;Server Certificate={" + certificate.name + "}"
            )
            self.assertTrue(ACCEPTANCE.connection_targets_fixture(value, certificate.name))

    def test_accepts_worker_builder_aliases_and_pinned_cert(self):
        with tempfile.NamedTemporaryFile() as certificate:
            value = (
                "Server=localhost,14336;Database=KohamiM0;Encrypt=Strict;"
                "TrustServerCertificate=False;HostNameInCertificate=localhost;"
                "Server Certificate=" + certificate.name
            )
            self.assertTrue(ACCEPTANCE.connection_targets_fixture(value, certificate.name))

    def test_rejects_remote_or_unpinned_connections(self):
        with tempfile.NamedTemporaryFile() as certificate:
            valid = (
                "Server=localhost,14336;Database=KohamiM0;Encrypt=Strict;"
                "TrustServerCertificate=False;HostNameInCertificate=localhost;"
                "Server Certificate=" + certificate.name
            )
            self.assertFalse(ACCEPTANCE.connection_targets_fixture(valid.replace("localhost,14336", "remote,14336"), certificate.name))
            self.assertFalse(ACCEPTANCE.connection_targets_fixture(valid.replace("Strict", "Optional"), certificate.name))
            self.assertFalse(ACCEPTANCE.connection_targets_fixture(valid.replace(certificate.name, "/tmp/other.pem"), certificate.name))

    def test_parses_semicolon_inside_braced_value(self):
        fields = ACCEPTANCE.parse_connection_string("Password={one;two}}three};Database=KohamiM0")
        self.assertEqual(fields['database'], 'KohamiM0')
        self.assertNotIn('password', fields)

    def test_worker_runtime_uses_validated_pinned_fixture_connection(self):
        worker = {'workerConnectionString': 'untrusted placeholder', 'other': 'preserved'}
        fixture = {'workerConnectionString': 'Server=localhost,14336;Database=KohamiM0'}
        runtime = ACCEPTANCE.worker_runtime_config(worker, fixture)
        self.assertEqual(runtime['workerConnectionString'], fixture['workerConnectionString'])
        self.assertEqual(runtime['other'], 'preserved')
        self.assertEqual(worker['workerConnectionString'], 'untrusted placeholder')

    def test_private_fixture_value_response_is_scoped_and_unlogged(self):
        accepted = ACCEPTANCE.private_value_response(b'{"name":"issue02OwnerPassword"}\n', 'synthetic-only')
        rejected = ACCEPTANCE.private_value_response(b'{"name":"databasePassword"}\n', 'synthetic-only')
        self.assertEqual(json.loads(accepted), {'ok': True, 'value': 'synthetic-only'})
        self.assertEqual(json.loads(rejected), {'ok': False})


if __name__ == "__main__":
    unittest.main()
