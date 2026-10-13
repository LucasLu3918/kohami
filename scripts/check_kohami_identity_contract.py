"""Validate account/access proposals and cross-document seams, never execute identity or SQL."""
from pathlib import Path
from copy import deepcopy
from datetime import datetime
import hashlib
import json
import re
import yaml
from jsonschema import Draft202012Validator, FormatChecker
from openapi_spec_validator import validate

ROOT = Path(__file__).resolve().parents[1]
PLAN = ROOT / 'docs/planning/kohami'
api = yaml.safe_load((PLAN / 'API_ACCOUNT_ACCESS.openapi.yaml').read_text())
validate(api)
assert api['x-authority'] == 'PROPOSED'
assert api['x-product-tests'] == 'NOT_EXECUTED'
assert api['x-mfa-method-decision'] == 'ACCEPTED_PASSWORD_TOTP'
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
    if schema.get('x-sensitive-schema'):
        assert 'examples' not in schema
    else:
        assert schema.get('examples'), name
        for example in schema['examples']:
            validator(name).validate(example)
            examples += 1
            if 'validFrom' in example:
                assert datetime.fromisoformat(example['validFrom']) < datetime.fromisoformat(example['validUntil'])
for node in walk(api):
    if '$ref' in node:
        assert node['$ref'].startswith('#/'), 'local refs only'
        target = api
        for part in node['$ref'][2:].split('/'):
            target = target[part.replace('~1', '/').replace('~0', '~')]
    if node.get('x-sensitive'):
        assert not any(k in node for k in ['examples', 'example', 'default'])

ids = set()
for path, item in api['paths'].items():
    for method, op in item.items():
        assert op['operationId'] not in ids
        ids.add(op['operationId'])
        assert op['security']
        for response in op['responses'].values():
            assert response['headers']['Cache-Control']['schema']['const'] == 'no-store'
        assert 'Retry-After' in op['responses']['429']['headers']
        params = [api['components']['parameters'][v['$ref'].split('/')[-1]] if '$ref' in v else v
                  for v in op.get('parameters', [])]
        names = [v['name'].lower() for v in params]
        assert len(names) == len(set(names))
        if '{id}' in path:
            assert any(v['in'] == 'path' and v['name'] == 'id' and v['required'] for v in params)
        if method != 'get':
            assert {'x-csrf-token', 'origin'} <= set(names)
        if op['x-authorization']['businessIdempotency']:
            assert {'if-match', 'idempotency-key'} <= set(names)
            assert op['x-recent-mfa-purpose'] in ['access.manage', 'user.manage']
        if path.startswith('/admin/'):
            assert op['x-authorization']['stage'] == 'owner'
            assert op['x-authorization']['mfaRequired'] is True
            assert op['security'] == [{'SessionCookie': []}]
        if op.get('x-uniform-public-receipt'):
            assert op['responses']['202']['content']['application/json']['schema']['$ref'].endswith('/UniformReceipt')
            assert 'Set-Cookie' not in op['responses']['202']['headers']
assert len(ids) == 19
assert api['paths']['/auth/member-reauthentications']['post']['x-authorization']['stage'] == 'member-only'
assert api['paths']['/admin/access-grants/{id}']['get']['responses']['200']['headers']['ETag']

fingerprints = api['x-input-baselines']
for path, expected in fingerprints.items():
    assert hashlib.sha256((ROOT / path).read_bytes()).hexdigest() == expected, path
names = ['API_M1_M3.openapi.yaml', 'API_TOTP_MFA.openapi.yaml', 'API_ACCOUNT_ACCESS.openapi.yaml']
ops = set()
all_ids = set()
cookie_map = {}
for name in names:
    document = yaml.safe_load((PLAN / name).read_text())
    for path, item in document['paths'].items():
        for method, op in item.items():
            if not isinstance(op, dict) or 'operationId' not in op:
                continue
            assert (method, path) not in ops, (method, path)
            assert op['operationId'] not in all_ids, op['operationId']
            ops.add((method, path)); all_ids.add(op['operationId'])
    for key, value in document['components']['securitySchemes'].items():
        if key in cookie_map:
            assert cookie_map[key] == (value['in'], value['name']), key
        cookie_map[key] = (value['in'], value['name'])
assert len(ops) == 104
mfa = yaml.safe_load((PLAN / 'API_TOTP_MFA.openapi.yaml').read_text())
purposes = set(mfa['components']['schemas']['ReauthChallengeRequest']['properties']['purpose']['enum'])
assert {'access.manage', 'user.manage', 'email.change'} <= purposes
assert 'refund.execute' not in api['x-role-permission-allowlist']['CustomerSupport']
for name in ['Profile', 'AdminUser', 'SecurityContext', 'UniformReceipt', 'PublicAccountContext']:
    assert not {'password', 'proof', 'hash', 'recoveryCodes', 'sharedKey', 'memberId', 'role'} & set(schemas[name]['properties'])

nonce = 'nonce_demo_1234567890'
grant = deepcopy(schemas['GrantCreate']['examples'][0])
proof = {'challengeId': 'challenge_demo', 'proof': 'SYNTHETIC_INVALID_PROOF', 'requestNonce': nonce}
negative = [
 ('GrantCreate', {**grant, 'role': 'Root'}),
 ('GrantCreate', {**grant, 'role': 'Owner', 'permission': 'access.manage', 'scope': {'kind': 'Product', 'id': 'product_demo'}}),
 ('GrantCreate', {**grant, 'permission': 'refund.execute'}),
 ('GrantCreate', {**grant, 'scope': {'kind': 'Dataset', 'id': 'dataset_demo'}}),
 ('GrantCreate', {**grant, 'role': 'Analyst', 'permission': 'reports.read', 'scope': {'kind': 'Order', 'id': 'order_demo'}}),
 ('GrantCreate', {**grant, 'role': 'Merchandiser', 'permission': 'stock.adjust', 'scope': {'kind': 'Product', 'id': 'product_demo'}}),
 ('GrantCreate', {**grant, 'mfaVerified': True}),
 ('GrantCreate', {**grant, 'grantedBy': 'other_actor'}),
 ('GrantCreate', {**grant, 'validUntil': None}),
 ('GrantCreate', {**grant, 'scope': {'kind': 'SqlFilter', 'id': 'all'}}),
 ('GrantCreate', {k: v for k, v in grant.items() if k != 'expectedAccessVersion'}),
 ('ContactProof', {**proof, 'proof': 'short'}),
 ('ContactProof', {**proof, 'requestNonce': 'short'}),
 ('ContactProof', {**proof, 'userId': 'other_user'}),
 ('ResetRequest', {'email': 'buyer@example.com', 'requestNonce': nonce}),
 ('ResetProof', {**proof, 'newPassword': 'short'}),
 ('ResetProof', {**proof, 'newPassword': 'SYNTHETIC_NOT_SECRET_LONG', 'disableMfa': True}),
 ('MemberReauthRequest', {'password': 'SYNTHETIC', 'purpose': 'refund.execute', 'requestNonce': nonce}),
 ('MemberReauthRequest', {'password': 'SYNTHETIC', 'purpose': 'email.change', 'requestNonce': nonce, 'userId': 'other_user'}),
 ('EmailChangeRequest', {'newEmail': 'buyer@example.test', 'requestNonce': nonce, 'memberId': 'other_user'}),
 ('Profile', {**schemas['Profile']['examples'][0], 'passwordHash': 'NOT_A_HASH'}),
 ('UserTransition', {'command': 'DeleteAll', 'reason': 'demo', 'expectedVersion': 'v1'}),
 ('UniformReceipt', {'accepted': True, 'messageCode': 'REQUEST_ACCEPTED', 'challengeId': 'exists'}),
 ('RegistrationRequest', {'email': 'buyer@example.test', 'password': 'SYNTHETIC_NOT_SECRET_LONG', 'privacyNoticeVersion': 'DEMO_NOTICE_v1', 'acknowledgedPrivacyNotice': False, 'requestNonce': nonce}),
 ('RegistrationRequest', {'email': 'buyer@example.test', 'password': 'SYNTHETIC_NOT_SECRET_LONG', 'privacyNoticeVersion': 'DEMO_NOTICE_v1', 'acknowledgedPrivacyNotice': True, 'requestNonce': nonce, 'role': 'Owner'}),
]
for name, value in negative:
    assert not validator(name).is_valid(value), name
ids_in_doc = re.findall(r'^\| (IAPI-AC\d+) \|', (PLAN / 'IDENTITY_API_INTEGRATION_PLAN.md').read_text(), re.M)
assert ids_in_doc == ['IAPI-AC%02d' % i for i in range(1, 11)]
print(json.dumps({'result': 'PASS', 'scope': 'proposal_document_only', 'account_operations': len(ids),
                  'account_schemas': len(schemas), 'nonsensitive_examples': examples,
                  'negative_schema_cases': len(negative), 'cross_document_operations': len(ops),
                  'source_baselines': 'UNCHANGED', 'product_tests': 'NOT_EXECUTED'}))
