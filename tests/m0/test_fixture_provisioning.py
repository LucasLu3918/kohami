import importlib.util
from pathlib import Path
from subprocess import TimeoutExpired
from tempfile import TemporaryDirectory
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

    def test_wallet03_http_fixture_has_its_own_default_profile_and_ports(self):
        self.assertEqual(PROVISIONING.PURPOSES, {"issue02", "issue06", "wallet03-http"})
        self.assertEqual(PROVISIONING.required_ports_for("wallet03-http", 14337), (14337,))
        with TemporaryDirectory() as temporary:
            root = Path(temporary)
            (root / ".local").mkdir()
            self.assertEqual(PROVISIONING.resolve_local_dir(root, None, "issue06"), root / ".local" / "m0")
            self.assertEqual(PROVISIONING.resolve_local_dir(root, None, "wallet03-http"), root / ".local" / "kohami-demo" / "wallet03-http")
        self.assertEqual(PROVISIONING.default_port_for("wallet03-http"), 14350)
        self.assertEqual(PROVISIONING.default_port_for("issue02"), 14336)
        self.assertFalse(PROVISIONING.valid_wallet03_http_port(14349))
        self.assertTrue(PROVISIONING.valid_wallet03_http_port(14350))

    def test_profile_directory_must_be_new_under_repo_local(self):
        with TemporaryDirectory() as temporary:
            root = Path(temporary)
            (root / ".local").mkdir()
            candidate = root / ".local" / "nested" / "profile"
            self.assertEqual(PROVISIONING.validate_local_dir(root, candidate), candidate)
            for invalid in (root / ".local", root.parent / "outside"):
                with self.subTest(candidate=invalid), self.assertRaises(RuntimeError):
                    PROVISIONING.validate_local_dir(root, invalid)

    def test_missing_parent_is_created_but_fixture_leaf_is_reserved_separately(self):
        with TemporaryDirectory() as temporary:
            root = Path(temporary)
            (root / ".local").mkdir()
            local = root / ".local" / "kohami-demo" / "wallet03-http"
            PROVISIONING.ensure_local_parent(local)
            self.assertTrue(local.parent.is_dir())
            self.assertFalse(local.exists())

    def test_profile_directory_refuses_existing_path_and_symlink(self):
        with TemporaryDirectory() as temporary:
            root = Path(temporary)
            (root / ".local").mkdir()
            existing = root / ".local" / "existing"
            existing.mkdir()
            with self.assertRaisesRegex(RuntimeError, "REFUSE_EXISTING_LOCAL_MATERIAL"):
                PROVISIONING.validate_local_dir(root, existing)
            (root / ".local" / "link").symlink_to(existing, target_is_directory=True)
            with self.assertRaisesRegex(RuntimeError, "LOCAL_PROFILE_PARENT_INVALID|REFUSE_EXISTING_LOCAL_MATERIAL"):
                PROVISIONING.validate_local_dir(root, root / ".local" / "link" / "child")

    def test_private_fixture_cleanup_uses_only_the_selected_profile(self):
        selected = Path("/repo/.local/kohami-demo/wallet03-http")
        with patch.object(PROVISIONING.shutil, "rmtree") as remove, patch.object(
            PROVISIONING.Path, "exists", return_value=True
        ):
            with patch.object(PROVISIONING, "local_identity", return_value=(1, 2)):
                PROVISIONING.remove_private_fixture(selected, (1, 2))
        remove.assert_called_once_with(selected)

    def test_cleanup_refuses_a_replaced_profile_directory(self):
        selected = Path("/repo/.local/kohami-demo/wallet03-http")
        with patch.object(PROVISIONING.Path, "is_symlink", return_value=False), patch.object(
            PROVISIONING.Path, "exists", return_value=True
        ), patch.object(PROVISIONING, "local_identity", return_value=(2, 3)), patch.object(
            PROVISIONING.shutil, "rmtree"
        ) as remove:
            with self.assertRaisesRegex(RuntimeError, "REFUSE_FIXTURE_CLEANUP_IDENTITY_CHANGED"):
                PROVISIONING.remove_private_fixture(selected, (1, 2))
        remove.assert_not_called()

    def test_project_names_are_collision_checked_and_purpose_scoped(self):
        with patch.object(PROVISIONING.uuid, "uuid4") as uuid4:
            uuid4.side_effect = [
                SimpleNamespace(hex="a" * 32),
                SimpleNamespace(hex="b" * 32),
            ]
            with patch.object(PROVISIONING, "existing_compose_projects", return_value={"kohami-wallet03-http-" + "a" * 12, "other"}):
                self.assertEqual(
                    PROVISIONING.unique_compose_project("wallet03-http"),
                    "kohami-wallet03-http-" + "b" * 12,
                )

    def test_native_fixture_config_marks_only_wallet03_http_purpose(self):
        local = Path("/repo/.local/kohami-demo/wallet03-http")
        self.assertEqual(
            PROVISIONING.native_profile_options(local),
            ["--local-dir", str(local)],
        )
        self.assertEqual(
            PROVISIONING.native_profile_options(local, "wallet03-http"),
            ["--local-dir", str(local), "--fixture-purpose", "wallet03-http"],
        )
        self.assertEqual(
            ["wallet03-http-activate", "/repo", "14337", *PROVISIONING.native_profile_options(local)],
            ["wallet03-http-activate", "/repo", "14337", "--local-dir", str(local)],
        )

    def test_sql_readiness_retries_a_single_probe_timeout(self):
        with patch.object(PROVISIONING.subprocess, "run", side_effect=[
            TimeoutExpired("fixture-status", 15),
            SimpleNamespace(returncode=0),
        ]) as run, patch.object(PROVISIONING.time, "sleep"):
            PROVISIONING.wait_sql_ready({}, 14337, seconds=30)

        self.assertEqual(run.call_count, 2)

    def test_sql_readiness_timeout_reports_only_sanitized_native_diagnostic(self):
        with patch.object(PROVISIONING.subprocess, "run", return_value=SimpleNamespace(
            returncode=1,
            stdout="",
            stderr="Diagnostic type=SqlException sql-number=10060\\nPassword=do-not-print",
        )), patch.object(PROVISIONING.time, "monotonic", side_effect=[0, 0, 0, 0, 100]), patch.object(
            PROVISIONING.time, "sleep"
        ):
            with self.assertRaisesRegex(
                RuntimeError,
                "SQL_FIXTURE_AUTHENTICATED_READY_TIMEOUT:Diagnostic_type=SqlException_sql-number=10060",
            ) as raised:
                PROVISIONING.wait_sql_ready({}, 14350, seconds=90)

        self.assertNotIn("do-not-print", str(raised.exception))


if __name__ == "__main__":
    unittest.main()
