import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
import urllib.error
from unittest.mock import MagicMock, patch

SCRIPT = Path(__file__).resolve().parents[2] / "scripts/foundation/run-kohami-demo.py"
SPEC = importlib.util.spec_from_file_location("run_kohami_demo", SCRIPT)
RUNNER = importlib.util.module_from_spec(SPEC)
assert SPEC and SPEC.loader
SPEC.loader.exec_module(RUNNER)


class RunKohamiDemoTests(unittest.TestCase):
    def profile(self, root: Path) -> Path:
        local = root / ".local/kohami-demo/environment"
        for relative in ("browser", "ca"):
            (local / relative).mkdir(parents=True, exist_ok=True)
        files = {
            "fixture-private.json": {"KohamiDemoDedicated": True, "port": 14351},
            "runtime-private.json": {
                "datasetId": "kohami_local",
                "connectionString": "Server=localhost,14351;Database=Kohami;Encrypt=Strict;TrustServerCertificate=False;",
            },
        }
        for name, value in files.items():
            path = local / name
            path.write_text(json.dumps(value))
            path.chmod(0o600)
        for name in ("browser/public.pem", "browser/private.key", "ca/public.pem"):
            path = local / name
            path.write_text("synthetic")
            path.chmod(0o600)
        return local

    def test_accepts_only_marker_verified_local_demo_profile(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            local = self.profile(root)
            # The runner roots all profiles at the checked-out repository's .local.
            original_root = RUNNER.ROOT
            try:
                RUNNER.ROOT = root
                (root / ".local").mkdir(exist_ok=True)
                selected, runtime = RUNNER.validate_profile(local)
                self.assertEqual(selected, local)
                self.assertEqual(runtime["datasetId"], "kohami_local")
            finally:
                RUNNER.ROOT = original_root

    def test_rejects_wrong_fixture_purpose_and_protected_ports(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            local = self.profile(root)
            original_root = RUNNER.ROOT
            try:
                RUNNER.ROOT = root
                config_path = local / "fixture-private.json"
                for config in (
                    {"Wallet03HttpDedicated": True, "port": 14350},
                    {"KohamiDemoDedicated": True, "port": 14349},
                ):
                    config_path.write_text(json.dumps(config))
                    with self.assertRaises(RuntimeError):
                        RUNNER.validate_profile(local)
            finally:
                RUNNER.ROOT = original_root

    def test_rejects_decoy_or_ambiguous_sql_connection_targets(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            local = self.profile(root)
            original_root = RUNNER.ROOT
            try:
                RUNNER.ROOT = root
                runtime_path = local / "runtime-private.json"
                for connection in (
                    "Server=localhost,14349;Data Source=localhost,14351;Initial Catalog=Kohami;Encrypt=Strict;TrustServerCertificate=False",
                    "Data Source=localhost,14351;Initial Catalog=Kohami;Initial Catalog=master;Encrypt=Strict;TrustServerCertificate=False",
                    "Data Source=localhost,14349;Initial Catalog=Kohami;Encrypt=Strict;TrustServerCertificate=False",
                    "Data Source=remote,14351;Initial Catalog=Kohami;Encrypt=Strict;TrustServerCertificate=False",
                    "Data Source=localhost,14351;Initial Catalog=master;Encrypt=Strict;TrustServerCertificate=False",
                ):
                    runtime = json.loads(runtime_path.read_text())
                    runtime["connectionString"] = connection
                    runtime_path.write_text(json.dumps(runtime))
                    with self.subTest(connection=connection), self.assertRaises(RuntimeError):
                        RUNNER.validate_profile(local)
            finally:
                RUNNER.ROOT = original_root

    def test_connection_parser_ignores_quoted_decoy_text_in_password(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            local = self.profile(root)
            original_root = RUNNER.ROOT
            try:
                RUNNER.ROOT = root
                runtime_path = local / "runtime-private.json"
                runtime = json.loads(runtime_path.read_text())
                runtime["connectionString"] = (
                    'Data Source=localhost,14351;Initial Catalog=Kohami;'
                    'Password="pw;Data Source=localhost,14349";Encrypt=Strict;TrustServerCertificate=False'
                )
                runtime_path.write_text(json.dumps(runtime))
                _, selected = RUNNER.validate_profile(local)
                self.assertEqual(selected["datasetId"], "kohami_local")
            finally:
                RUNNER.ROOT = original_root

    def test_live_fixture_marker_must_be_verified_before_api_launch(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            local = self.profile(root)
            original_root = RUNNER.ROOT
            try:
                RUNNER.ROOT = root
                env = {"KOHAMI_LOCAL_DIR": str(local)}
                with patch.object(RUNNER.subprocess, "run", side_effect=RUNNER.subprocess.CalledProcessError(1, ["native"])) as run:
                    with self.assertRaisesRegex(RuntimeError, "DEMO_FIXTURE_MARKER_VERIFICATION_FAILED"):
                        RUNNER.verify_live_fixture(local, 14351, env)
                args, kwargs = run.call_args
                self.assertEqual(args[0][-3:], ["verify-local-demo-catalog", str(root), "14351"])
                self.assertEqual(kwargs["env"]["KOHAMI_LOCAL_DIR"], str(local))
                self.assertEqual(kwargs["env"]["KOHAMI_FIXTURE_PURPOSE"], "local-demo")
                self.assertTrue(kwargs["check"])
            finally:
                RUNNER.ROOT = original_root

    def test_missing_or_forged_live_marker_blocks_api_process_launch(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            local = self.profile(root)
            api = root / "apps/api/bin/Release/net10.0/Api.dll"
            api.parent.mkdir(parents=True)
            api.touch()
            original_root = RUNNER.ROOT
            try:
                RUNNER.ROOT = root
                with patch.object(RUNNER, "require_loopback_port"), patch.object(
                    RUNNER, "verify_live_fixture", side_effect=RuntimeError("DEMO_FIXTURE_MARKER_VERIFICATION_FAILED")
                ), patch.object(RUNNER.subprocess, "Popen") as popen:
                    with self.assertRaisesRegex(RuntimeError, "DEMO_FIXTURE_MARKER_VERIFICATION_FAILED"):
                        RUNNER.serve(local, 5087, 3004)
                popen.assert_not_called()
            finally:
                RUNNER.ROOT = original_root

    def test_child_exit_is_observed_and_remaining_child_is_stopped(self):
        exited = MagicMock(returncode=7)
        exited.poll.return_value = 7
        running = MagicMock()
        running.poll.return_value = None
        with patch.object(RUNNER.time, "sleep"):
            self.assertEqual(RUNNER.wait_for_children([exited, running], poll_interval=0), 7)
        RUNNER.stop_children([exited, running])
        running.terminate.assert_called_once()
        running.wait.assert_called_once_with(timeout=8)

    def test_readiness_requires_both_database_health_and_schema_ready(self):
        responses = iter([
            (200, b'{"status":"database-connected"}'),
            (503, b'{"status":"not-ready"}'),
        ])
        with patch.object(RUNNER, "request_json", side_effect=lambda *_a, **_k: next(responses)), patch.object(RUNNER.time, "sleep"):
            with self.assertRaisesRegex(RuntimeError, "DEMO_API_SCHEMA_NOT_READY"):
                RUNNER.wait_database_ready(5087, seconds=1)

    def test_runner_waits_for_fixture_ca_verified_https_storefront_before_ready(self):
        class Response:
            status = 200

            def __enter__(self):
                return self

            def __exit__(self, *_args):
                return False

        with patch.object(RUNNER.ssl, "create_default_context", return_value="trusted-context") as context, patch.object(
            RUNNER.urllib.request, "urlopen", side_effect=[urllib.error.URLError("starting"), Response()]
        ) as urlopen, patch.object(RUNNER.time, "sleep"):
            RUNNER.wait_storefront_ready(3004, Path("/demo/ca.pem"), seconds=1)

        context.assert_called_once_with(cafile="/demo/ca.pem")
        self.assertEqual(urlopen.call_count, 2)

    def test_reusable_port_probe_allows_clean_restart_after_tcp_time_wait(self):
        listener = MagicMock()
        listener.__enter__.return_value = listener
        with patch.object(RUNNER.socket, "socket", return_value=listener):
            RUNNER.require_loopback_port(5087, 5080, 5099, "API")

        listener.setsockopt.assert_called_once_with(
            RUNNER.socket.SOL_SOCKET, RUNNER.socket.SO_REUSEADDR, 1
        )


if __name__ == "__main__":
    unittest.main()
