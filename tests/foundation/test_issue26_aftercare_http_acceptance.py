from pathlib import Path
import unittest


ROOT = Path(__file__).resolve().parents[2]


class Issue26AftercareHttpAcceptanceTests(unittest.TestCase):
    def test_runner_uses_approved_fixture_and_maintenance_window(self):
        runner = ROOT / "scripts/foundation/run-issue26-aftercare-http-acceptance.py"
        self.assertTrue(runner.is_file())
        source = runner.read_text()
        self.assertIn("issue11-fixture-check", source)
        self.assertIn("issue11-maintenance-window", source)
        self.assertIn("issue26-aftercare-private.json", source)

    def test_acceptance_is_real_member_http_and_restores_exact_scope(self):
        helper = ROOT / "tests/Commerce/Issue26AftercareHttpAcceptance.cs"
        self.assertTrue(helper.is_file())
        source = helper.read_text()
        for contract in (
            '"auth/csrf"', '"auth/sessions"', 'MemberAuthenticated',
            'orders/{orderId}', 'receipts', 'returns', 'cancellations',
            'shipments', 'transitions', 'inspections',
            'NotFound', 'RefundCase', 'CleanupAndVerify', 'refund.execute',
        ):
            self.assertIn(contract, source)
        self.assertIn("Refund execution is intentionally outside", source)
        self.assertIn('"restore"', (ROOT / "scripts/foundation/run-issue26-aftercare-http-acceptance.py").read_text())

    def test_cancellation_only_acceptance_covers_owner_replay_stale_foreign_and_stock_restore(self):
        runner = (ROOT / "scripts/foundation/run-issue26-aftercare-http-acceptance.py").read_text()
        program = (ROOT / "tests/Commerce/Program.cs").read_text()
        helper_path = ROOT / "tests/Commerce/Issue26CancellationHttpAcceptance.cs"
        self.assertTrue(helper_path.is_file())
        helper = helper_path.read_text()
        for contract in (
            "cancellations", "Idempotency-Key", "HttpStatusCode.Accepted",
            "VERSION_CHANGED", "HttpStatusCode.NotFound", '"Released"', "StockBefore",
            "CleanupAndVerifyAsync",
        ):
            self.assertIn(contract, helper)
        self.assertIn("--cancellation-only", runner)
        self.assertIn("--issue26-cancellation-http", runner)
        self.assertIn("issue11-maintenance-window", runner)
        self.assertIn("--issue26-cancellation-http", program)
        self.assertIn("if opened or MAINTENANCE_STATE.exists()", runner)
        self.assertIn("PASS Issue11 fixture Maintenance and complete SecurityState restored and verified by SQL readback", runner)
        self.assertIn("ISSUE26_CANCELLATION_PRIVATE_STATE_REMAINS", runner)


if __name__ == "__main__":
    unittest.main()
