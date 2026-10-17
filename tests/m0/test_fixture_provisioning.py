import importlib.util
from pathlib import Path
from subprocess import TimeoutExpired
from types import SimpleNamespace
import unittest
from unittest.mock import patch

SCRIPT = Path(__file__).resolve().parents[2] / "scripts/m0/fixture_provisioning.py"
SPEC = importlib.util.spec_from_file_location("fixture_provisioning", SCRIPT)
PROVISIONING = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(PROVISIONING)


class FixtureProvisioningTests(unittest.TestCase):
    def test_fixture_port_range_supports_isolated_loopback_fixture_without_touching_default_port(self):
        self.assertTrue(PROVISIONING.valid_fixture_port(14336))
        self.assertTrue(PROVISIONING.valid_fixture_port(14337))
        self.assertTrue(PROVISIONING.valid_fixture_port(14399))
        self.assertFalse(PROVISIONING.valid_fixture_port(14335))
        self.assertFalse(PROVISIONING.valid_fixture_port(14400))

    def test_issue02_required_port_checks_preserve_selected_sql_fixture_port(self):
        self.assertEqual(PROVISIONING.required_ports_for("issue02", 14337), (14337, 5080, 3000))
        self.assertEqual(PROVISIONING.required_ports_for("issue06", 14337), (14337,))

    def test_sql_readiness_retries_a_single_probe_timeout(self):
        with patch.object(PROVISIONING.subprocess, "run", side_effect=[
            TimeoutExpired("fixture-status", 15),
            SimpleNamespace(returncode=0),
        ]) as run, patch.object(PROVISIONING.time, "sleep"):
            PROVISIONING.wait_sql_ready({}, 14337, seconds=30)

        self.assertEqual(run.call_count, 2)


if __name__ == "__main__":
    unittest.main()
