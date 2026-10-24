"""Check asset workflow proposal contracts; no PNG, SQL or actual rights execution."""
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
PLAN = ROOT / 'tests/fixtures/kohami-legacy/docs/planning/kohami'
api = yaml.safe_load((PLAN / 'contracts/API_ASSET_WORKFLOW.openapi.yaml').read_text())
validate(api)
assert api['x-authority'] == 'PROPOSED' and api['x-product-tests'] == 'OUT_OF_SCOPE_OPTION'
assert api['x-engineering-limits-adopted'] is False
assert api['x-identity-overlay-activation'].startswith('BLOCKED_')
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
    for ex in schema['examples']:
        json.dumps(ex, allow_nan=False)
        validator(name).validate(ex)
        if 'validFrom' in ex:
            assert datetime.fromisoformat(ex['validFrom']) < datetime.fromisoformat(ex['validUntil'])
        examples += 1
for node in walk(api):
    if '$ref' in node:
        assert node['$ref'].startswith('#/'), 'local namespace references only'
        target = api
        for part in node['$ref'][2:].split('/'):
            target = target[part.replace('~1','/').replace('~0','~')]
manifest = json.loads((PLAN / 'fitting/FITTING_IMPORT_MANIFEST.schema.json').read_text())
assert {k:v for k,v in schemas['ImportManifest'].items() if k != 'x-product-tests'} == {k:v for k,v in manifest.items() if k != 'x-product-tests'}
fit = yaml.safe_load((PLAN / 'contracts/API_FITTING.openapi.yaml').read_text())
for name in ['AdminAsset', 'RightsSummary', 'Selection', 'AdminCompatibility']:
    assert schemas[name] == fit['components']['schemas'][name]

ops = []
overlay = yaml.safe_load((PLAN / 'fitting/FITTING_IDENTITY_OVERLAY.yaml').read_text())
for path, item in api['paths'].items():
    for method, op in item.items():
        ops.append(op)
        assert path.startswith('/admin/fitting/')
        assert op['security'] == [{'SessionCookie': []}]
        auth = op['x-authorization']
        assert auth['stage'] == 'owner'
        params = [api['components']['parameters'][v['$ref'].split('/')[-1]] for v in op['parameters']]
        names = {v['name'].lower() for v in params}
        assert len(names) == len(params)
        for token in re.findall(r'\{([^}]+)\}', path):
            assert any(v['in']=='path' and v['name']==token and v['required'] for v in params)
        if method != 'get':
            assert {'x-csrf-token', 'origin', 'idempotency-key'} <= names
            assert auth['businessIdempotency'] is True
            if 'x-command-authorization' in op:
                for mapping in op['x-command-authorization'].values():
                    assert mapping['resourceKind'] in {'Dataset', 'AssetRevision', 'Compatibility'}
                    assert mapping['permission'] in overlay['permission_additions']
            else:
                assert auth['resourceKind'] in {'Dataset', 'AssetRevision', 'Compatibility'}
        else:
            assert auth['permission'] == 'fitting.read'
        for resp in op['responses'].values():
            if '$ref' in resp:
                resp = api['components']['responses'][resp['$ref'].split('/')[-1]]
            assert resp['headers']['Cache-Control']['schema']['const'] == 'no-store'
        assert '429' in op['responses']
assert len(ops) == 16 and len(schemas) == 25
slot_path = '/admin/fitting/imports/{id}/slots/{view}'
assert 'ETag' in api['paths'][slot_path]['get']['responses']['200']['headers']
assert 'image/png' in api['paths'][slot_path]['put']['requestBody']['content']
assert any(v['$ref'].endswith('/SourceHash') for v in api['paths'][slot_path]['put']['parameters'])
assert 'Location' in api['paths']['/admin/fitting/imports/{id}/acceptances']['post']['responses']['201']['headers']
assert set(api['x-event-additions']) == set(schemas['WorkflowEvent']['properties']['eventType']['enum'])
old_events = json.loads((PLAN / 'fitting/FITTING_EVENT_CONTRACT.schema.json').read_text())
assert not set(api['x-event-additions']) & set(old_events['properties']['eventType']['enum'])
for ex in schemas['WorkflowEvent']['examples']:
    assert ex['aggregateKind'] == api['x-event-additions'][ex['eventType']]
    if ex['eventType'] == 'AlignmentCandidateCreated':
        assert ex['aggregateId'] == ex['payload']['alignmentRevisionId']
    if ex['eventType'] == 'CompatibilityDraftCreated':
        assert ex['aggregateId'] == ex['payload']['compatibilityId']
assert 'garmentRevisionId' not in schemas['AlignmentCreate']['properties'], 'avoid import/alignment cycle'

operation_keys = set()
operation_ids = set()
for file in ['contracts/API_M1_M3.openapi.yaml','contracts/API_ACCOUNT_ACCESS.openapi.yaml','contracts/API_FITTING.openapi.yaml','contracts/API_ASSET_WORKFLOW.openapi.yaml']:
    doc = yaml.safe_load((PLAN/file).read_text())
    assert doc['components']['securitySchemes']['SessionCookie']['name'] == 'kohami.session'
    for path, item in doc['paths'].items():
        for method, op in item.items():
            assert (method,path) not in operation_keys
            assert op['operationId'] not in operation_ids
            operation_keys.add((method,path));operation_ids.add(op['operationId'])
assert len(operation_keys) == 131

negative = []
def bad(name, mutate):
    value = deepcopy(schemas[name]['examples'][0]);mutate(value)
    assert list(validator(name).iter_errors(value)), (name,value)
    negative.append(name)
bad('ImportCreate', lambda v:v.update(nonAiAttestation=False))
bad('ImportCreate', lambda v:v.update(assetKind='GarmentViewSet'))
bad('ImportCreate', lambda v:v.update(memberId='another_member'))
bad('ImportCreate', lambda v:v.update(sourceUrl='https://example.test/arbitrary.png'))
bad('ImportCreate', lambda v:v.update(rightsEvidenceRefs=[]))
bad('ImportSlot', lambda v:v.update(view='Left'))
bad('ImportSlot', lambda v:v.update(state='Empty',sourceSha256='0'*64))
bad('ImportSlot', lambda v:v.update(state='Validated'))
bad('ImportSession', lambda v:v.update(slots=v['slots'][:2]))
bad('ImportSession', lambda v:v.update(slots=[v['slots'][0]]*3))
bad('ImportSession', lambda v:v.update(state='Accepted'))
bad('ImportSession', lambda v:v.update(acceptedAssetRevisionId='asset_bypass'))
bad('Completion', lambda v:v.pop('expectedSessionVersion'))
bad('Completion', lambda v:v['manifest'].update(files=v['manifest']['files'][:2]))
bad('Acceptance', lambda v:v.update(confirmCreateDraft=False))
bad('Acceptance', lambda v:v.update(state='Approved'))
bad('Abort', lambda v:v.update(reason=''))
bad('SetupContext', lambda v:v.update(policyVersion='fake_default'))
bad('SetupContext', lambda v:v.update(policyConfigured=True))
bad('AlignmentCreate', lambda v:v.update(garmentRevisionId='circular_dependency'))
bad('AlignmentCreate', lambda v:v.update(coordinateConvention='ARBITRARY_SCRIPT'))
bad('AlignmentCreate', lambda v:v.update(views=[v['views'][0]]*3))
bad('AlignmentCreate', lambda v:v['views'][0].update(scaleX=0))
bad('AlignmentCreate', lambda v:v['views'][0].update(offsetX=2))
bad('AlignmentCreate', lambda v:v['views'][0].update(script='eval()'))
bad('Alignment', lambda v:v.update(state='Approved'))
bad('RightsCreate', lambda v:v.update(validUntil=None))
bad('RightsCreate', lambda v:v.update(evidenceReferences=[]))
bad('RightsCreate', lambda v:v.update(state='Active'))
bad('RightsRecord', lambda v:v.update(selectedForDelivery=True))
bad('RightsRecord', lambda v:v.update(state='Revoked',selectedForDelivery=True))
bad('RightsTransition', lambda v:v.update(command='ReactivateRevoked'))
bad('RightsTransition', lambda v:v.update(reviewEvidenceReferences=[]))
bad('CompatibilityCreate', lambda v:v.pop('expectedAlignmentVersion'))
bad('CompatibilityCreate', lambda v:v.update(state='Approved'))
bad('WorkflowEvent', lambda v:v.update(aggregateKind='Dataset'))
bad('WorkflowEvent', lambda v:v['payload'].update(memberId='private'))
bad('WorkflowEvent', lambda v:v.update(schemaVersion=2))
bad('ImportSlot', lambda v:v.update(state='Uploaded'))
bad('ImportSession', lambda v:v.update(state='Validated'))
bad('AcceptanceResult', lambda v:v['asset'].update(state='Approved'))
# Check creation response variants cannot claim publication or rights activation.
for path,name,mutate in [
    ('/admin/fitting/assets/{id}/rights-records','RightsRecord',lambda v:v.update(state='Active',selectedForDelivery=True)),
    ('/admin/fitting/compatibilities','AdminCompatibility',lambda v:v.update(state='Approved'))]:
    shape=api['paths'][path]['post']['responses']['201']['content']['application/json']['schema']
    v=deepcopy(schemas[name]['examples'][0])
    Draft202012Validator({**api,**shape},format_checker=FormatChecker()).validate(v)
    mutate(v)
    assert list(Draft202012Validator({**api,**shape},format_checker=FormatChecker()).iter_errors(v))
    negative.append('creation-response')
plan = (PLAN/'fitting/ASSET_WORKFLOW_API_INTEGRATION_PLAN.md').read_text()
ids = re.findall(r'\| (AAPI-AC\d{2}) \|', plan)
assert len(ids)==len(set(ids))==10
print(json.dumps({'result':'PASS','scope':'proposal_documents_only','operations':16,'schemas':25,'synthetic_examples':examples,'negative_schema_cases':len(negative),'cross_document_operations':131,'source_baselines':'UNCHANGED','new_event_variants':3,'activation':'BLOCKED','product_tests':'NOT_EXECUTED'}))
