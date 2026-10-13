"""Validate unadopted candidate structure and adversarial schema boundaries; no product tests."""
from pathlib import Path
from copy import deepcopy
import json,yaml,hashlib
from collections import Counter
import jsonschema
from referencing import Registry,Resource
from openapi_spec_validator import validate
import sys
sys.dont_write_bytecode = True
import build_kohami_canonical as builder
ROOT=Path(__file__).resolve().parents[1];PLAN=ROOT/'docs/planning/kohami'
api=yaml.safe_load((PLAN/'API_CANONICAL.openapi.yaml').read_text());mapping=yaml.safe_load((PLAN/'CANONICAL_OPERATION_MAP.yaml').read_text())
assert api['x-authority']=='PROPOSED' and api['x-human-adoption'] is None
assert api['x-product-tests']=='NOT_EXECUTED' and mapping['human_adoption'] is None
validate(api)
for name,content in builder.build().items():assert (PLAN/name).read_text()==content,name
ops={o['operationId']:o for item in api['paths'].values() for m,o in item.items() if m in builder.METHODS}
assert len(ops)==len(mapping['operations'])==140
assert Counter(o['x-phase'] for o in ops.values())=={'core':104,'phase2':36}
assert {r['operationId'] for r in mapping['operations']}==set(ops)
base={'$schema':'https://json-schema.org/draft/2020-12/schema','components':api['components']}
registry=Registry().with_resource('urn:kohami:canonical',Resource.from_contents(base))
def validator(schema):return jsonschema.Draft202012Validator(schema,registry=registry,format_checker=jsonschema.FormatChecker())
def component(name):return validator({'$ref':'urn:kohami:canonical#/components/schemas/'+name})
examples=0
for name,schema in api['components']['schemas'].items():
 jsonschema.Draft202012Validator.check_schema(schema)
 for example in schema.get('examples',[]):component(name).validate(example);examples+=1
refs=0
def check_refs(v):
 global refs
 if isinstance(v,dict):
  if '$ref' in v:
   assert v['$ref'].startswith('#/'),v['$ref'];target=api
   for part in v['$ref'][2:].split('/'):target=target[part.replace('~1','/').replace('~0','~')]
   refs+=1
  for item in v.values():check_refs(item)
 elif isinstance(v,list):
  for item in v:check_refs(item)
check_refs(api)
negative=0
def reject(schema,example):
 global negative
 assert list(schema.iter_errors(example)),example
 negative+=1
anon=deepcopy(api['components']['schemas']['Session']['examples'][0])
for k,v in [('userId','user_demo'),('permissions',['refund.execute']),('mfaChallengeId','challenge_demo'),('mfaVerifiedAt','2026-10-13T07:00:00Z')]:
 e=deepcopy(anon);e[k]=v;reject(component('Session'),e)
stage=deepcopy(anon);stage.update(state='AdminMfaRequired',userId='user_demo',expiresAt='2026-10-13T07:05:00Z');component('Session').validate(stage)
e=deepcopy(stage);e['permissions']=['refund.execute'];reject(component('Session'),e)
e=deepcopy(stage);e['mfaVerifiedAt']='2026-10-13T07:00:00Z';reject(component('Session'),e)
admin=deepcopy(api['components']['schemas']['MfaAdminSession']['examples'][0]);component('MfaAdminSession').validate(admin)
e=deepcopy(admin);e['mfaVerifiedAt']=None;reject(component('MfaAdminSession'),e)
req=deepcopy(api['components']['schemas']['MfaReauthChallengeRequest']['examples'][0])
for purpose,kinds in api['x-recent-purpose-resource-map'].items():
 for kind in kinds:
  e=deepcopy(req);e.update(purpose=purpose,resourceKind=kind);component('MfaReauthChallengeRequest').validate(e)
 e=deepcopy(req);e.update(purpose=purpose,resourceKind='User' if 'User' not in kinds else 'Dataset');reject(component('MfaReauthChallengeRequest'),e)
grant=deepcopy(api['components']['schemas']['AccountGrantCreate']['examples'][0]);grant['role']='Owner'
for permission,kinds in builder.EXTRA_SCOPE.items():
 for kind in kinds:
  e=deepcopy(grant);e.update(permission=permission,scope={'kind':kind,'id':'resource_demo'});component('AccountGrantCreate').validate(e)
 for role in ['Merchandiser','CustomerSupport','Analyst']:
  e=deepcopy(grant);e.update(permission=permission,role=role,scope={'kind':kinds[0],'id':'resource_demo'});reject(component('AccountGrantCreate'),e)
 e=deepcopy(grant);e.update(permission=permission,scope={'kind':'User','id':'resource_demo'});reject(component('AccountGrantCreate'),e)
e=deepcopy(grant);e.update(role='CustomerSupport',permission='refund.execute');reject(component('AccountGrantCreate'),e)
asset=deepcopy(api['components']['schemas']['AdminAsset']['examples'][0]);e=deepcopy(asset);e['rightsState']='Active';reject(component('AdminAsset'),e)
e=deepcopy(asset);e['effectiveRightsRecordId']='rights_demo';reject(component('AdminAsset'),e)
e=deepcopy(asset);del e['effectiveRightsRecordId'];reject(component('AdminAsset'),e)
events=json.loads((PLAN/'FITTING_EVENT_CANONICAL.schema.json').read_text());ev=jsonschema.Draft202012Validator(events);event_count=0
for variant in events['oneOf']:
 for e in variant.get('examples',[]):ev.validate(e);event_count+=1
seed=deepcopy(events['oneOf'][1]['examples'][0]);seed['eventType']='UnknownEvent';reject(ev,seed)
seed=deepcopy(events['oneOf'][1]['examples'][0]);seed['aggregateKind']='Dataset';reject(ev,seed)
seed=deepcopy(events['oneOf'][1]['examples'][0]);seed['payload']={'arbitraryUrl':'https://example.test'};reject(ev,seed)
for row in mapping['operations']:
 op=ops[row['operationId']];assert row['authorization']==op['x-canonical-policy']
 assert row['implementedConsumerSource'] is None and row['consumers']
 assert op['x-activation']==('APPROVED_FOR_M0_IMPLEMENTATION_RUNTIME_CHECKS_REQUIRED' if row['phase']=='core' else 'BLOCKED_UNTIL_HUMAN_ADOPTION_AND_RUNTIME_CHECKS')
 if row['path'].startswith('/admin/'):
  assert row['authorization']['roles'] and (row['authorization']['permission'] or row['authorization']['commandMap'])
 for purpose in [row['authorization']['recentPurpose']]+[x['purpose'] for x in row['authorization']['commandMap'].values()]:
  if purpose:assert purpose in api['x-recent-purpose-resource-map']
 for status,codes in row['errorCodeMap'].items():
  assert codes and op['responses'][status]['content']['application/problem+json']['schema']['allOf'][1]['properties']['code']['enum']==codes
 assert row['source']['sha256']==hashlib.sha256((ROOT/row['source']['path']).read_bytes()).hexdigest()
profile=yaml.safe_load((PLAN/'PNG_PROCESSING_PROFILE.yaml').read_text())
assert profile['adopted'] is False and profile['product_checks']=='NOT_EXECUTED'
assert profile['input']['partial_decode_allowed'] is False and profile['limits']['network_allowed'] is False
assert profile['canvas']['configured_width'] is None and profile['canvas']['configured_height'] is None
assert len(profile['acceptance'])==12 and all(x['status']=='NOT_EXECUTED' for x in profile['acceptance'])
for src in profile['sources']:assert hashlib.sha256((ROOT/src['path']).read_bytes()).hexdigest()==src['sha256']
print(json.dumps({'result':'PASS','scope':'unadopted_candidate_documents_only','operations':140,'core':104,'phase2':36,'schema_examples':examples,'local_refs':refs,'event_examples':event_count,'negative_schema_cases':negative,'png_runtime':'NOT_EXECUTED','product_checks':'NOT_EXECUTED'}))
