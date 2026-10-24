"""Validate proposed import/events and empty pilot evidence; never decode files or run product."""
from pathlib import Path
from copy import deepcopy
import hashlib
import json
import re
import yaml
from jsonschema import Draft202012Validator, FormatChecker

ROOT = Path(__file__).resolve().parents[1]
PLAN = ROOT / 'tests/fixtures/kohami-legacy/docs/planning/kohami'
manifest = json.loads((PLAN / 'fitting/FITTING_IMPORT_MANIFEST.schema.json').read_text())
event = json.loads((PLAN / 'fitting/FITTING_EVENT_CONTRACT.schema.json').read_text())
template = yaml.safe_load((PLAN / 'fitting/FITTING_PILOT_EVIDENCE_TEMPLATE.yaml').read_text())
validators = {}
example_count = 0
for name, doc in [('manifest', manifest), ('event', event)]:
    assert doc['x-authority'] == 'PROPOSED' and doc['x-product-tests'] == 'NOT_EXECUTED'
    Draft202012Validator.check_schema(doc)
    validators[name] = Draft202012Validator(doc, format_checker=FormatChecker())
    for example in doc['examples']:
        validators[name].validate(example)
        example_count += 1
assert manifest['x-number-policy'] == 'ENGINEERING_PROPOSAL_NOT_ADOPTED'
assert event['x-no-private-payload'] is True
assert event['x-consumer-truth'] == 'CURRENT_DATABASE_STATE_NOT_EVENT_SNAPSHOT'
assert len(event['properties']['eventType']['enum']) == 6
assert len(event['examples']) == 6
assert len({v['eventType'] for v in event['examples']}) == 6
assert len({v['eventId'] for v in event['examples']}) == 6
assert len({v['sourceOperationId'] for v in event['examples']}) == 6
for ex in event['examples']:
    if ex['aggregateKind'] == 'Dataset':
        assert ex['aggregateId'] == ex['datasetId']
for ex in manifest['examples']:
    assert len({v['slotId'] for v in ex['files']}) == 3
    assert {v['view'] for v in ex['files']} == {'Front', 'Right', 'Back'}

# Boundary shapes only; source/bytes/dataset ownership and event authority remain runtime checks.
negative = []
def bad(name, mutate, index=0):
    value = deepcopy((manifest if name == 'manifest' else event)['examples'][index])
    mutate(value)
    assert list(validators[name].iter_errors(value)), (name, value)
    negative.append(name)
bad('manifest', lambda v: v.update(nonAiAttestation=False))
bad('manifest', lambda v: v.update(sourceKind='AI_GENERATED'))
bad('manifest', lambda v: v.update(sourceKind='MemberBodyPhoto'))
bad('manifest', lambda v: v.update(files=v['files'][:2]))
bad('manifest', lambda v: v.update(files=[v['files'][0]] * 3))
bad('manifest', lambda v: v['files'][0].update(view='Left'))
bad('manifest', lambda v: v['files'][0].update(slotId='../private.png'))
bad('manifest', lambda v: v['files'][0].update(url='https://example.test/arbitrary.png'))
bad('manifest', lambda v: v['files'][0].update(mediaType='image/svg+xml'))
bad('manifest', lambda v: v['files'][0].update(declaredSourceSha256='not-a-hash'))
bad('manifest', lambda v: v['files'][0].update(declaredBytes=0))
bad('manifest', lambda v: v['files'][0].update(declaredBytes=10485761))
bad('manifest', lambda v: v['files'][0].update(declaredWidth=4097))
bad('manifest', lambda v: v.update(sourceEvidenceRefs=[]))
bad('manifest', lambda v: v.update(rightsEvidenceRefs=[]))
bad('manifest', lambda v: v.update(assetKind='GarmentViewSet'))
bad('manifest', lambda v: v.update(skuId='unexpected_avatar_sku'))
bad('manifest', lambda v: v.update(memberId='another_member'))
bad('manifest', lambda v: v.update(rawFilePath='/private/secret'))
bad('manifest', lambda v: v.update(schemaVersion=2))
bad('manifest', lambda v: v.pop('alignmentRevisionId'), index=1)
bad('event', lambda v: v.update(schemaVersion=2))
bad('event', lambda v: v.update(eventType='UnknownEvent'))
bad('event', lambda v: v.update(aggregateKind='SavedOutfit'))
bad('event', lambda v: v.update(aggregateVersion=0))
bad('event', lambda v: v.update(rightsEpoch=-1))
bad('event', lambda v: v.update(occurredAt='yesterday'))
bad('event', lambda v: v.update(memberId='private_member'))
bad('event', lambda v: v['payload'].update(email='private@example.test'))
bad('event', lambda v: v['payload'].update(reasonCode='ALL_CLEAR'))
bad('event', lambda v: v['payload'].update(downloadUrl='https://example.test/public'))
bad('event', lambda v: v.update(aggregateKind='Dataset'), index=3)
bad('event', lambda v: v['payload'].update(memberId='private'), index=3)
bad('event', lambda v: v['payload'].update(contentSetSha256='bad'), index=4)
bad('event', lambda v: v.update(aggregateKind='AssetRevision'), index=5)

bad('manifest', lambda v: v['files'][0].update(slotId='slot_demo\n'))
bad('manifest', lambda v: v['files'][0].update(declaredSourceSha256='0' * 64 + '\n'))
bad('event', lambda v: v.update(eventId='evt_demo\n'))

# The template cannot be interpreted as completed measurements or human approval.
assert template['artifact_type'] == 'EMPTY_PILOT_EVIDENCE_TEMPLATE'
assert template['authority'] == 'PROPOSED' and template['status'] == 'NOT_EXECUTED'
assert template['human_review']['status'] == 'PENDING'
assert all(template['human_review'][k] is None for k in ['reviewer', 'reviewed_at', 'approval_reference'])
assert all(v is None for v in template['candidate'].values())
assert template['blockers']
assert len(template['checks']) == 8 and len({v['id'] for v in template['checks']}) == 8
assert all(v['status'] == 'NOT_EXECUTED' and not v['evidence_refs'] for v in template['checks'])
assert not template['measurements']['samples'] and not template['measurements']['device_results']
for key in ['currency', 'labor_hours', 'hourly_rate', 'one_time_amount', 'maintenance_amount', 'storage_bytes']:
    assert template['costs'][key] is None
assert template['costs']['confidence'] == 'UNKNOWN'
assert template['manual_review']['rights_verdict'] == template['manual_review']['quality_verdict'] == 'UNKNOWN'
assert not template['decision']['enabled'] and template['decision']['result'] == 'NOT_EXECUTED'
assert not template['decision']['supported_combinations'] and not template['expansion_matrix']
assert len(template['baseline_openapi_sha256']) == 4
for name, expected in template['baseline_openapi_sha256'].items():
    assert hashlib.sha256((PLAN / name).read_bytes()).hexdigest() == expected, name

# Stable coverage IDs and linked sources are structural evidence, not executed ACs.
for file, prefix, n in [('fitting/ASSET_IMPORT_REVIEW_PLAN.md', 'IMP', 6), ('fitting/ASSET_IMPORT_REVIEW_PLAN.md', 'EVT', 6), ('fitting/FITTING_PILOT_PLAN.md', 'PILOT', 8)]:
    ids = re.findall(r'\| (' + prefix + r'-AC\d{2}) \|', (PLAN / file).read_text())
    assert len(ids) == len(set(ids)) == n, (file, prefix)
print(json.dumps({'result':'PASS','scope':'proposal_documents_only','schemas':2,'synthetic_examples':example_count,'event_variants':6,'negative_schema_cases':len(negative),'planned_acceptance_cases':20,'evidence_template':'EMPTY_NOT_EXECUTED','source_api_baselines':'FOUR_UNCHANGED','product_tests':'NOT_EXECUTED'}))
