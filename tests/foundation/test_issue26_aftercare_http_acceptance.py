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


if __name__ == "__main__":
    unittest.main()
