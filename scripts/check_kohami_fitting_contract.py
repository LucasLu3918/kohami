"""Check proposal structure and identity seams; no product, OTP, renderer or SQL execution."""
from pathlib import Path
from copy import deepcopy
import hashlib
import json
import re
import yaml
from jsonschema import Draft202012Validator, FormatChecker
from openapi_spec_validator import validate

ROOT = Path(__file__).resolve().parents[1]
PLAN = ROOT / 'docs/planning/kohami'
api = yaml.safe_load((PLAN / 'contracts/API_FITTING.openapi.yaml').read_text())
overlay = yaml.safe_load((PLAN / 'fitting/FITTING_IDENTITY_OVERLAY.yaml').read_text())
validate(api)
assert api['x-authority'] == overlay['status'] == 'PROPOSED'
assert api['x-product-tests'] == 'OUT_OF_SCOPE_OPTION' and overlay['product_tests'] == 'NOT_EXECUTED'
assert overlay['activation'].startswith('BLOCKED_')
assert api['x-provisional-engineering']['limitsAdopted'] is False
assert api['x-input-baselines'] == overlay['input_baselines']
for path, expected in api['x-input-baselines'].items():
    assert hashlib.sha256((ROOT / path).read_bytes()).hexdigest() == expected, path

schemas = api['components']['schemas']
def validator(name):
    return Draft202012Validator({**api, '$ref': '#/components/schemas/' + name}, format_checker=FormatChecker())
def walk(value):
    if isinstance(value, dict):
        yield value
        for child in value.values():
            yield from walk(child)
    elif isinstance(value, list):
        for child in value:
            yield from walk(child)

examples = 0
for name, schema in schemas.items():
    assert schema.get('examples'), name
    for example in schema['examples']:
        validator(name).validate(example)
        examples += 1
for node in walk(api):
    if '$ref' in node:
        assert node['$ref'].startswith('#/'), 'no cross-document ref namespace assumptions'
        target = api
        for part in node['$ref'][2:].split('/'):
            target = target[part.replace('~1', '/').replace('~0', '~')]
for name in ['ResolveInput', 'SaveInput', 'ExportCurrentSource', 'ExportSavedSource']:
    props = schemas[name]['properties']
    assert not {'memberId', 'userId', 'datasetId', 'photo', 'bodyMeasurements', 'assetUrl', 'html'} & set(props)

operations = []
for path, item in api['paths'].items():
    for method, op in item.items():
        operations.append(op)
        params = [api['components']['parameters'][v['$ref'].split('/')[-1]] for v in op['parameters']]
        names = {v['name'].lower() for v in params}
        assert len(names) == len(params)
        if '{id}' in path:
            assert any(v['in'] == 'path' and v['name'] == 'id' and v['required'] for v in params)
        if method != 'get':
            assert {'x-csrf-token', 'origin'} <= names
        if op['x-authorization']['businessIdempotency']:
            assert 'idempotency-key' in names
        if path.startswith('/admin/'):
            assert op['security'] == [{'SessionCookie': []}]
            assert op['x-authorization']['stage'] == 'owner'
            if method != 'get':
                assert 'if-match' in names
                if 'x-command-authorization' in op:
                    assert op['x-command-authorization'] == overlay['command_authorization']
                else:
                    assert op['x-resource-kind'] in {'Dataset', 'AssetRevision', 'Compatibility'}
        if path.startswith('/account/'):
            assert op['security'] == [{'SessionCookie': []}]
            assert op['x-authorization']['stage'] == 'member'
        for resp in op['responses'].values():
            if '$ref' in resp:
                resp = api['components']['responses'][resp['$ref'].split('/')[-1]]
            assert resp['headers']['Cache-Control']['schema']['const'] == 'no-store'
        assert '429' in op['responses']
assert len(operations) == 20 and len(schemas) == 28
assert 'Set-Cookie' in api['paths']['/fitting/context']['get']['responses']['200']['headers']
assert '/admin/fitting/suspensions' not in api['paths']
for path in ['/admin/fitting/assets/{id}', '/admin/fitting/compatibilities/{id}', '/admin/fitting/control', '/account/outfits/{id}']:
    assert 'ETag' in api['paths'][path]['get']['responses']['200']['headers']
for path in ['/fitting/assets/{id}/content', '/fitting/exports/{id}/content']:
    assert 'image/png' in api['paths'][path]['get']['responses']['200']['content']
assert len(api['paths']['/fitting/exports']['post']['security']) == 2

# Cross-file operation identity and literal cookie consistency, not semantic merging.
all_ops = set()
all_ids = set()
cookies = {}
for name in ['contracts/API_M1_M3.openapi.yaml', 'contracts/API_ACCOUNT_ACCESS.openapi.yaml', 'contracts/API_FITTING.openapi.yaml']:
    doc = yaml.safe_load((PLAN / name).read_text())
    for path, item in doc['paths'].items():
        for method, op in item.items():
            assert (method, path) not in all_ops, (method, path)
            assert op['operationId'] not in all_ids, op['operationId']
            all_ops.add((method, path))
            all_ids.add(op['operationId'])
    for key, scheme in doc['components']['securitySchemes'].items():
        if key in cookies:
            assert cookies[key] == (scheme.get('in'), scheme.get('name'))
        cookies[key] = (scheme.get('in'), scheme.get('name'))
assert len(all_ops) == 115
account = yaml.safe_load((PLAN / 'contracts/API_ACCOUNT_ACCESS.openapi.yaml').read_text())
assert not set(overlay['permission_additions']) & set(account['x-role-permission-allowlist']['Owner'])
for key in overlay['existing_schema_targets']['account']:
    assert key in account['components']['schemas']
for permission, policy in overlay['permission_additions'].items():
    assert policy['roles'] == ['Owner']
    assert set(policy['scopeKinds']) <= {'Dataset', 'AssetRevision', 'Compatibility'}
for command, auth in overlay['command_authorization'].items():
    assert auth['permission'] in overlay['permission_additions']
    assert auth['resourceKind'] in {'Dataset', 'AssetRevision', 'Compatibility'}
assert set(overlay['command_authorization']) == set(schemas['AssetTransition']['properties']['command']['enum'])
assert 'catalog.write' not in set(v['permission'] for v in overlay['command_authorization'].values())

# Nontrivial negative cases: prevent changing ownership/source, fake availability,
# implicit save, unsupported directions and privileged arbitrary transitions.
negative = []
def bad(name, mutate):
    value = deepcopy(schemas[name]['examples'][0])
    mutate(value)
    assert list(validator(name).iter_errors(value)), (name, value)
    negative.append(name)
bad('ResolveInput', lambda v: v.update(memberId='someone_else'))
bad('ResolveInput', lambda v: v.update(view='Left'))
bad('ResolveInput', lambda v: v.pop('bodyProfileRevisionId'))
bad('ResolveInput', lambda v: v.update(assetUrl='https://example.test/file'))
bad('Selection', lambda v: v.pop('garmentRevisionId'))
bad('SaveInput', lambda v: v.update(explicitSave=False))
bad('SaveInput', lambda v: v.update(userId='someone_else'))
bad('ReplacementInput', lambda v: v.update(confirmNewSave=False))
bad('Quota', lambda v: v.update(limit=20))
bad('Quota', lambda v: v.update(configured=True))
bad('FittingAvailability', lambda v: v.update(assets=v['assets'][:2]))
bad('FittingAvailability', lambda v: v.update(assets=[v['assets'][0]] * 3))
bad('FittingAvailability', lambda v: v.update(state='Unsupported'))
bad('Reproduction', lambda v: v.update(state='RightsUnavailable'))
bad('Reproduction', lambda v: v.update(state='ReplacementAvailable', assets=[], reasonCode='ORIGINAL_UNAVAILABLE', rights={'display':False,'transform':False,'download':False}))
bad('Catalog', lambda v: v.update(selectedBodyProfile='body_demo_v1'))
bad('Catalog', lambda v: v.update(families=[v['families'][0]] * 4))
bad('ExportInput', lambda v: v.update(kind='SavedOutfit', outfitId='another'))
bad('ExportSavedSource', lambda v: v.update(selection=schemas['Selection']['examples'][0]))
bad('ExportCurrentSource', lambda v: v.update(html='<svg>fake</svg>'))
bad('Export', lambda v: v.update(state='Ready'))
bad('Export', lambda v: v.update(state='Revoked'))
bad('Export', lambda v: v.update(state='Expired', reasonCode='NONE'))
bad('Export', lambda v: v.update(state='Failed', reasonCode='NONE'))
bad('Export', lambda v: v.update(downloadUrl='https://example.test/public'))
bad('AssetTransition', lambda v: v.update(command='ForceApproved'))
bad('AssetTransition', lambda v: v.update(command='Approve', evidenceReferences=[]))
bad('AssetTransition', lambda v: v.update(reason=''))
bad('CompatibilityTransition', lambda v: v.update(command='Resume', evidenceReferences=[]))
bad('ControlTransition', lambda v: v.update(command='Resume', evidenceReferences=[]))
bad('FittingContext', lambda v: v.update(email='another@example.test'))

plan = (PLAN / 'fitting/FITTING_API_INTEGRATION_PLAN.md').read_text()
ac_ids = re.findall(r'\| (FAPI-AC\d{2}) \|', plan)
assert len(ac_ids) == len(set(ac_ids)) == 10
fixture = yaml.safe_load((PLAN / 'fixtures/FITTING_CASES.yaml').read_text())
assert fixture['product_tests'] == 'NOT_EXECUTED'
assert len(fixture['cases']) == 16 and all(v['status'] == 'NOT_EXECUTED' for v in fixture['cases'])
assert fixture['quota']['adopted'] is False and fixture['quota']['production_limit'] is None
print(json.dumps({'result': 'PASS', 'scope': 'proposal_document_only', 'operations': len(operations), 'schemas': len(schemas), 'nonsensitive_examples': examples, 'negative_schema_cases': len(negative), 'cross_document_operations': len(all_ops), 'source_baselines': 'UNCHANGED', 'overlay_activation': 'BLOCKED', 'product_tests': 'NOT_EXECUTED'}))
