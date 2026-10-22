import importlib.util
import inspect
from pathlib import Path
import sys
import unittest

ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / "scripts/foundation/run-issue25-analyst-reports-acceptance.py"
HELPER = ROOT / "tests/Commerce/Issue25AnalystReportsAcceptance.cs"

SPEC = importlib.util.spec_from_file_location("issue25_analyst_reports", SCRIPT)
assert SPEC and SPEC.loader
RUNNER = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = RUNNER
SPEC.loader.exec_module(RUNNER)


class Issue25AnalystReportsContractTests(unittest.TestCase):
    def test_plan_checks_exact_scope_wrong_scope_and_revoked_grant_on_same_route(self):
        cases = RUNNER.authorization_cases()
        self.assertEqual(
            [(case.name, case.method, case.path, case.expected_status, case.expected_code)
             for case in cases],
            [
                ("exact Analyst report scope allowed", "GET", "/api/v1/admin/reports?from=2026-10-22&to=2026-10-22", 200, None),
                ("wrong Analyst report scope denied", "GET", "/api/v1/admin/reports?from=2026-10-22&to=2026-10-22", 403, "PERMISSION_CHANGED"),
                ("revoked Analyst report grant denied in active session", "GET", "/api/v1/admin/reports?from=2026-10-22&to=2026-10-22", 403, "PERMISSION_CHANGED"),
            ],
        )
        self.assertEqual(len({case.path for case in cases}), 1)

    def test_runner_rejects_unapproved_sql_targets_and_uses_private_state(self):
        source = inspect.getsource(RUNNER)
        self.assertIn("kohami-sql-test", source)
        self.assertIn("Issue25AnalystReportsAcceptance.cs", source)
        self.assertIn("--approved-fixture-run", source)
        self.assertIn("--approved-maintenance-window", source)
        self.assertLess(source.index('invoke_sql_helper("cleanup"'),
                        source.index('invoke_sql_helper("maintenance-restore"'))
        helper = HELPER.read_text()
        self.assertIn("DedicatedAdmin(configPath)", helper)
        self.assertIn("VerifyFixtureMarkerAsync", helper)
        self.assertIn("File.SetUnixFileMode", helper)
        self.assertIn("OpenMaintenanceWindowAsync", helper)
        self.assertIn("RestoreMaintenanceWindowAsync", helper)
        self.assertIn("finally", source)

    def test_runner_dispatches_helpers_from_the_released_commerce_binary(self):
        runner = SCRIPT.read_text()
        program = (ROOT / "tests/Commerce/Program.cs").read_text()
        self.assertIn('"--issue25-analyst-reports"', program)
        self.assertIn('"--issue25-analyst-reports", action', runner)
        self.assertNotIn("TemporaryDirectory(prefix=\"issue25-analyst-runner-\")", runner)
        self.assertIn('"Cookie": self._cookie_header()', runner)
        self.assertIn("SimpleCookie()", runner)

    def test_sql_helper_seeds_only_analyst_reports_operations_and_cleans_exact_rows(self):
        source = HELPER.read_text()
        self.assertIn("'Analyst','reports.read','Report','operations'", source)
        self.assertIn("identity_access.security_lock", source)
        self.assertIn("RevokedAt=SYSDATETIMEOFFSET()", source)
        self.assertIn("ScopeId='operations'", source)
        self.assertIn("commerce.ReportAccessAudit", source)
        self.assertIn("identity_access.Sessions", source)
        self.assertIn("DELETE FROM commerce.ReportAccessAudit WHERE ActorUserId=@user", source)
        self.assertIn("ISSUE25_ANALYST_CLEANUP_READBACK_FAILED", source)
        self.assertIn("VerifyFixtureMarkerAsync", source)
        self.assertIn("Stage='AdminAuthenticated'", source)


if __name__ == "__main__":
    unittest.main()
