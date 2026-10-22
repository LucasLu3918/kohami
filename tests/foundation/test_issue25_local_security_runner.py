import importlib.util
import inspect
import io
from email.message import Message
from pathlib import Path
import tempfile
import sys
import unittest
from unittest.mock import Mock, patch
from urllib.error import HTTPError

SCRIPT = Path(__file__).resolve().parents[2] / "scripts" / "foundation" / "run-issue25-local-security-acceptance.py"
assert SCRIPT.is_file(), f"Issue25 acceptance runner not found: {SCRIPT}"
SPEC = importlib.util.spec_from_file_location("issue25_local_security", SCRIPT)
ACCEPTANCE = importlib.util.module_from_spec(SPEC)
assert SPEC and SPEC.loader
sys.modules[SPEC.name] = ACCEPTANCE
SPEC.loader.exec_module(ACCEPTANCE)


class Issue25LocalSecurityRunnerTests(unittest.TestCase):
    def test_result_check_count_includes_authenticated_matrix_only_when_enabled(self):
        origin = "https://localhost:3000"
        self.assertEqual(ACCEPTANCE.expected_check_count(origin, False), 11)
        self.assertEqual(ACCEPTANCE.expected_check_count(origin, True), 16)

    def test_approved_maintenance_window_opens_before_commerce_http_cases(self):
        source = inspect.getsource(ACCEPTANCE.run_acceptance)
        self.assertLess(
            source.index('run_maintenance_helper(config_path, "open", maintenance_state_path)'),
            source.index("for case in acceptance_plan(config[\"origin\"])")
        )

    def test_revoked_session_sql_readback_runs_before_exact_cleanup(self):
        source = inspect.getsource(ACCEPTANCE.run_acceptance)
        self.assertIn("authenticated.logout_synthetic_session()", source)
        self.assertLess(source.index('run_commerce_helper("verify-revoked", config_path)'),
                        source.index('run_commerce_helper("verify", config_path)'))
        self.assertIn('"verify-revoked"', inspect.getsource(ACCEPTANCE.run_commerce_helper))

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

    def test_authenticated_non_owner_plan_is_explicitly_scoped_and_denies_three_http_operations(self):
        actor = {
            "role": "Merchandiser",
            "permission": "stock.adjust",
            "scopeKind": "Sku",
            "scopeId": "issue25-allowed-sku",
            "deniedSku": "issue25-other-sku",
            "productSlug": "issue25-" + "a" * 32,
            "catalogSku": "issue25-" + "b" * 32,
            "idempotencyKey": "issue25-key-" + "c" * 32,
        }
        cases = ACCEPTANCE.authenticated_non_owner_plan(actor, "https://localhost:3000", "private-csrf")
        self.assertEqual([(case.method, case.path, case.expected_status, case.expected_code) for case in cases], [
            ("GET", "/api/v1/admin/stock/issue25-other-sku", 403, "PERMISSION_CHANGED"),
            ("POST", "/api/v1/admin/stock-adjustments", 403, "PERMISSION_CHANGED"),
            ("POST", "/api/v1/admin/products", 403, "PERMISSION_CHANGED"),
        ])
        self.assertEqual(cases[1].body, {
            "skuId": "issue25-other-sku", "quantityDelta": 1,
            "reason": "Issue25 synthetic authorization denial",
        })
        for case in cases[1:]:
            self.assertEqual(case.headers["Origin"], "https://localhost:3000")
            self.assertEqual(case.headers["X-CSRF-TOKEN"], "private-csrf")
        self.assertEqual(cases[2].body["slug"], actor["productSlug"])
        self.assertEqual(cases[2].body["skus"][0]["id"], actor["catalogSku"])

    def test_authenticated_non_owner_plan_refuses_owner_and_bad_scope_inputs(self):
        cases = [
            {"role": "Owner", "permission": "stock.adjust", "scopeKind": "Sku",
             "scopeId": "sku-1", "deniedSku": "sku-2", "productSlug": "x",
             "catalogSku": "y", "idempotencyKey": "z"},
            {"role": "Merchandiser", "permission": "catalog.read", "scopeKind": "Dataset",
             "scopeId": "kohami_local", "deniedSku": "sku-2", "productSlug": "x",
             "catalogSku": "y", "idempotencyKey": "z"},
            {"role": "Merchandiser", "permission": "stock.adjust", "scopeKind": "Dataset",
             "scopeId": "kohami_local", "deniedSku": "sku-2", "productSlug": "x",
             "catalogSku": "y", "idempotencyKey": "z"},
        ]
        for actor in cases:
            with self.subTest(actor=actor):
                with self.assertRaisesRegex(RuntimeError, "NON_OWNER_SCOPE_INVALID"):
                    ACCEPTANCE.authenticated_non_owner_plan(actor, "https://localhost:3000", "private-csrf")

    def test_authenticated_logout_plan_revokes_session_then_denies_same_stock_route(self):
        cases = ACCEPTANCE.session_revocation_plan(
            "issue25-denied-sku", "https://localhost:3000", "private-csrf"
        )
        self.assertEqual(
            [(case.method, case.path, case.expected_status, case.expected_code) for case in cases],
            [
                ("DELETE", "/api/v1/auth/session", 204, None),
                ("GET", "/api/v1/admin/stock/issue25-denied-sku", 401, "AUTHENTICATION_REQUIRED"),
            ],
        )
        self.assertEqual(cases[0].headers["Origin"], "https://localhost:3000")
        self.assertEqual(cases[0].headers["X-CSRF-TOKEN"], "private-csrf")

    def test_logout_requires_api_to_clear_session_cookie_before_replay(self):
        client = ACCEPTANCE.ApiClient("http://localhost:5080", "https://localhost:3000")
        client.cookies = {"kohami.session": "private-session", "kohami.csrf": "private-csrf"}
        client.csrf_token = "private-csrf"
        headers = Message()
        headers.add_header("Set-Cookie", "kohami.session=; Path=/; Secure; HttpOnly")
        opened = Mock()
        opened.status = 204
        opened.headers = headers
        opened.read.return_value = b""
        opened.__enter__ = Mock(return_value=opened)
        opened.__exit__ = Mock(return_value=False)
        with patch.object(ACCEPTANCE.urllib.request, "urlopen", return_value=opened) as urlopen:
            status, payload = client.logout_synthetic_session()
        self.assertEqual((status, payload), (204, {}))
        self.assertEqual(client.cookies.get("kohami.session"), "")
        request = urlopen.call_args.args[0]
        self.assertEqual(request.get_method(), "DELETE")
        self.assertEqual(request.get_header("Origin"), "https://localhost:3000")
        self.assertEqual(request.get_header("X-csrf-token"), "private-csrf")
        self.assertIn("kohami.session=private-session", request.get_header("Cookie"))

    def test_authenticated_non_owner_login_bootstraps_and_requires_api_session_cookie(self):
        client = ACCEPTANCE.ApiClient("http://localhost:5080", "https://localhost:3000")
        csrf_response = Message()
        csrf_response.add_header("Set-Cookie", "kohami.csrf=csrf-cookie; Path=/; Secure; HttpOnly")
        csrf_response.add_header("Set-Cookie", "kohami.browser=browser-cookie; Path=/; Secure; HttpOnly")
        csrf_response.add_header("Set-Cookie", "kohami.command=command-cookie; Path=/; Secure; HttpOnly")
        csrf_response.add_header("Content-Type", "application/json")
        login_response = Message()
        login_response.add_header("Set-Cookie", "kohami.session=issued-session; Path=/; Secure; HttpOnly")
        login_response.add_header("Content-Type", "application/json")
        opened = Mock()
        opened.__enter__ = Mock(return_value=opened)
        opened.__exit__ = Mock(return_value=False)
        opened.headers = csrf_response
        opened.status = 200
        opened.read.return_value = b'{"requestToken":"issued-csrf"}'
        login_opened = Mock()
        login_opened.__enter__ = Mock(return_value=login_opened)
        login_opened.__exit__ = Mock(return_value=False)
        login_opened.headers = login_response
        login_opened.status = 200
        login_opened.read.return_value = b'{"state":"AdminAuthenticated","permissions":["stock.adjust"]}'
        with patch.object(ACCEPTANCE.urllib.request, "urlopen", side_effect=[opened, login_opened]) as urlopen:
            session = client.login_synthetic_non_owner("issue25@example.test", "private-password")
        self.assertEqual(session["state"], "AdminAuthenticated")
        self.assertIn("stock.adjust", session["permissions"])
        self.assertIn("kohami.session", client.cookies)
        self.assertEqual(client.csrf_token, "issued-csrf")
        self.assertEqual(urlopen.call_count, 2)
        login_request = urlopen.call_args_list[1].args[0]
        self.assertEqual(login_request.get_header("Origin"), "https://localhost:3000")
        self.assertEqual(login_request.get_header("X-csrf-token"), "issued-csrf")

    def test_runtime_config_requires_mock_loopback_dedicated_tls_target_without_exposing_secrets(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory).resolve()
            (root / ".git").mkdir()
            profile_dir = root / ".local" / "kohami-sql-test" / "identity"
            profile_dir.mkdir(parents=True)
            certificate = root / "server.pem"
            ca = root / "ca.pem"
            ring = root / "ring"
            session_certificate = root / "session.pfx"
            for path in (certificate, ca, session_certificate):
                path.write_text("synthetic")
            ring.mkdir()
            (ring / "key-test.xml").write_text("synthetic")
            config = {
                "datasetId": "kohami_local", "origin": "https://localhost:3000", "sqlServerName": "localhost",
                "identityNamespace": "kohami.identity_access.v1", "port": 14333,
                "KohamiSqlTestDedicated": True,
                "connectionString": f"Server=localhost,14333;Database=Kohami;Encrypt=Strict;TrustServerCertificate=False;HostNameInCertificate=localhost;Server Certificate={certificate};Password=do-not-print",
                "sqlCertificatePath": str(certificate), "sqlCaPath": str(ca), "sessionRingPath": str(ring),
                "sessionCertificatePath": str(session_certificate), "sessionCertificatePassword": "do-not-print",
            }
            path = profile_dir / "fixture-private.json"
            ACCEPTANCE.write_config(path, config)
            loaded = ACCEPTANCE.inspect_runtime_config(path)
            self.assertEqual(loaded["origin"], "https://localhost:3000")
            config["connectionString"] = config["connectionString"].replace("localhost,14333", "remote,14333")
            ACCEPTANCE.write_config(path, config)
            with self.assertRaisesRegex(RuntimeError, "CONFIG_SCOPE_INVALID") as raised:
                ACCEPTANCE.inspect_runtime_config(path)
            self.assertNotIn("do-not-print", str(raised.exception))
            config["connectionString"] = config["connectionString"].replace("remote,14333", "localhost,14333")
            config["KohamiSqlTestDedicated"] = False
            ACCEPTANCE.write_config(path, config)
            with self.assertRaisesRegex(RuntimeError, "CONFIG_SCOPE_INVALID"):
                ACCEPTANCE.inspect_runtime_config(path)
            config["KohamiSqlTestDedicated"] = True
            config["port"] = 14349
            ACCEPTANCE.write_config(path, config)
            with self.assertRaisesRegex(RuntimeError, "CONFIG_SCOPE_INVALID"):
                ACCEPTANCE.inspect_runtime_config(path)
            config["port"] = 14333
            config["connectionString"] = config["connectionString"].replace("Encrypt=Strict", "Encrypt=Optional")
            ACCEPTANCE.write_config(path, config)
            with self.assertRaisesRegex(RuntimeError, "CONFIG_SCOPE_INVALID"):
                ACCEPTANCE.inspect_runtime_config(path)
            old_path = profile_dir / "runtime-private.json"
            ACCEPTANCE.write_config(old_path, config)
            with self.assertRaisesRegex(RuntimeError, "CONFIG_PATH_INVALID"):
                ACCEPTANCE.inspect_runtime_config(old_path)

    def test_runtime_config_accepts_only_the_marker_verified_sql_test_profile(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory).resolve()
            (root / ".git").mkdir()
            profile_dir = root / ".local" / "kohami-sql-test" / "identity"
            profile_dir.mkdir(parents=True)
            certificate = root / "server.pem"
            ca = root / "ca.pem"
            ring = root / "ring"
            session_certificate = root / "session.pfx"
            for path in (certificate, ca, session_certificate):
                path.write_text("synthetic")
            ring.mkdir()
            (ring / "key-test.xml").write_text("synthetic")
            config = {
                "datasetId": "kohami_local", "origin": "https://localhost:3000", "sqlServerName": "localhost",
                "identityNamespace": "kohami.identity_access.v1", "port": 14333,
                "KohamiSqlTestDedicated": True,
                "connectionString": f"Server=localhost,14333;Database=Kohami;Encrypt=Strict;TrustServerCertificate=False;HostNameInCertificate=localhost;Server Certificate={certificate};Password=do-not-print",
                "sqlCertificatePath": str(certificate), "sqlCaPath": str(ca), "sessionRingPath": str(ring),
                "sessionCertificatePath": str(session_certificate), "sessionCertificatePassword": "do-not-print",
            }
            path = profile_dir / "fixture-private.json"
            ACCEPTANCE.write_config(path, config)
            loaded = ACCEPTANCE.inspect_runtime_config(path)
            self.assertEqual(loaded["port"], 14333)
            self.assertIs(loaded["KohamiSqlTestDedicated"], True)

    def test_marker_verification_uses_read_only_native_fixture_status_and_fails_closed(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory).resolve()
            (root / ".git").mkdir()
            profile_dir = root / ".local" / "kohami-sql-test" / "identity"
            profile_dir.mkdir(parents=True)
            native = root / "tools" / "native-operations" / "bin" / "Release" / "net10.0" / "Native.dll"
            native.parent.mkdir(parents=True)
            native.write_text("synthetic")
            dotnet = root / "dotnet"
            dotnet.write_text("synthetic")
            config_path = profile_dir / "fixture-private.json"
            config_path.write_text("synthetic")
            with patch.object(ACCEPTANCE, "DOTNET", dotnet), patch.object(ACCEPTANCE.subprocess, "run") as run:
                ACCEPTANCE.verify_fixture_marker(config_path)
            command = run.call_args.args[0]
            self.assertEqual(command[1:4], [str(native), "fixture-status", str(root)])
            self.assertEqual(command[4], "14333")
            self.assertEqual(command[-2:], ["--local-dir", str(profile_dir)])
            self.assertEqual(run.call_args.kwargs["stdout"], ACCEPTANCE.subprocess.DEVNULL)
            self.assertEqual(run.call_args.kwargs["stderr"], ACCEPTANCE.subprocess.DEVNULL)
            with patch.object(ACCEPTANCE, "DOTNET", dotnet), patch.object(
                ACCEPTANCE.subprocess, "run", side_effect=ACCEPTANCE.subprocess.CalledProcessError(1, "fixture-status")
            ):
                with self.assertRaisesRegex(RuntimeError, "DEDICATED_MARKER_UNVERIFIED"):
                    ACCEPTANCE.verify_fixture_marker(config_path)

    def test_api_port_picker_uses_only_free_isolated_range(self):
        with patch.object(ACCEPTANCE, "port_is_free", side_effect=lambda port: port == 5087):
            self.assertEqual(ACCEPTANCE.choose_api_port(), 5087)
        with patch.object(ACCEPTANCE, "port_is_free", return_value=False):
            with self.assertRaisesRegex(RuntimeError, "API_PORT_UNAVAILABLE"):
                ACCEPTANCE.choose_api_port()


if __name__ == "__main__":
    unittest.main()
