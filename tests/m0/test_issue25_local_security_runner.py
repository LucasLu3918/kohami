import importlib.util
import io
from email.message import Message
from pathlib import Path
import tempfile
import sys
import unittest
from unittest.mock import Mock, patch
from urllib.error import HTTPError

SCRIPT = Path(__file__).resolve().parents[2] / "scripts" / "m0" / "run-issue25-local-security-acceptance.py"
SPEC = importlib.util.spec_from_file_location("issue25_local_security", SCRIPT)
ACCEPTANCE = importlib.util.module_from_spec(SPEC)
assert SPEC and SPEC.loader
sys.modules[SPEC.name] = ACCEPTANCE
SPEC.loader.exec_module(ACCEPTANCE)


class Issue25LocalSecurityRunnerTests(unittest.TestCase):
    def test_plan_contains_only_read_denials_and_middleware_rejected_posts(self):
        plan = ACCEPTANCE.acceptance_plan("https://localhost:3000")
        self.assertEqual([item.method for item in plan if item.method == "GET"], [
            "GET", "GET", "GET", "GET", "GET", "GET", "GET",
        ])
        self.assertTrue(all(item.method == "GET" for item in plan))
        self.assertEqual(sum(item.method == "POST" for item in plan), 0)

    def test_client_adds_forwarded_https_on_every_request(self):
        client = ACCEPTANCE.ApiClient("http://localhost:5080", "https://localhost:3000")
        response = Message()
        response.add_header("Set-Cookie", "kohami.csrf=private-cookie; Path=/; Secure; HttpOnly")
        response.add_header("Set-Cookie", "kohami.browser=private-browser; Path=/; Secure; HttpOnly")
        response.add_header("Set-Cookie", "kohami.command=private-command; Path=/; Secure; HttpOnly")
        response.add_header("Set-Cookie", "__Host-RequestVerificationToken=private-antiforgery; Path=/; Secure; HttpOnly")
        response.add_header("Content-Type", "application/json")
        response_body = b'{"requestToken":"private-request-token"}'
        opened = Mock()
        opened.status = 200
        opened.headers = response
        opened.read.return_value = response_body
        opened.__enter__ = Mock(return_value=opened)
        opened.__exit__ = Mock(return_value=False)
        with patch.object(ACCEPTANCE.urllib.request, "urlopen", return_value=opened) as urlopen:
            client.request("GET", "/api/v1/auth/csrf")
        request = urlopen.call_args.args[0]
        self.assertEqual(request.get_header("X-forwarded-proto"), "https")

    def test_csrf_plan_uses_api_issued_cookie_and_bad_tokens_without_logging_them(self):
        client = ACCEPTANCE.ApiClient("http://localhost:5080", "https://localhost:3000")
        api = client
        api.cookies = {"kohami.csrf": "private-cookie"}
        api.csrf_token = "private-request-token"
        requests = api.security_requests()
        self.assertEqual(len(requests), 4)
        self.assertEqual(requests[0].headers.get("Origin"), None)
        self.assertEqual(requests[1].headers["Origin"], "https://invalid.example")
        self.assertEqual(requests[2].headers["Origin"], "https://localhost:3000")
        self.assertNotIn("X-CSRF-TOKEN", requests[2].headers)
        self.assertEqual(requests[3].headers["X-CSRF-TOKEN"], "invalid-issue25-token")
        self.assertEqual(api._cookie_header(), "kohami.csrf=private-cookie")

    def test_runtime_config_requires_mock_loopback_dedicated_tls_target_without_exposing_secrets(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            certificate = root / "server.pem"
            ca = root / "ca.pem"
            ring = root / "ring"
            session_certificate = root / "session.pfx"
            for path in (certificate, ca, session_certificate):
                path.write_text("synthetic")
            ring.mkdir()
            (ring / "key-test.xml").write_text("synthetic")
            config = {
                "datasetId": "demo_m0", "origin": "https://localhost:3000", "sqlServerName": "localhost",
                "identityNamespace": "kohami.m0.v1",
                "connectionString": f"Server=localhost,14349;Database=KohamiM0;Encrypt=Strict;TrustServerCertificate=False;HostNameInCertificate=localhost;Server Certificate={certificate};Password=do-not-print",
                "sqlCertificatePath": str(certificate), "sqlCaPath": str(ca), "sessionRingPath": str(ring),
                "sessionCertificatePath": str(session_certificate), "sessionCertificatePassword": "do-not-print",
            }
            path = root / "runtime-private.json"
            ACCEPTANCE.write_config(path, config)
            loaded = ACCEPTANCE.inspect_runtime_config(path)
            self.assertEqual(loaded["origin"], "https://localhost:3000")
            config["connectionString"] = config["connectionString"].replace("localhost,14349", "remote,14349")
            ACCEPTANCE.write_config(path, config)
            with self.assertRaisesRegex(RuntimeError, "CONFIG_SCOPE_INVALID") as raised:
                ACCEPTANCE.inspect_runtime_config(path)
            self.assertNotIn("do-not-print", str(raised.exception))

    def test_api_port_picker_uses_only_free_isolated_range(self):
        with patch.object(ACCEPTANCE, "port_is_free", side_effect=lambda port: port == 5087):
            self.assertEqual(ACCEPTANCE.choose_api_port(), 5087)
        with patch.object(ACCEPTANCE, "port_is_free", return_value=False):
            with self.assertRaisesRegex(RuntimeError, "API_PORT_UNAVAILABLE"):
                ACCEPTANCE.choose_api_port()


if __name__ == "__main__":
    unittest.main()
