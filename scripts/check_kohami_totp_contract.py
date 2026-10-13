"""Validate the TOTP proposal only; never compute OTPs, create secrets or run a product."""
from pathlib import Path
import json
import re
import yaml
from jsonschema import Draft202012Validator, FormatChecker
from openapi_spec_validator import validate

ROOT = Path(__file__).resolve().parents[1]
PLAN = ROOT / 'docs/planning/kohami'
api = yaml.safe_load((PLAN / 'API_TOTP_MFA.openapi.yaml').read_text())
validate(api)
assert api['x-authority'] == 'PROPOSED'
assert api['x-mfa-method-decision'] == 'ACCEPTED_PASSWORD_TOTP'
assert api['x-product-tests'] == 'NOT_EXECUTED'
schemas = api['components']['schemas']

def walk(value):
    if isinstance(value, dict):
        yield value
        for child in value.values():
            yield from walk(child)
    elif isinstance(value, list):
        for child in value:
            yield from walk(child)

def validator(name):
    return Draft202012Validator({**api, '$ref': '#/components/schemas/' + name},
                               format_checker=FormatChecker())

examples = 0
for name, schema in schemas.items():
    if schema.get('x-sensitive-schema'):
        assert 'examples' not in schema, name
    else:
        assert schema.get('examples'), name
        for example in schema['examples']:
            validator(name).validate(example)
            examples += 1
for node in walk(api):
    if '$ref' in node:
        assert node['$ref'].startswith('#/'), 'no network refs'
        target = api
        for part in node['$ref'][2:].split('/'):
            target = target[part.replace('~1', '/').replace('~0', '~')]
    if node.get('x-sensitive'):
        assert not any(k in node for k in ('example', 'examples', 'default')), 'no secret examples'

ids = set()
for path, item in api['paths'].items():
    for method, op in item.items():
        assert op['operationId'] not in ids
        ids.add(op['operationId'])
        assert op['security'] and all(entry for entry in op['security'])
        assert op['x-product-tests'] == 'NOT_EXECUTED'
        assert op['x-business-idempotency'] is False
        for response in op['responses'].values():
            assert response['headers']['Cache-Control']['schema']['const'] == 'no-store'
        assert 'Retry-After' in op['responses']['429']['headers']
        if method == 'post':
            assert op['requestBody']['required']
            params = [api['components']['parameters'][v['$ref'].split('/')[-1]]
                      for v in op['parameters']]
            names = [v['name'].lower() for v in params]
            assert len(names) == len(set(names))
            assert {'x-csrf-token', 'origin'} <= set(names)
        if op.get('x-sensitive-response-once'):
            output = op['responses']['200']['content']['application/json']['schema']['$ref'].split('/')[-1]
            assert schemas[output]['x-sensitive-schema']
assert len(ids) == 9
assert api['paths']['/auth/mfa-verifications']['post']['security'] == [{'AdminPasswordStageCookie': []}]
assert api['paths']['/auth/mfa-recoveries/confirm']['post']['security'] == [{'AdminPasswordStageCookie': []}]
assert api['paths']['/auth/reauthentications']['post']['security'] == [{'SessionCookie': []}]
assert api['paths']['/auth/mfa-recoveries']['post']['responses']['200']['content']['application/json']['schema']['$ref'].endswith('/RecoveryChallenge')
for name in ['Context', 'AdminSession', 'RecentProof', 'RebindContext', 'Challenge', 'RecoveryChallenge']:
    assert not {'sharedKey', 'recoveryCodes', 'password', 'code', 'hash'} & set(schemas[name]['properties'])

# Synthetic shape-only values, unattached to any credential. No valid secret material.
proof = {'challengeId': 'challenge_demo', 'requestNonce': 'nonce_demo_1234567890', 'code': '000000'}
validator('LoginProof').validate(proof)  # leading-zero code is a string, not numeric.
negative = [
    ('LoginProof', {**proof, 'code': 0}),
    ('LoginProof', {**proof, 'code': '00000'}),
    ('LoginProof', {**proof, 'code': '0000000'}),
    ('LoginProof', {**proof, 'code': '00 000'}),
    ('LoginProof', {**proof, 'userId': 'other_user'}),
    ('LoginProof', {**proof, 'mfaVerified': True}),
    ('LoginProof', {**proof, 'timeStep': 1}),
    ('LoginProof', {**proof, 'requestNonce': 'short'}),
    ('EnrollmentRequest', {'requestNonce': proof['requestNonce'], 'expectedVersion': 'v1', 'sharedKey': 'NOT_SECRET'}),
    ('RecoveryProof', {**proof, 'recoveryCode': 'not-valid', 'code': None}),
    ('RecoveryProof', {'challengeId': 'challenge_demo', 'requestNonce': proof['requestNonce'], 'recoveryCode': ''}),
    ('RebindContext', {'stage': 'RecoveryRebind', 'permissions': ['refund.execute'], 'expiresAt': '2026-10-13T07:00:00Z', 'version': 'v1'}),
    ('RotationRequest', {'confirmed': False, 'expectedVersion': 'v1', 'requestNonce': proof['requestNonce']}),
    ('ReauthChallengeRequest', {'purpose': 'disable.mfa', 'resourceKind': 'User', 'resourceId': 'user_demo', 'requestNonce': proof['requestNonce']}),
    ('AdminSession', {**schemas['AdminSession']['examples'][0], 'state': 'AdminMfaRequired'}),
    ('ReauthChallengeRequest', {'purpose': 'refund.execute', 'resourceKind': 'User', 'resourceId': 'user_demo', 'requestNonce': proof['requestNonce']}),
    ('ReauthChallengeRequest', {'purpose': 'recovery.rotate', 'resourceKind': 'Dataset', 'resourceId': 'dataset_demo', 'requestNonce': proof['requestNonce']}),
]
for name, value in negative:
    assert not validator(name).is_valid(value), name
ids_in_doc = re.findall(r'^\| (TOTP-AC\d+) \|', (PLAN / 'TOTP_MFA_LIFECYCLE_PLAN.md').read_text(), re.M)
assert ids_in_doc == ['TOTP-AC%02d' % i for i in range(1, 15)]
print(json.dumps({'result': 'PASS', 'scope': 'proposal_document_only', 'operations': len(ids),
                  'schemas': len(schemas), 'nonsensitive_examples': examples,
                  'negative_schema_cases': len(negative), 'product_tests': 'NOT_EXECUTED'}))
