import importlib.util
from pathlib import Path
import tempfile
import json
import unittest

SCRIPT = Path(__file__).resolve().parents[2] / "scripts/foundation/run-local-demo-wallet-http-acceptance.py"
SPEC = importlib.util.spec_from_file_location("local_demo_wallet_http_runner", SCRIPT)
RUNNER = importlib.util.module_from_spec(SPEC)


class LocalDemoWalletHttpRunnerTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        assert SPEC and SPEC.loader
        SPEC.loader.exec_module(RUNNER)

    def write_profile(self, repo, port=14351, marker=True):
        local = repo / ".local" / "kohami-demo" / "environment"
        local.mkdir(parents=True)
        (local / "runtime-private.json").write_text(json.dumps({"connectionString": f"Server=localhost,{port};Database=Kohami;Encrypt=Strict;TrustServerCertificate=False", "sqlServerName": "localhost", "sqlCertificatePath": "/private/sql.pem", "origin": "https://localhost:3000"}))
        (local / "provisioner-private.json").write_text(json.dumps({"fixtureId": "e11d64a8-7a30-43e9-b011-dc0fd13a5f00", "migrator": "private"}))
        (local / "fixture-private.json").write_text(json.dumps({"KohamiDemoDedicated": marker, "port": port}))
        (local / "local-demo-compose-project").write_text("kohami-local-demo-012345abcdef\n")
        cert_dir = local / "cert"
        cert_dir.mkdir()
        cert = cert_dir / "sql.pem"
        cert.write_text("synthetic certificate fixture")
        (local / "runtime-private.json").write_text(json.dumps({"connectionString": f"Server=localhost,{port};Database=Kohami;Encrypt=Strict;TrustServerCertificate=False", "sqlServerName": "localhost", "sqlCertificatePath": str(cert), "origin": "https://localhost:3000"}))
        for name in ("runtime-private.json", "provisioner-private.json", "fixture-private.json", "local-demo-compose-project"):
            (local / name).chmod(0o600)
        return local

    def test_accepts_only_marker_verified_local_demo_profile_and_reserved_port_exclusions(self):
        with tempfile.TemporaryDirectory() as root:
            repo = Path(root)
            local = self.write_profile(repo)
            self.assertEqual(RUNNER.validate_profile(repo, local), local.resolve())
            for port in (14349, 14350, 14335, 14400):
                with self.subTest(port=port):
                    self.write_profile(repo / f"bad-{port}", port=port)
                    self.assertRaises(RuntimeError, RUNNER.validate_profile, repo / f"bad-{port}", repo / f"bad-{port}" / ".local" / "kohami-demo" / "environment")

    def test_rejects_wrong_marker_or_compose_identity(self):
        with tempfile.TemporaryDirectory() as root:
            repo = Path(root)
            local = self.write_profile(repo, marker=False)
            self.assertRaises(RuntimeError, RUNNER.validate_profile, repo, local)
            (local / "fixture-private.json").write_text(json.dumps({"KohamiDemoDedicated": True, "port": 14351}))
            (local / "local-demo-compose-project").write_text("kohami-wallet03-http-012345abcdef\n")
            self.assertRaises(RuntimeError, RUNNER.validate_profile, repo, local)

    def test_api_port_is_acceptance_range_and_numeric(self):
        self.assertEqual(RUNNER.validate_api_port(5088), 5088)
        for port in (5079, 5100, 0, -1, "5088"):
            with self.subTest(port=port):
                self.assertRaises(RuntimeError, RUNNER.validate_api_port, port)

    def test_rejects_weak_tls_or_wrong_server_name(self):
        with tempfile.TemporaryDirectory() as root:
            repo = Path(root)
            local = self.write_profile(repo)
            runtime_path = local / "runtime-private.json"
            data = json.loads(runtime_path.read_text())
            data["connectionString"] = data["connectionString"].replace("Encrypt=Strict", "Encrypt=True").replace("TrustServerCertificate=False", "TrustServerCertificate=True")
            runtime_path.write_text(json.dumps(data))
            self.assertRaises(RuntimeError, RUNNER.validate_profile, repo, local)
            data["connectionString"] = f"Server=localhost,14351;Database=Kohami;Encrypt=Strict;TrustServerCertificate=False"
            data["sqlServerName"] = "remote.example"
            runtime_path.write_text(json.dumps(data))
            self.assertRaises(RuntimeError, RUNNER.validate_profile, repo, local)
            data["sqlServerName"] = "localhost"
            data["connectionString"] = data["connectionString"].replace("Server=localhost,14351", "Server=db.example,14351")
            runtime_path.write_text(json.dumps(data))
            self.assertRaises(RuntimeError, RUNNER.validate_profile, repo, local)

    @unittest.skipIf(__import__("os").name == "nt", "POSIX permission bits are unavailable on Windows")
    def test_rejects_unsafe_private_config_mode(self):
        with tempfile.TemporaryDirectory() as root:
            repo = Path(root)
            local = self.write_profile(repo)
            private_config = local / "provisioner-private.json"
            private_config.chmod(0o640)
            with self.assertRaisesRegex(RuntimeError, "LOCAL_DEMO_PROFILE_MODE_INVALID"):
                RUNNER.validate_profile(repo, local)

    def test_rejects_profile_outside_local_root(self):
        with tempfile.TemporaryDirectory() as root:
            repo = Path(root)
            local = self.write_profile(repo)
            external = Path(root) / "external-profile"
            external.mkdir()
            with self.assertRaisesRegex(RuntimeError, "LOCAL_DEMO_PROFILE_OUTSIDE_REPO_LOCAL"):
                RUNNER.validate_local_dir(repo, external)

    def test_rejects_symlinked_profile_parent(self):
        with tempfile.TemporaryDirectory() as root:
            repo = Path(root)
            local = self.write_profile(repo)
            profiles = repo / ".local" / "profiles"
            profiles.symlink_to(local.parent, target_is_directory=True)
            through_link = profiles / local.name
            with self.assertRaisesRegex(RuntimeError, "LOCAL_DEMO_PROFILE_SYMLINK"):
                RUNNER.validate_local_dir(repo, through_link)

    def test_rejects_symlinked_private_config_and_certificate(self):
        with tempfile.TemporaryDirectory() as root:
            repo = Path(root)
            local = self.write_profile(repo)
            config = local / "fixture-private.json"
            target = local / "fixture-target.json"
            config.rename(target)
            config.symlink_to(target)
            self.assertRaises(RuntimeError, RUNNER.validate_profile, repo, local)
        with tempfile.TemporaryDirectory() as root:
            repo = Path(root)
            local = self.write_profile(repo)
            cert = local / "cert" / "sql.pem"
            cert.unlink()
            cert.symlink_to(Path("/etc/hosts"))
            self.assertRaises(RuntimeError, RUNNER.validate_profile, repo, local)


if __name__ == "__main__":
    unittest.main()
