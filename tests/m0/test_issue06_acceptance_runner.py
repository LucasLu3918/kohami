import importlib.util
import io
from email.message import Message
from pathlib import Path
import tempfile
import unittest
from unittest.mock import Mock, patch
from urllib.error import HTTPError

SCRIPT = Path(__file__).resolve().parents[2] / "scripts/m0/run-issue06-aftercare-acceptance.py"
SPEC = importlib.util.spec_from_file_location("issue06_aftercare", SCRIPT)
ACCEPTANCE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(ACCEPTANCE)


class Issue06AftercareRunnerTests(unittest.TestCase):
    def test_checkout_scope_assertion_checks_owner_and_another_signed_browser(self):
        api = Mock()
        api.call.return_value = {"version": "synthetic-version"}
        other_browser = Mock()
        artifact = Path("/private/tmp/session-artifact.json")

        with patch.object(ACCEPTANCE, "ApiAcceptance", return_value=other_browser) as create_api:
            status = ACCEPTANCE.assert_checkout_owner_scope(api, artifact, "checkout-synthetic")

        self.assertEqual(status, {"version": "synthetic-version"})
        api.call.assert_called_once_with("GET", "/checkouts/checkout-synthetic/status")
        create_api.assert_called_once_with(artifact)
        other_browser._raw.assert_called_once_with("GET", "/auth/csrf")
        other_browser.expect_error.assert_called_once_with(
            "GET", "/checkouts/checkout-synthetic/status", 404, "RESOURCE_NOT_FOUND",
        )

    def test_checkout_ssr_assertion_checks_owner_summary_and_other_actor_unavailable(self):
        status = {
            "checkoutId": "checkout-synthetic",
            "snapshot": {"totalTwd": 560},
        }
        owner_api = Mock()
        owner_api._cookie_header.return_value = "kohami.session=owner; kohami.browser=owner-browser"
        other_api = Mock()
        other_api._cookie_header.return_value = "kohami.browser=other-browser"
        owner_response = Mock()
        owner_response.status = 200
        owner_response.read.return_value = b'<html>checkout-synthetic TWD 560 Demo item Mock mode</html>'
        owner_response.__enter__ = Mock(return_value=owner_response)
        owner_response.__exit__ = Mock(return_value=False)
        denied_response = Mock()
        denied_response.status = 200
        denied_response.read.return_value = b'<html>This checkout result is unavailable</html><script>checkout-status-checkout-synthetic</script>'
        denied_response.__enter__ = Mock(return_value=denied_response)
        denied_response.__exit__ = Mock(return_value=False)
        artifact = Path("/private/tmp/session-artifact.json")

        with patch.object(ACCEPTANCE, "ApiAcceptance", return_value=other_api) as create_api, \
                patch.object(ACCEPTANCE.urllib.request, "urlopen", side_effect=[owner_response, denied_response]) as open_url:
            ACCEPTANCE.assert_checkout_ssr_scope(owner_api, artifact, status)

        create_api.assert_called_once_with(artifact)
        self.assertEqual(other_api.cookies, {})
        other_api._raw.assert_called_once_with("GET", "/auth/csrf")
        self.assertEqual(open_url.call_count, 2)
        self.assertIn("kohami.session=owner", open_url.call_args_list[0].args[0].get_header("Cookie"))
        self.assertIn("kohami.browser=other-browser", open_url.call_args_list[1].args[0].get_header("Cookie"))

    def test_expect_error_accepts_only_the_expected_http_status_and_code(self):
        api = ACCEPTANCE.ApiAcceptance.__new__(ACCEPTANCE.ApiAcceptance)
        api.origin = "http://127.0.0.1:5080/api/v1"
        api.cookies = {}

        with patch.object(ACCEPTANCE.urllib.request, "urlopen", side_effect=HTTPError(
            "http://127.0.0.1:5080/api/v1/checkouts/demo/status", 404, "Not Found", Message(),
            io.BytesIO(b'{"code":"RESOURCE_NOT_FOUND"}')
        )):
            api.expect_error("GET", "/checkouts/demo/status", 404, "RESOURCE_NOT_FOUND")

        with patch.object(ACCEPTANCE.urllib.request, "urlopen", side_effect=HTTPError(
            "http://127.0.0.1:5080/api/v1/checkouts/demo/status", 403, "Forbidden", Message(),
            io.BytesIO(b'{"code":"PERMISSION_CHANGED"}')
        )):
            with self.assertRaisesRegex(RuntimeError, "EXPECTED_DENIAL_UNEXPECTED"):
                api.expect_error("GET", "/checkouts/demo/status", 404, "RESOURCE_NOT_FOUND")

    def test_accepts_only_issue06_owned_compose_marker(self):
        self.assertTrue(ACCEPTANCE.valid_issue06_project("kohami-issue06-012345abcdef"))
        self.assertFalse(ACCEPTANCE.valid_issue06_project("kohami-issue02-012345abcdef"))
        self.assertFalse(ACCEPTANCE.valid_issue06_project("kohami-issue06-../unsafe"))

    def test_requires_pinned_localhost_issue06_fixture(self):
        with tempfile.NamedTemporaryFile() as certificate:
            good = (
                "Server=localhost,14336;Database=KohamiM0;Encrypt=Strict;"
                "TrustServerCertificate=False;HostNameInCertificate=localhost;"
                "Server Certificate=" + certificate.name
            )
            self.assertTrue(ACCEPTANCE.connection_targets_issue06_fixture(good, certificate.name))
            self.assertFalse(ACCEPTANCE.connection_targets_issue06_fixture(
                good.replace("localhost,14336", "localhost,14334"), certificate.name))
            self.assertFalse(ACCEPTANCE.connection_targets_issue06_fixture(
                good.replace("Encrypt=Strict", "Encrypt=Optional"), certificate.name))

    def test_never_removes_fixture_or_private_directory_during_cleanup(self):
        self.assertFalse(ACCEPTANCE.cleanup_fixture_after_run)
        self.assertTrue(ACCEPTANCE.cleanup_only_synthetic_rows)

    def test_browser_aftercare_has_a_dedicated_stage_and_order_setup(self):
        self.assertIn("browser_ui", ACCEPTANCE.ACCEPTANCE_STAGES)
        self.assertTrue(callable(ACCEPTANCE.prepare_browser_order))
        self.assertIn("issue06-playwright-artifacts", str(ACCEPTANCE.TEST_OUTPUT))

    def test_identity_only_path_runs_security_guards_without_worker_or_order_setup(self):
        source = SCRIPT.read_text()
        self.assertIn("'security_guards'", source)
        self.assertIn("parser.add_argument('--identity-only'", source)
        self.assertIn("if not args.identity_only and not args.ac02_only:", source)
        self.assertIn("Issue06 Identity logout rejects replay", source)


if __name__ == "__main__":
    unittest.main()
