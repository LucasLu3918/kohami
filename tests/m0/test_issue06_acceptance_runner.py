import importlib.util
import io
import json
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

    def test_expect_error_returns_payload_for_sanitized_query_failure_assertions(self):
        api = ACCEPTANCE.ApiAcceptance.__new__(ACCEPTANCE.ApiAcceptance)
        api.origin = "http://127.0.0.1:5080/api/v1"
        api.cookies = {}
        with patch.object(ACCEPTANCE.urllib.request, "urlopen", side_effect=HTTPError(
            "http://127.0.0.1:5080/api/v1/admin/reconciliation-report", 503, "Unavailable", Message(),
            io.BytesIO('{"type":"about:blank","title":"無法完成模擬操作","status":503,"code":"SERVICE_UNAVAILABLE","traceId":"synthetic"}'.encode())
        )):
            payload = api.expect_error("GET", "/admin/reconciliation-report", 503, "SERVICE_UNAVAILABLE")
        self.assertEqual(payload["code"], "SERVICE_UNAVAILABLE")
        self.assertNotIn("sql", json.dumps(payload).lower())

    def test_issue15_query_failure_restores_fault_before_recovery_read(self):
        source = SCRIPT.read_text()
        self.assertIn("parser.add_argument('--issue15-query-failure-only'", source)
        fault = source.index("def verify_issue15_query_failure")
        self.assertLess(fault, source.index("def prepare_browser_order", fault))
        self.assertIn("finally:", source[fault:source.index("def prepare_browser_order", fault)])
        self.assertIn("issue15-report-query-fault', str(SOURCE_CONFIG), 'off'", source)
        self.assertIn("ISSUE15_QUERY_FAILURE_NOT_SANITIZED", source)
        self.assertIn("if not args.issue15_query_failure_only:", source)

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

    def test_api_acceptance_uses_the_recorded_synthetic_command_actor(self):
        with tempfile.TemporaryDirectory() as directory:
            artifact = Path(directory) / "session.json"
            artifact.write_text('{"sessionCookie":"session","browserCookie":"browser","commandCookie":"command"}')
            api = ACCEPTANCE.ApiAcceptance(artifact)

        self.assertEqual(api.cookies["kohami.command"], "command")

    def test_browser_aftercare_has_a_dedicated_stage_and_order_setup(self):
        self.assertIn("browser_ui", ACCEPTANCE.ACCEPTANCE_STAGES)
        self.assertTrue(callable(ACCEPTANCE.prepare_browser_order))
        self.assertIn("issue06-playwright-artifacts", str(ACCEPTANCE.TEST_OUTPUT))
        browser_test = (Path(__file__).resolve().parents[2] / "apps/storefront/tests/issue06-aftercare.spec.ts").read_text()
        self.assertIn("artifact.commandCookie", browser_test)

    def test_identity_only_path_runs_security_guards_without_worker_or_order_setup(self):
        source = SCRIPT.read_text()
        self.assertIn("'security_guards'", source)
        self.assertIn("parser.add_argument('--identity-only'", source)
        self.assertIn("if not args.identity_only and not args.ac02_only and not args.catalog_only and not args.search_only and not args.cart_only:", source)
        self.assertIn("Issue06 Identity logout rejects replay", source)

    def test_issue10_search_mode_isolated_from_worker_and_has_catalog_search_stage(self):
        source = SCRIPT.read_text()
        self.assertIn("parser.add_argument('--search-only'", source)
        self.assertIn("'catalog_search'", source)
        self.assertIn("'--grep', 'Issue10'", source)
        self.assertIn("'issue10-stock-zero'", source)
        self.assertIn("'issue10-stock-restore'", source)

    def test_issue11_cart_mode_isolated_and_cleans_only_recorded_cart_evidence(self):
        source = SCRIPT.read_text()
        self.assertIn("parser.add_argument('--cart-only'", source)
        self.assertIn("'--grep', 'Issue11'", source)
        self.assertIn("'issue11-cart-cleanup'", source)
        self.assertIn("cart", ACCEPTANCE.ACCEPTANCE_STAGES)
        self.assertIn("and not args.cart_only", source)

    def test_issue13_payment_outcomes_use_live_api_sql_and_browser_mode(self):
        source = SCRIPT.read_text()
        self.assertIn("parser.add_argument('--payment-outcomes-only'", source)
        self.assertIn("create_issue13_payment_case(api, 'Failed')", source)
        self.assertIn("create_issue13_payment_case(api, 'Unknown')", source)
        self.assertIn("ISSUE13_SERVICE_OUTAGE_CHANGED_PAYMENT_RESULT", source)
        self.assertIn("'issue13-payment-outcome-verify'", source)
        self.assertIn("Issue13 live payment result", source)

    def test_issue14_late_payment_mode_checks_support_api_sql_and_browser(self):
        source = SCRIPT.read_text()
        self.assertIn("parser.add_argument('--issue14-late-payment-only'", source)
        self.assertIn("create_issue14_late_payment_case(api)", source)
        self.assertIn("issue14-verify-late-compensation", source)
        self.assertIn("Issue14 live late payment", source)

    def test_issue15_manual_reconciliation_checks_api_replay_and_live_storefront(self):
        source = SCRIPT.read_text()
        self.assertIn("parser.add_argument('--issue15-manual-reconciliation-only'", source)
        self.assertIn("create_issue15_reconciliation_case(api, case, issue15_browser_source[0])", source)
        self.assertIn("issue15-report-grant", source)
        self.assertIn("ISSUE15_READ_REPLAY_CHANGED_REPORT", source)
        self.assertIn("'--grep', 'Issue15 manual reconciliation queues original check'", source)
        self.assertIn("'--grep', 'Issue15 manual reconciliation closes cleared finding'", source)
        self.assertIn("issue15_reconciliation", source)

    def test_issue15_case_provisions_guest_read_capability_for_its_browser_order(self):
        report = {
            "mode": "mock", "readOnly": True, "scheduleStatus": "ManualOnly",
            "state": "DifferencesFound",
            "summary": {"lateOrExtraCollectionCount": 1, "compensationRequiredOrderCount": 1},
            "findings": [{"code": "NON_PRIMARY_COLLECTION_REQUIRES_REVIEW", "sourceId": "collection_issue15_synthetic"}],
        }
        api = Mock()
        api.call.side_effect = [report, report]
        case = {"orderId": "order_issue15_synthetic"}
        with patch.object(ACCEPTANCE, "run") as run:
            result = ACCEPTANCE.create_issue15_reconciliation_case(api, case, "collection_issue15_synthetic")

        self.assertEqual(result, {**case, "sourceId": "collection_issue15_synthetic"})
        run.assert_called_once_with([
            str(ACCEPTANCE.DOTNET), str(ACCEPTANCE.COMMERCE_DLL),
            "issue06-owner-capabilities", str(ACCEPTANCE.SOURCE_CONFIG),
            str(ACCEPTANCE.ARTIFACT), case["orderId"],
        ], timeout=30)

    def test_issue15_browser_acceptance_sends_fixture_guest_read_cookie(self):
        browser_test = (Path(__file__).resolve().parents[2]
                        / "apps/storefront/tests/issue15-reconciliation.spec.ts").read_text()
        self.assertIn("guestReadCookie: string", browser_test)
        self.assertIn("name: 'kohami.guest.read'", browser_test)
        self.assertIn("value: session.guestReadCookie", browser_test)

    def test_issue15_browser_clearance_uses_reported_source_between_resolution_journeys(self):
        source = SCRIPT.read_text()
        self.assertIn("def clear_issue15_reconciliation_source(api: ApiAcceptance, source_id: str) -> None:", source)
        self.assertIn("def seed_issue15_reconciliation_source(api: ApiAcceptance) -> tuple[str, str]:", source)
        manual = source.index("elif args.issue15_manual_reconciliation_only:")
        queue_browser = source.index("Issue15 manual reconciliation queues original check", manual)
        clear_source = source.index("clear_issue15_reconciliation_source(api, case['sourceId'])", queue_browser)
        refresh_capability = source.index("'issue06-owner-capabilities'", queue_browser)
        close_browser = source.index("Issue15 manual reconciliation closes cleared finding", clear_source)
        self.assertLess(queue_browser, clear_source)
        self.assertLess(refresh_capability, clear_source)
        self.assertLess(clear_source, close_browser)
        self.assertIn("issue15-cleanup-browser-source", source)
        browser_test = (Path(__file__).resolve().parents[2]
                        / "apps/storefront/tests/issue15-reconciliation.spec.ts").read_text()
        self.assertIn("KOHAMI_ISSUE15_SOURCE_ID", browser_test)

    def test_issue13_worker_outcome_verification_requires_exact_unknown_and_failure_states(self):
        source = (Path(__file__).resolve().parents[2] / "tests/Commerce/SyntheticOwnerAcceptance.cs").read_text()
        self.assertIn('expectedState is not ("Failed" or "Unknown")', source)
        self.assertIn('paymentJobState != "Completed"', source)
        self.assertIn('orderPaymentState != expectedState || orderDocumentState != "AwaitingPayment"', source)
        self.assertIn('attemptCount != 1 || collectionCount != 0', source)
        self.assertIn('CatalogInventory: catalogInventory.ToArray()', source)
        self.assertIn('if (!catalogInventory.Any(item => item.Sku == "sku_demo_03"))', source)
        self.assertIn('foreach (var snapshot in inventorySnapshots)', source)


if __name__ == "__main__":
    unittest.main()
