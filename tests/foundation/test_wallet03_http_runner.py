import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

SCRIPT = Path(__file__).resolve().parents[2] / "scripts/foundation/run-wallet03-http-acceptance.py"
SPEC = importlib.util.spec_from_file_location("wallet03_http_runner", SCRIPT)
RUNNER = importlib.util.module_from_spec(SPEC)


class Wallet03HttpRunnerTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        assert SPEC and SPEC.loader
        SPEC.loader.exec_module(RUNNER)

    def profile(self, root: Path):
        root = root.resolve()
        local = root / ".local" / "kohami-sql-test" / "identity"
        local.mkdir(parents=True)
        cert = local / "sql" / "public.pem"
        cert.parent.mkdir()
        cert.write_text("fixture certificate path only")
        profile = {
            "IdentitySqlTestDedicated": True, "KohamiSqlTestDedicated": True,
            "port": 14333, "datasetId": "kohami_local", "origin": "https://localhost:3000",
            "identityNamespace": "kohami.identity_access.v1", "sqlServerName": "localhost",
            "sqlCertificatePath": str(cert),
            "connectionString": f"Data Source=localhost,14333;Initial Catalog=Kohami;Encrypt=Strict;TrustServerCertificate=False;Host Name In Certificate=localhost;Server Certificate={cert}",
            "workerConnectionString": f"Data Source=localhost,14333;Initial Catalog=Kohami;Encrypt=Strict;TrustServerCertificate=False;Host Name In Certificate=localhost;Server Certificate={cert}",
        }
        runtime = {key: profile[key] for key in ("datasetId", "origin", "identityNamespace", "sqlServerName", "sqlCertificatePath")}
        runtime["connectionString"] = "Server=localhost,14333;Database=Kohami;Encrypt=Strict;TrustServerCertificate=False"
        runtime["workerConnectionString"] = runtime["connectionString"]
        provisioner = {"fixtureId": "475bbde5-2f70-4825-903a-0c4047dc5169"}
        for name, value in (("fixture-private.json", profile), ("runtime-private.json", runtime), ("provisioner-private.json", provisioner)):
            path = local / name
            path.write_text(json.dumps(value))
            path.chmod(0o600)
        for path in (local, cert.parent):
            path.chmod(0o700)
        return local

    def test_accepts_only_canonical_private_fixture_profile(self):
        with tempfile.TemporaryDirectory() as root:
            local = self.profile(Path(root))
            self.assertEqual(RUNNER.verify_fixture_config(local, local.parents[2]), local.resolve())

    def test_rejects_wrong_fixture_or_sql_scope(self):
        for field, value in (("KohamiSqlTestDedicated", False), ("port", 14350), ("datasetId", "demo_m0")):
            with self.subTest(field=field), tempfile.TemporaryDirectory() as root:
                local = self.profile(Path(root))
                path = local / "fixture-private.json"
                config = json.loads(path.read_text())
                config[field] = value
                path.write_text(json.dumps(config))
                path.chmod(0o600)
                self.assertRaises(RuntimeError, RUNNER.verify_fixture_config, local, local.parents[2])

    def test_rejects_untrusted_sql_connection_settings(self):
        with tempfile.TemporaryDirectory() as root:
            local = self.profile(Path(root))
            path = local / "fixture-private.json"
            config = json.loads(path.read_text())
            config["connectionString"] = config["connectionString"].replace("Encrypt=Strict", "Encrypt=False")
            path.write_text(json.dumps(config))
            path.chmod(0o600)
            self.assertRaises(RuntimeError, RUNNER.verify_fixture_config, local, local.parents[2])

    def test_rejects_symlinked_or_noncanonical_profile_path(self):
        with tempfile.TemporaryDirectory() as root:
            repo = Path(root).resolve()
            local = self.profile(repo)
            wrong = repo / ".local" / "wallet03"
            wrong.mkdir()
            self.assertRaises(RuntimeError, RUNNER.verify_fixture_config, wrong, repo)
            link = repo / ".local" / "linked"
            link.symlink_to(local, target_is_directory=True)
            self.assertRaises(RuntimeError, RUNNER.verify_fixture_config, link, repo)

    def test_maintenance_window_requires_exact_issue11_helper_evidence(self):
        with tempfile.TemporaryDirectory() as root:
            local = self.profile(Path(root))
            runner = RUNNER
            original = (runner.ROOT, runner.LOCAL, runner.MAINTENANCE_STATE)
            try:
                runner.ROOT = Path(root)
                runner.LOCAL = local
                runner.MAINTENANCE_STATE = local / "issue11-maintenance-state-private.json"
                calls = []

                def run(args, capture=False):
                    calls.append(args)
                    if args[-2:] == ["open", str(runner.MAINTENANCE_STATE)]:
                        runner.MAINTENANCE_STATE.write_text("private-state")
                        return type("Completed", (), {"stdout": "PASS Issue11 maintenance disabled only on marker-verified canonical kohami-sql-test; exact prior state saved privately\n"})()
                    runner.MAINTENANCE_STATE.unlink()
                    return type("Completed", (), {"stdout": "PASS Issue11 fixture Maintenance and complete SecurityState restored and verified by SQL readback\n"})()

                with patch.object(runner, "run", side_effect=run), patch.object(runner, "require_maintenance"):
                    with runner.maintenance_window():
                        self.assertTrue(runner.MAINTENANCE_STATE.exists())
                self.assertFalse(runner.MAINTENANCE_STATE.exists())
                self.assertEqual([call[-2] for call in calls], ["open", "restore"])
            finally:
                runner.ROOT, runner.LOCAL, runner.MAINTENANCE_STATE = original


if __name__ == "__main__":
    unittest.main()
