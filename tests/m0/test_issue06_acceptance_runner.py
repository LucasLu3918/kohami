import importlib.util
from pathlib import Path
import tempfile
import unittest

SCRIPT = Path(__file__).resolve().parents[2] / "scripts/m0/run-issue06-aftercare-acceptance.py"
SPEC = importlib.util.spec_from_file_location("issue06_aftercare", SCRIPT)
ACCEPTANCE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(ACCEPTANCE)


class Issue06AftercareRunnerTests(unittest.TestCase):
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


if __name__ == "__main__":
    unittest.main()
