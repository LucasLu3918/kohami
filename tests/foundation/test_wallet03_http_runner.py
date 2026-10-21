import importlib.util
from pathlib import Path
import tempfile
import unittest

SCRIPT = Path(__file__).resolve().parents[2] / "scripts/foundation/run-wallet03-http-acceptance.py"
SPEC = importlib.util.spec_from_file_location("wallet03_http_runner", SCRIPT)
RUNNER = importlib.util.module_from_spec(SPEC)


class Wallet03HttpRunnerTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        assert SPEC and SPEC.loader
        SPEC.loader.exec_module(RUNNER)

    def test_accepts_only_repo_local_non_symlink_fixture_directory(self):
        with tempfile.TemporaryDirectory() as root:
            repo = Path(root)
            local = repo / ".local" / "wallet03"
            local.mkdir(parents=True)
            for name in ("runtime-private.json", "provisioner-private.json", "fixture-private.json"):
                (local / name).write_text("{}")
            self.assertEqual(RUNNER.validate_local_dir(repo, local), local.resolve())
            outside = repo.parent / (repo.name + "-outside")
            outside.mkdir()
            self.assertRaises(RuntimeError, RUNNER.validate_local_dir, repo, outside)

    def test_api_port_is_loopback_acceptance_range_and_numeric(self):
        self.assertEqual(RUNNER.validate_port(5086), 5086)
        for value in (5079, 5100, 0, -1, "5080"):
            with self.subTest(value=value):
                self.assertRaises(RuntimeError, RUNNER.validate_port, value)

    def test_rejects_symlinked_fixture_directory(self):
        with tempfile.TemporaryDirectory() as root:
            repo = Path(root)
            (repo / ".local").mkdir()
            target = repo / "outside"
            target.mkdir()
            link = repo / ".local" / "linked"
            link.symlink_to(target, target_is_directory=True)
            self.assertRaises(RuntimeError, RUNNER.validate_local_dir, repo, link)


if __name__ == "__main__":
    unittest.main()
