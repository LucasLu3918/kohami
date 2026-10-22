from pathlib import Path
import re
import unittest
import yaml

ROOT = Path(__file__).resolve().parents[2]


class GitHubValidationWorkflowTests(unittest.TestCase):
    def test_required_job_always_runs_and_checks_the_exact_candidate(self):
        source = (ROOT / '.github/workflows/validate.yml').read_text()
        workflow = yaml.safe_load(source)
        triggers = workflow.get('on', workflow.get(True))
        self.assertIn('pull_request', triggers)
        self.assertEqual(triggers['push']['branches'], ['main'])
        self.assertNotIn('paths', triggers['pull_request'] or {})
        self.assertEqual(workflow['permissions'], {'contents': 'read'})
        job = workflow['jobs']['repository']
        self.assertNotIn('if', job)
        steps = job['steps']
        for step in steps:
            if 'uses' in step:
                self.assertRegex(step['uses'], r'^[\w-]+/[\w-]+@[a-f0-9]{40}$')
        checkout = next(step for step in steps if step.get('uses', '').startswith('actions/checkout@'))
        self.assertEqual(checkout['with']['fetch-depth'], 0)
        commands = '\n'.join(step.get('run', '') for step in steps)
        self.assertIn('scripts/security/integration_gate.py', commands)
        self.assertIn('--base "$BASE_SHA"', commands)
        self.assertIn('--head "$HEAD_SHA"', commands)
        self.assertIn('git rev-parse HEAD', commands)
        self.assertIn('npm run test:data-source', commands)
        self.assertIn('npm run build:public-demo', commands)
        self.assertNotIn('continue-on-error', source)
        self.assertNotIn('pull_request_target', triggers)
