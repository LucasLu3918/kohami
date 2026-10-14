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

    def test_hidden_prompt_socket_returns_input_without_echoing_it(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'prompt.sock'
            server = ACCEPTANCE.HiddenPromptServer(str(path), prompt_reader=lambda prompt: 'synthetic-only')
            thread = threading.Thread(target=server.serve_forever, daemon=True)
            thread.start()
            try:
                self.assertEqual(stat.S_IMODE(path.stat().st_mode), 0o600)
                with socket.socket(socket.AF_UNIX, socket.SOCK_STREAM) as client:
                    client.connect(str(path))
                    client.sendall(b'{"prompt":"hidden prompt"}\n')
                    response = b''
                    while not response.endswith(b'\n'):
                        response += client.recv(4096)
                self.assertEqual(json.loads(response), {'ok': True, 'value': 'synthetic-only'})
            finally:
                server.shutdown()
                server.server_close()
                thread.join(timeout=2)


if __name__ == "__main__":
    unittest.main()
