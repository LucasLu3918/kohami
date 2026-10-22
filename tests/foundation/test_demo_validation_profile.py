import importlib.util
import copy
import json
import subprocess
import sys
import tempfile
import unittest
from datetime import date
from pathlib import Path

import yaml

ROOT = Path(__file__).resolve().parents[2]
PROFILE_PATH = ROOT / "docs/quality/VALIDATION_PROFILE.yaml"
POLICY_PATH = ROOT / "docs/quality/SECRET_SCAN_POLICY.yaml"
SCANNER_PATH = ROOT / "scripts/security/check_secret_leakage.py"
GATE_PATH = ROOT / "scripts/security/integration_gate.py"
sys.path.insert(0, str(SCANNER_PATH.parent))


class DemoValidationProfileTests(unittest.TestCase):
    def test_candidate_scan_is_required_strict_and_covers_every_path(self):
        profile = yaml.safe_load(PROFILE_PATH.read_text(encoding="utf-8"))
        self.assertEqual(profile.get("version"), 1)
        self.assertTrue(profile["candidate_secret_scan"]["required"])
        scan_id = profile["candidate_secret_scan"]["check_id"]
        check = next(item for item in profile["checks"] if item["id"] == scan_id)
        self.assertTrue(check["required"])
        self.assertTrue(check["candidate_context"])
        self.assertEqual(check["paths"], ["**"])
        self.assertIn("--publication-candidate", check["argv"])
        self.assertIn("--strict", check["argv"])
        self.assertIn(profile["candidate_secret_scan"]["scanner"], check["argv"])
        self.assertIn(profile["candidate_secret_scan"]["policy"], check["argv"])
        self.assertTrue(SCANNER_PATH.is_file())
        self.assertTrue(GATE_PATH.is_file())
        self.assertIn("scripts/security/integration_gate.py", profile["gate_command"])

    def test_project_policy_keeps_strict_detectors_and_bounds_text_size(self):
        policy = yaml.safe_load(POLICY_PATH.read_text(encoding="utf-8"))
        self.assertEqual(policy["version"], 1)
        self.assertEqual(policy["limits"]["max_file_bytes"], 8_000_000)
        keys = [(item["path"], item["detector"], item["fingerprint"]) for item in policy["allowlist"]]
        self.assertEqual(len(keys), len(set(keys)))
        for item in policy["allowlist"]:
            self.assertNotRegex(item["path"], r"[?*\[]")
            self.assertEqual(item["detector"], "generic-secret-assignment")
            self.assertRegex(item["fingerprint"], r"^[a-f0-9]{16}$")
            self.assertTrue(item["reason"])
            self.assertGreaterEqual((date.fromisoformat(item["expires"]) - date.today()).days, 0)
            self.assertLessEqual((date.fromisoformat(item["expires"]) - date.today()).days, 90)
        exception = policy["allowlist"][0]
        self.assertEqual(exception["path"], "tools/native-operations/SqlProvision.cs")
        self.assertEqual(exception["detector"], "generic-secret-assignment")
        self.assertEqual(exception["fingerprint"], "ee3507ab580b3714")
        self.assertEqual(exception["expires"], "2027-01-19")
        self.assertTrue(exception["reason"])
        self.assertEqual(policy["generated_paths"], [
            "**/.git/**", "**/.venv/**", "**/venv/**", "**/node_modules/**",
            "**/vendor/**", "**/dist/**", "**/build/**", "**/coverage/**",
            "**/.next/**", "**/.cache/**", "**/.ruff_cache/**",
            "**/.pytest_cache/**", "**/target/**", "**/__pycache__/**",
        ])
        self.assertEqual(policy["lockfiles"]["disabled_detectors"], ["generic-secret-assignment"])
        self.assertEqual(policy["lockfiles"]["path_patterns"], [
            "**/package-lock.json", "**/yarn.lock", "**/pnpm-lock.yaml",
            "**/poetry.lock", "**/Pipfile.lock", "**/*.lock",
        ])
        spec = importlib.util.spec_from_file_location("demo_secret_scanner", SCANNER_PATH)
        scanner = importlib.util.module_from_spec(spec)
        assert spec.loader is not None
        spec.loader.exec_module(scanner)
        self.assertEqual(
            {name for name, _ in scanner.PATTERNS},
            {"private-key", "aws-access-key", "github-token", "slack-token", "stripe-live-key"},
        )
        assignment_key = "pass" + "word"
        self.assertIsNotNone(scanner.ASSIGNMENT.search(f"{assignment_key}={'A' * 24}"))

    def test_exact_ddl_fingerprint_is_allowed_but_changed_and_other_path_findings_fail(self):
        policy = yaml.safe_load(POLICY_PATH.read_text(encoding="utf-8"))
        allowed = policy["allowlist"][0]
        expiry = date.fromisoformat(allowed["expires"])
        self.assertGreaterEqual((expiry - date.today()).days, 0)
        self.assertLessEqual((expiry - date.today()).days, 90)

        def scan(root, relative_path):
            return subprocess.run(
                [
                    sys.executable, str(SCANNER_PATH), "--root", str(root),
                    "--policy", str(POLICY_PATH), "--strict", "--path", relative_path, "--json",
                ],
                cwd=ROOT,
                capture_output=True,
                text=True,
                check=False,
            )

        source = (ROOT / allowed["path"]).read_text(encoding="utf-8")
        exact = scan(ROOT, allowed["path"])
        self.assertEqual(exact.returncode, 0, exact.stderr)
        self.assertEqual(json.loads(exact.stdout)["findings"], [])

        ddl_lines = source.splitlines(keepends=True)
        matching_lines = [i for i, line in enumerate(ddl_lines) if "CREATE LOGIN" in line and "@password" in line.lower()]
        self.assertGreaterEqual(len(matching_lines), 1)
        index = matching_lines[0]
        self.assertIn("@password", ddl_lines[index].lower())
        ddl_lines[index] = ddl_lines[index].replace("@password", "@credential", 1)

        with tempfile.TemporaryDirectory() as tmp:
            temp_root = Path(tmp)
            changed_path = temp_root / allowed["path"]
            changed_path.parent.mkdir(parents=True)
            changed_path.write_text("".join(ddl_lines), encoding="utf-8")
            changed = scan(temp_root, allowed["path"])
            self.assertEqual(changed.returncode, 1)
            changed_findings = json.loads(changed.stdout)["findings"]
            self.assertEqual(len(changed_findings), 1)
            self.assertNotEqual(changed_findings[0]["fingerprint"], allowed["fingerprint"])
            self.assertNotIn("@credential", changed.stdout)

            other_path = temp_root / "tools/native-operations/Other.cs"
            other_path.write_text(source, encoding="utf-8")
            other = scan(temp_root, "tools/native-operations/Other.cs")
            self.assertEqual(other.returncode, 1)
            other_findings = json.loads(other.stdout)["findings"]
            self.assertTrue(any(item["fingerprint"] == allowed["fingerprint"] for item in other_findings))
            self.assertTrue(all(item["path"] == "tools/native-operations/Other.cs" for item in other_findings))

    def test_project_gate_resolves_git_top_level_from_invocation_directory(self):
        spec = importlib.util.spec_from_file_location("demo_integration_gate", GATE_PATH)
        gate = importlib.util.module_from_spec(spec)
        assert spec.loader is not None
        spec.loader.exec_module(gate)
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            nested = root / "nested" / "checkout"
            nested.mkdir(parents=True)
            subprocess.run(["git", "init", "-q", str(root)], check=True)
            actual = gate.resolve_repository_root(nested)
            self.assertEqual(actual, root.resolve())

    def test_profile_builds_release_outputs_consumed_by_kohami_tests(self):
        profile = yaml.safe_load(PROFILE_PATH.read_text(encoding="utf-8"))
        checks = profile["checks"]
        build_index = next(i for i, item in enumerate(checks) if item["id"] == "release-build")
        tests_index = next(i for i, item in enumerate(checks) if item["id"] == "local-foundation-tests")
        build = checks[build_index]
        self.assertLess(build_index, tests_index)
        self.assertIn("--configuration", build["argv"])
        self.assertIn("Release", build["argv"])
        self.assertNotIn("--artifacts-path", build["argv"])

    def test_api_source_requires_release_build_and_kohami_contract_checks(self):
        spec = importlib.util.spec_from_file_location("demo_coverage_gate", GATE_PATH)
        gate = importlib.util.module_from_spec(spec)
        assert spec.loader is not None
        spec.loader.exec_module(gate)
        profile = yaml.safe_load(PROFILE_PATH.read_text(encoding="utf-8"))
        files = ["apps/api/Program.cs"]
        checks = gate.profile_checks(profile, files)
        required = gate.validate_required_coverage(profile, checks, files)
        self.assertEqual(required, {"release-build", "local-foundation-tests"})
        by_id = {item["id"]: item for item in checks}
        self.assertTrue(by_id["release-build"]["applies"])
        self.assertTrue(by_id["local-foundation-tests"]["applies"])
        self.assertEqual(
            gate.required_check_failures(
                [{"id": "local-foundation-tests", "required": True, "status": "SKIPPED"}], required,
            ),
            ["local-foundation-tests"],
        )

        narrowed = copy.deepcopy(profile)
        by_id = {item["id"]: item for item in narrowed["checks"]}
        by_id["local-foundation-tests"]["paths"] = ["tests/foundation/**"]
        with self.assertRaisesRegex(gate.GateError, "coverage"):
            gate.validate_required_coverage(narrowed, gate.profile_checks(narrowed, files), files)

        removed = copy.deepcopy(profile)
        removed["checks"] = [item for item in removed["checks"] if item["id"] != "local-foundation-tests"]
        with self.assertRaisesRegex(gate.GateError, "coverage"):
            gate.validate_required_coverage(removed, gate.profile_checks(removed, files), files)

    def test_storefront_source_requires_typecheck_and_build_and_coverage_cannot_shrink(self):
        spec = importlib.util.spec_from_file_location("demo_storefront_coverage_gate", GATE_PATH)
        gate = importlib.util.module_from_spec(spec)
        assert spec.loader is not None
        spec.loader.exec_module(gate)
        profile = yaml.safe_load(PROFILE_PATH.read_text(encoding="utf-8"))
        files = ["apps/storefront/app/pages/index.vue"]
        checks = gate.profile_checks(profile, files)
        required = gate.validate_required_coverage(profile, checks, files)
        self.assertEqual(required, {"storefront-dependencies", "storefront-typecheck", "storefront-build"})
        by_id = {item["id"]: item for item in checks}
        self.assertTrue(by_id["storefront-typecheck"]["applies"])
        self.assertTrue(by_id["storefront-build"]["applies"])

        narrowed = copy.deepcopy(profile)
        by_id = {item["id"]: item for item in narrowed["checks"]}
        by_id["storefront-build"]["paths"] = ["docs/**"]
        with self.assertRaisesRegex(gate.GateError, "coverage"):
            gate.validate_required_coverage(narrowed, gate.profile_checks(narrowed, files), files)

        removed = copy.deepcopy(profile)
        removed["required_coverage"].pop("storefront")
        with self.assertRaisesRegex(gate.GateError, "coverage"):
            gate.validate_required_coverage(removed, gate.profile_checks(removed, files), files)

    def test_candidate_scan_cannot_narrow_repository_root_or_selected_paths(self):
        spec = importlib.util.spec_from_file_location("demo_scan_binding_gate", GATE_PATH)
        gate = importlib.util.module_from_spec(spec)
        assert spec.loader is not None
        spec.loader.exec_module(gate)
        profile = yaml.safe_load(PROFILE_PATH.read_text(encoding="utf-8"))
        checks = gate.profile_checks(profile, ["apps/api/Program.cs"])
        gate.candidate_secret_scan_binding(profile, checks, ROOT)

        for argv in (
            [arg if arg != "." else "apps/api" for arg in next(c for c in checks if c["id"] == "mandatory-candidate-secret-scan")["argv"]],
            next(c for c in checks if c["id"] == "mandatory-candidate-secret-scan")["argv"] + ["--path", "apps/storefront/**"],
        ):
            narrowed = copy.deepcopy(profile)
            scan = next(c for c in narrowed["checks"] if c["id"] == "mandatory-candidate-secret-scan")
            scan["argv"] = argv
            with self.assertRaisesRegex(gate.GateError, "root|path|all changed"):
                gate.candidate_secret_scan_binding(narrowed, gate.profile_checks(narrowed, ["apps/api/Program.cs"]), ROOT)


if __name__ == "__main__":
    unittest.main()
