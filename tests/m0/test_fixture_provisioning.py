import importlib.util
from pathlib import Path
import unittest

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


if __name__ == "__main__":
    unittest.main()
