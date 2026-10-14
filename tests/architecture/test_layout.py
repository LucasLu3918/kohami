"""Repository boundary regressions; no private configuration or SQL access."""
import json
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


class ArchitectureTests(unittest.TestCase):
    def test_document_authority_and_migration_map(self):
        self.assertTrue((ROOT / 'docs/README.md').is_file())
        mapping = json.loads((ROOT / 'docs/architecture/PATH_MIGRATION.json').read_text())
        self.assertTrue(mapping['moves'])
        for row in mapping['moves']:
            with self.subTest(path=row['to']):
                self.assertTrue((ROOT / row['to']).is_file())
                self.assertFalse((ROOT / row['from']).is_file())

    def test_domain_projects_have_no_transport_or_storage_dependencies(self):
        projects = list((ROOT / 'src/Modules').glob('*/Domain/*.csproj'))
        self.assertEqual(len(projects), 2)
        for project in projects:
            with self.subTest(project=project):
                tree = ET.parse(project)
                self.assertEqual(tree.findall('.//FrameworkReference'), [])
                self.assertEqual(tree.findall('.//PackageReference'), [])
                self.assertEqual(tree.findall('.//ProjectReference'), [])
                for source in project.parent.glob('*.cs'):
                    code = source.read_text()
                    for forbidden in ['Microsoft.AspNetCore', 'Microsoft.Data.SqlClient', 'Kohami.M0.Core']:
                        self.assertNotIn(forbidden, code)

    def test_project_references_resolve(self):
        for project in list((ROOT / 'src').rglob('*.csproj')) + list((ROOT / 'apps').rglob('*.csproj')) + list((ROOT / 'tools').rglob('*.csproj')) + list((ROOT / 'tests').rglob('*.csproj')):
            with self.subTest(project=project):
                for reference in ET.parse(project).findall('.//ProjectReference'):
                    self.assertTrue((project.parent / reference.attrib['Include']).is_file())


if __name__ == '__main__':
    unittest.main()
