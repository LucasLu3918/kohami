"""Publication regressions use generated credentials and isolated Git repositories."""
import importlib.util
import io
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
import zipfile

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "scripts/security"))
SPEC = importlib.util.spec_from_file_location("publication_scanner", ROOT / "scripts/security/check_secret_leakage.py")
SCANNER = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(SCANNER)


def package(document="<document><text>Public plan</text></document>", extra=None):
    output = io.BytesIO()
    with zipfile.ZipFile(output, "w", zipfile.ZIP_DEFLATED) as archive:
        archive.writestr("[Content_Types].xml", "<Types/>")
        archive.writestr("_rels/.rels", "<Relationships/>")
        archive.writestr("word/document.xml", document)
        for name, payload in (extra or {}).items():
            archive.writestr(name, payload)
    return output.getvalue()


class PublicationSecretScanTests(unittest.TestCase):
    def setUp(self):
        self.policy, _ = SCANNER.load_policy(ROOT / "docs/quality/SECRET_SCAN_POLICY.yaml")

    def scan_docx(self, payload):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "plan.docx").write_bytes(payload)
            return SCANNER.scan_worktree(root, self.policy, strict=True, paths=[])

    def test_clean_docx_is_inspected_instead_of_rejected_as_binary(self):
        self.assertEqual(self.scan_docx(package()), ([], []))

    def test_docx_text_split_across_runs_and_metadata_are_scanned_and_redacted(self):
        token = "gh" + "p_" + "Ab3Cd4Ef5Gh6Ij7Kl8Mn9Op0Qr1St2Uv3"
        document = f"<document><text>{token[:15]}</text><text>{token[15:]}</text></document>"
        findings, blockers = self.scan_docx(package(document, {"docProps/core.xml": f"<core>{token}</core>"}))
        self.assertFalse(blockers)
        self.assertTrue(any(item["detector"] == "github-token" for item in findings))
        self.assertTrue(any("docProps/core.xml" in item["path"] for item in findings))
        self.assertNotIn(token, str(findings))

    def test_invalid_unsafe_oversized_and_opaque_packages_fail_closed(self):
        cases = [b"not a package", package(extra={"../escape.xml": "<x/>"}),
                 package(extra={"word/embeddings/object.bin": b"opaque"}),
                 package("<!DOCTYPE document [<!ENTITY x 'hidden'>]><document>&x;</document>"),
                 package(extra={"word/broken.xml": "<broken>"}),
                 package(extra={"word/large.xml": "<x>" + "a" * 8_000_000 + "</x>"})]
        for payload in cases:
            with self.subTest(size=len(payload)):
                self.assertTrue(self.scan_docx(payload)[1])

    def test_word_thumbnail_null_padding_is_bounded_and_metadata_still_scanned(self):
        token = "gh" + "p_" + "Ab3Cd4Ef5Gh6Ij7Kl8Mn9Op0Qr1St2Uv3"
        comment = token.encode()
        image = (b"\xff\xd8\xff\xfe" + (len(comment) + 2).to_bytes(2, "big") + comment
                 + b"\xff\xc0\x00\x0b\x08\x00\x01\x00\x01\x01\x01\x11\x00"
                 + b"\xff\xda\x00\x08\x01\x01\x00\x00\x3f\x00\x01\xff\xd9")
        findings, blockers = self.scan_docx(package(extra={"docProps/thumbnail.jpeg": image + b"\x00"}))
        self.assertFalse(blockers)
        self.assertTrue(any(item["detector"] == "github-token" for item in findings))
        self.assertNotIn(token, str(findings))
        for tail in [b"\x00" * 4, b"hidden"]:
            self.assertTrue(self.scan_docx(package(extra={"docProps/thumbnail.jpeg": image + tail}))[1])

    def test_initial_publication_scans_root_tree_and_message_even_after_deletion(self):
        token = "gh" + "p_" + "Ab3Cd4Ef5Gh6Ij7Kl8Mn9Op0Qr1St2Uv3"
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)

            def git(*args):
                return subprocess.check_output(["git", "-C", str(root), *args], stderr=subprocess.DEVNULL).decode().strip()

            git("init", "-q")
            git("config", "user.name", "Publication Test")
            git("config", "user.email", "publication@example.test")
            (root / "plan.docx").write_bytes(package(f"<document>{token}</document>"))
            git("add", ".")
            git("commit", "-qm", "initial " + token)
            base = git("rev-parse", "HEAD")
            git("rm", "plan.docx")
            (root / "README.md").write_text("Public project\n")
            git("add", ".")
            git("commit", "-qm", "remove old plan")
            findings, blockers, _, _ = SCANNER.scan_candidate(root, base, "HEAD", self.policy)
            self.assertFalse(blockers)
            self.assertTrue(any("plan.docx" in item["path"] and item.get("revision") == base for item in findings))
            self.assertTrue(any(item["path"] == "<commit-message>" and item.get("revision") == base for item in findings))
            self.assertNotIn(token, str(findings))

    def test_reviewed_expression_allowlist_never_allows_a_literal_credential(self):
        source = (ROOT / "tests/Commerce/Issue25AuthMatrixAcceptance.cs").read_text()
        self.assertFalse(SCANNER.scan_text(source, "tests/Commerce/Issue25AuthMatrixAcceptance.cs", self.policy, strict=True))
        value = "Ab3Cd4Ef5Gh6Ij7Kl8Mn9Op0Qr1St2Uv3"
        findings = SCANNER.scan_text('password="' + value + '"', "tests/Commerce/Issue25AuthMatrixAcceptance.cs", self.policy, strict=True)
        self.assertEqual(len(findings), 1)
        self.assertNotIn(value, str(findings))
