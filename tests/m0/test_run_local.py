import importlib.util
import errno
import json
import tempfile
import unittest
from unittest.mock import patch
from pathlib import Path


SCRIPT = Path(__file__).resolve().parents[2] / "scripts" / "m0" / "run-local.py"
SPEC = importlib.util.spec_from_file_location("run_local", SCRIPT)
run_local = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(run_local)


class RunLocalConfigurationTests(unittest.TestCase):
    def test_defaults_to_kohami_sql_profile(self):
        root = Path("/repo")
        self.assertEqual(
            run_local.resolve_local_dir(root, {}),
            root / ".local" / "kohami-sql" / "m0",
        )

    def test_allows_explicit_private_profile_directory(self):
        root = Path("/repo")
        self.assertEqual(
            run_local.resolve_local_dir(root, {"KOHAMI_M0_LOCAL_DIR": "/private/kohami"}),
            Path("/private/kohami"),
        )

    def test_defaults_to_ports_after_existing_local_services(self):
        self.assertEqual(run_local.configured_port({}, "KOHAMI_API_PORT", 5083, 5080, 5099), 5083)
        self.assertEqual(run_local.configured_port({}, "KOHAMI_SITE_PORT", 3003, 3000, 3999), 3003)

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
