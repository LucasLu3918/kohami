"""Current repository boundary regressions; docs only describe running architecture."""
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


class ArchitectureTests(unittest.TestCase):
    def test_docs_single_authority_and_todo_three_files(self):
        docs = ROOT / "docs"
        self.assertTrue((docs / "README.md").is_file())
        self.assertTrue((docs / "project-overview.html").is_file())
        todo = {p.name for p in (docs / "todo").iterdir() if p.is_file()}
        self.assertEqual({"plan.md", "options.md", "confuse.md"}, todo)
        current = {str(p.relative_to(docs)) for p in docs.rglob("*") if p.is_file() and p.suffix.lower() not in {".svg", ".png", ".jpg", ".jpeg", ".webp"}}
        self.assertLessEqual(len(current), 15)
        self.assertEqual(15, len(current))
        for path in ("architecture.md", "api-spec.md", "database-schema.md", "operations.md", "contracts/runtime/Core.openapi.json", "quality/VALIDATION_PROFILE.yaml", "quality/SECRET_SCAN_POLICY.yaml", "quality/requirements-validation.txt"):
            self.assertTrue((docs / path).is_file(), path)
        creative_docs = {
            "agents/model-image-generation.md": "agents/model-image-generation.md",
            "planning/kohami/experience/VISUAL_SYSTEM.md": "planning/kohami/experience/VISUAL_SYSTEM.md",
        }
        index = (docs / "README.md").read_text()
        for path, link in creative_docs.items():
            self.assertTrue((docs / path).is_file(), path)
            self.assertIn(link, index)
        self.assertTrue((docs / "images/system-architecture.svg").is_file())
        self.assertTrue((docs / "images/order-data-flow.svg").is_file())

    def test_domain_projects_have_no_transport_or_storage_dependencies(self):
        projects = list((ROOT / "src/Modules").glob("*/Domain/*.csproj"))
        self.assertEqual({project.name for project in projects}, {"Catalog.Domain.csproj", "Commerce.Domain.csproj", "IdentityAccess.Domain.csproj"})
        for project in projects:
            with self.subTest(project=project):
                tree = ET.parse(project)
                self.assertEqual(tree.findall(".//FrameworkReference"), [])
                self.assertEqual(tree.findall(".//PackageReference"), [])
                self.assertEqual(tree.findall(".//ProjectReference"), [])
                for source in project.parent.glob("*.cs"):
                    code = source.read_text()
                    for forbidden in ("Microsoft.AspNetCore", "Microsoft.Data.SqlClient", "Kohami.Core"):
                        self.assertNotIn(forbidden, code)

    def test_project_references_resolve(self):
        for project in list((ROOT / "src").rglob("*.csproj")) + list((ROOT / "apps").rglob("*.csproj")) + list((ROOT / "tools").rglob("*.csproj")) + list((ROOT / "tests").rglob("*.csproj")):
            with self.subTest(project=project):
                for reference in ET.parse(project).findall(".//ProjectReference"):
                    self.assertTrue((project.parent / reference.attrib["Include"]).is_file())


if __name__ == "__main__":
    unittest.main()
