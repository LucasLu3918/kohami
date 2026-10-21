import importlib.util
import errno
import json
import sys
import tempfile
import unittest
from unittest.mock import patch
from pathlib import Path


SCRIPT = Path(__file__).resolve().parents[2] / "scripts" / "foundation" / "run-local.py"
sys.path.insert(0, str(SCRIPT.parent))
SPEC = importlib.util.spec_from_file_location("run_local", SCRIPT)
run_local = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(run_local)


class RunLocalConfigurationTests(unittest.TestCase):
    def test_defaults_to_kohami_sql_profile(self):
        root = Path("/repo")
        self.assertEqual(
            run_local.resolve_local_dir(root, {}),
            root / ".local" / "kohami-sql" / "identity",
        )

    def test_allows_explicit_private_profile_directory(self):
        root = Path("/repo")
        with self.assertRaisesRegex(RuntimeError, "LOCAL_RUNNER_REQUIRES_KOHAMI_SQL_PROFILE"):
            run_local.resolve_local_dir(root, {"KOHAMI_LOCAL_DIR": "/private/kohami"})

    def test_defaults_to_ports_after_existing_local_services(self):
        self.assertEqual(run_local.configured_port({}, "KOHAMI_API_PORT", 5083, 5080, 5099), 5083)
        self.assertEqual(run_local.configured_port({}, "KOHAMI_SITE_PORT", 3003, 3000, 3999), 3003)

    def test_identifies_api_listener_by_project_directory_and_release_binary(self):
        output = "\n".join((
            "p123",
            "fcwd",
            "n/repo",
            "ftxt",
            "n/usr/local/share/dotnet/dotnet",
            "ftxt",
            "n/repo/apps/api/bin/Release/net10.0/Api.dll",
        ))
        self.assertTrue(run_local.lsof_process_matches(output, Path("/repo"), Path("/repo"), "apps/api/bin/Release/net10.0/Api.dll"))
        self.assertFalse(run_local.lsof_process_matches(output, Path("/other"), Path("/other"), "apps/api/bin/Release/net10.0/Api.dll"))

    def test_identifies_storefront_listener_by_project_directory_and_source_files(self):
        output = "\n".join((
            "p456",
            "fcwd",
            "n/repo/apps/storefront",
            "ftxt",
            "n/opt/homebrew/bin/node",
            "n/repo/apps/storefront/app/app.vue",
        ))
        self.assertTrue(run_local.lsof_process_matches(output, Path("/repo"), Path("/repo/apps/storefront"), "apps/storefront/app/"))
        self.assertFalse(run_local.lsof_process_matches(output, Path("/repo"), Path("/repo/apps/other"), "apps/storefront/app/"))

    def test_loopback_upstreams_bypass_system_proxy_and_preserve_existing_exclusions(self):
        environment = {"NO_PROXY": "internal.example", "no_proxy": "database.internal"}

        run_local.configure_loopback_proxy_bypass(environment)

        expected = "internal.example,database.internal,127.0.0.1,localhost,::1"
        self.assertEqual(environment["NO_PROXY"], expected)
        self.assertEqual(environment["no_proxy"], expected)

    def test_preview_config_changes_only_expected_origin(self):
        with tempfile.TemporaryDirectory() as directory:
            source = Path(directory) / "runtime-private.json"
            source.write_text(json.dumps({"origin": "https://localhost:3000", "private": "keep"}))

            with run_local.preview_runtime_config(source, 3002) as preview:
                payload = json.loads(preview.read_text())
                self.assertEqual(payload["origin"], "https://localhost:3002")
                self.assertEqual(payload["private"], "keep")
                self.assertEqual(preview.stat().st_mode & 0o777, 0o600)
                self.assertNotEqual(preview, source)

            self.assertFalse(preview.exists())
            self.assertEqual(json.loads(source.read_text())["origin"], "https://localhost:3000")

    def test_port_conflict_is_distinguished_from_sandbox_denial(self):
        class Listener:
            def __enter__(self):
                return self

            def __exit__(self, *_args):
                self.close()

            def setsockopt(self, *_args):
                pass

            def bind(self, _address):
                raise OSError(errno.EPERM, "sandbox denied bind")

            def close(self):
                pass

        with patch.object(run_local.socket, "socket", return_value=Listener()):
            with self.assertRaisesRegex(RuntimeError, "LOCAL_PORT_CHECK_BLOCKED:3002:EPERM"):
                run_local.require_free_loopback_port(3002)

        class ConflictingListener(Listener):
            def bind(self, _address):
                raise OSError(errno.EADDRINUSE, "already listening")

        with patch.object(run_local.socket, "socket", return_value=ConflictingListener()):
            with self.assertRaisesRegex(RuntimeError, "LOCAL_PORT_IN_USE:3002"):
                run_local.require_free_loopback_port(3002)


if __name__ == "__main__":
    unittest.main()
