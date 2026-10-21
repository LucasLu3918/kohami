"""Derive exact approved Foundation route/schema projection; Phase2 grant/purpose values denied."""
from pathlib import Path
from copy import deepcopy
import json,yaml,hashlib
ROOT=Path(__file__).resolve().parents[2];PLAN=ROOT/'docs/planning/kohami'
a=yaml.safe_load((PLAN/'contracts/API_CANONICAL.openapi.yaml').read_text());scope=yaml.safe_load((PLAN/'delivery/technical-baseline/APPROVED_SCOPE.yaml').read_text());assert scope['implementation_authorization'] is not None
ids={r['operationId'] for r in scope['kohami_operations']};a['paths']={p:{m:o for m,o in item.items() if o['operationId'] in ids} for p,item in a['paths'].items()};a['paths']={p:item for p,item in a['paths'].items() if item};assert sum(len(v) for v in a['paths'].values())==len(ids)
# Whole contract schemas allow Phase2 as proposals; the executable core rejects them.
extra={'fitting.read','fitting.manage','asset.publish','asset.revoke'};extra_kinds={'AssetRevision','Compatibility'}
for name in ['AccountGrantCreate','AccountGrantRead']:
 s=a['components']['schemas'][name];s['properties']['permission']['enum']=[v for v in s['properties']['permission']['enum'] if v not in extra];owner=s['allOf'][0]['then']['properties'];owner['permission']['enum']=[v for v in owner['permission']['enum'] if v not in extra];owner['scope']['properties']['kind']['enum']=[v for v in owner['scope']['properties']['kind']['enum'] if v not in extra_kinds]
 s['allOf']=[v for v in s['allOf'] if v.get('if',{}).get('properties',{}).get('permission',{}).get('const') not in extra]
a['components']['schemas']['AccountGrantScope']['properties']['kind']['enum']=[v for v in a['components']['schemas']['AccountGrantScope']['properties']['kind']['enum'] if v not in extra_kinds]
a['components']['schemas']['CommerceScenarioRequest']['properties']['targetKind']['enum']=['PaymentAttempt','RefundOperation']
# The approved runtime exposes one administrative session state. The planning
# contract also contains OwnerAuthenticated, which the current Identity adapter
# maps to AdminAuthenticated and must not leak into the executable projection.
session=a['components']['schemas']['Session']
session['properties']['state']['enum']=[v for v in session['properties']['state']['enum'] if v!='OwnerAuthenticated']
session['allOf']=[rule for rule in session['allOf'] if rule.get('if',{}).get('properties',{}).get('state',{}).get('const')!='OwnerAuthenticated']
for rule in session['allOf']:
 if rule.get('if',{}).get('properties',{}).get('state',{}).get('const')=='MemberAuthenticated':
  rule.setdefault('then',{}).setdefault('properties',{})['permissions']={'maxItems':0}
needed=set();security=set()
def refs(v):
 if isinstance(v,dict):
  if '$ref' in v:needed.add(v['$ref'])
  if 'security' in v:
   for item in v['security']:security.update(item)
  for item in v.values():refs(item)
 elif isinstance(v,list):
  for item in v:refs(item)
refs(a['paths']);done=set()
while needed-done:
 for ref in list(needed-done):
  assert ref.startswith('#/components/');section,name=ref.split('/')[2:];refs(a['components'][section][name]);done.add(ref)
a['components']={section:{name:value for name,value in items.items() if f'#/components/{section}/{name}' in needed or section=='securitySchemes' and name in security} for section,items in a['components'].items()};a['x-authority']='ADOPTED_Foundation_CORE';a['x-implementation-scope']=f'{len(ids)} approved routes only';a['x-source-canonical-sha256']=hashlib.sha256((PLAN/'contracts/API_CANONICAL.openapi.yaml').read_bytes()).hexdigest();a['x-role-permission-allowlist']['Owner']=[v for v in a['x-role-permission-allowlist']['Owner'] if v not in extra]
if 'x-recent-purpose-resource-map' in a:a['x-recent-purpose-resource-map']={k:v for k,v in a['x-recent-purpose-resource-map'].items() if k not in extra}
a['x-phase-profiles']={'identity_access':{'operations':len(ids),'phase2':'DENIED'}}
p=ROOT/'docs/contracts/runtime/Core.openapi.json';p.write_text(json.dumps(a,ensure_ascii=False,indent=2)+'\n');print(f'Projected {len(ids)} approved routes; dependencies only; Phase2 purposes/scopes denied')
