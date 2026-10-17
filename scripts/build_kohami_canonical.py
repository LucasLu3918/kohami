"""Build unadopted planning candidate only; never generate product clients or execute PNG."""
from pathlib import Path
from copy import deepcopy
import hashlib,json,yaml,argparse
ROOT=Path(__file__).resolve().parents[1]
PLAN=ROOT/'docs/planning/kohami'
SOURCES=[('Commerce','contracts/API_M1_M3.openapi.yaml','core'),('Account','contracts/API_ACCOUNT_ACCESS.openapi.yaml','core'),('Fitting','contracts/API_FITTING.openapi.yaml','phase2'),('AssetWorkflow','contracts/API_ASSET_WORKFLOW.openapi.yaml','phase2')]
METHODS={'get','post','put','patch','delete','head','options','trace'}
# Reviewed explicit permission groups, not role/text-based automatic inference.
PERMISSIONS={
 'orders.read':'listAdminOrders getAdminOrder getRefundOperation listRefundCases getRefundCases listCompensationCases getCompensationCases listShippingRefundCases getShippingRefundCases listReturns getReturn getAdminSupportCase',
 'refund.request':'createOrderRefundCase createShippingOnlyRefundCase',
 'refund.execute':'executeProductRefund executePaymentCompensation executeShippingOnlyRefund decideRefundCase',
 'refund.check':'checkRefundOperation', 'shipment.manage':'createShipment transitionShipment',
 'return.inspect':'transitionReturn inspectReturn',
 'catalog.write':'createProductDraft listAdminProducts updateProductDraft getAdminProduct publishOrUnpublishProduct',
 'stock.adjust':'listStockBalances adjustStock', 'shipping.configure':'listFeeVersions createFeeVersion',
 'audit.read':'listAuditEvents','reconciliation.read':'listReconciliationCases getReconciliationJob getReceiptAdjustment',
 'reconciliation.run':'queueReconciliationJob','reconciliation.check':'checkReceiptAdjustment',
 'reconciliation.resolve':'resolveReconciliationCase','reports.read':'getAggregateReport',
 'demo.control':'enqueueDemoScenario','demo.reset':'previewDatasetReset resetDataset',
 'support.respond':'respondSupportCase','data.handle':'decideDataRequest getAdminDataRequest',
 'user.read':'listManagedUsers getManagedUser','user.manage':'transitionManagedUser',
 'access.read':'listAccessGrants getAccessGrant','access.manage':'createAccessGrant revokeAccessGrant'}
OP_PERMISSION={o:p for p,ops in PERMISSIONS.items() for o in ops.split()}
EXTRA_SCOPE={'fitting.read':['Dataset','AssetRevision','Compatibility'],'fitting.manage':['Dataset','AssetRevision','Compatibility'],'asset.publish':['Dataset','AssetRevision'],'asset.revoke':['Dataset','AssetRevision']}
BASE_ERROR={'400':['INVALID_REQUEST'],'401':['AUTHENTICATION_REQUIRED'],'403':['PERMISSION_CHANGED','CSRF_INVALID'],'404':['RESOURCE_NOT_FOUND'],'409':['INVALID_TRANSITION','KEY_CONFLICT'],'412':['VERSION_CHANGED'],'413':['PAYLOAD_TOO_LARGE'],'415':['UNSUPPORTED_MEDIA_TYPE'],'422':['VALIDATION_ERROR'],'428':['PRECONDITION_REQUIRED'],'429':['RATE_LIMITED'],'500':['INTERNAL_ERROR'],'503':['SERVICE_UNAVAILABLE']}
DOMAIN_ERROR={'refund':{'409':['REFUND_UNKNOWN','REFUND_LIMIT','SOURCE_CONFLICT'],'422':['AMOUNT_MISMATCH']},'commerce':{'409':['STOCK_CHANGED','QUOTE_CHANGED','QUOTE_EXPIRED','PAYMENT_UNKNOWN','PRODUCT_UNAVAILABLE','FEE_UNAVAILABLE','SHIPPING_NOT_ELIGIBLE','AMOUNT_MISMATCH']},'account':{'409':['EMAIL_CHANGE_UNAVAILABLE','LAST_OWNER_PROTECTED'],'422':['ACCOUNT_PROOF_INVALID']},'fitting':{'409':['POLICY_NOT_CONFIGURED','RIGHTS_NOT_ACTIVE','FITTING_UNAVAILABLE','QUOTA_EXCEEDED','DEPENDENCY_VERSION_CONFLICT','IMPORT_POLICY_NOT_CONFIGURED','INVALID_SLOT_CONTENT','SLOT_ALREADY_WRITTEN'],'422':['INVALID_SELECTION','INVALID_EVIDENCE_REFERENCE']}}
class Dumper(yaml.SafeDumper):
 def ignore_aliases(self,data):return True

def text_yaml(d):return yaml.dump(d,Dumper=Dumper,allow_unicode=True,sort_keys=False)
def walk(v,fn):
 if isinstance(v,dict):
  v={k:walk(x,fn) for k,x in v.items()};return fn(v)
 if isinstance(v,list):return [walk(x,fn) for x in v]
 return v

def rename(v,prefix):
 def node(d):
  if '$ref' in d:
   ref=d['$ref'];assert ref.startswith('#/components/'),ref
   section,name=ref.split('/')[2:];d['$ref']=f'#/components/{section}/{prefix}{name}' if section!='securitySchemes' else ref
  if 'security' in d:assert isinstance(d['security'],list)
  return d
 return walk(deepcopy(v),node)

def schema_properties(schema,components):
 if '$ref' in schema:return schema_properties(components['schemas'][schema['$ref'].split('/')[-1]],components)
 return schema.get('properties',{})

def parameter_list(op,components):
 return [components['parameters'][v['$ref'].split('/')[-1]] if '$ref' in v else v for v in op.get('parameters',[])]

def auth_policy(op,path,method,prefix,roles):
 oid=op['operationId'];old=op.get('x-authorization',{});old=old if isinstance(old,dict) else {}
 perm=OP_PERMISSION.get(oid,op.get('x-permission',old.get('permission')))
 if path.startswith('/admin/'):
  stage='AdminAuthenticated';allowed=[r for r,ps in roles.items() if perm in ps] if perm and perm!='command-map' else ['Owner']
  assert allowed,oid
 else:stage=old.get('stage','ScopedCapabilityOrPublic');allowed=[]
 kind=op.get('x-resource-kind',old.get('resourceKind'))
 if not kind and perm:
  kind={'refund.execute':'RefundCase','access.manage':'User','user.manage':'User','user.read':'User','access.read':'User','shipping.configure':'Dataset','demo.control':'Dataset','demo.reset':'Dataset','reconciliation.run':'Dataset','reports.read':'Report','stock.adjust':'Sku','catalog.write':'Product'}.get(perm,'Case' if 'refund' in path or 'return' in path or 'reconciliation' in path or 'support' in path or 'data-request' in path else 'Order')
 commands=deepcopy(op.get('x-command-authorization',{}))
 return {'stage':stage,'roles':allowed,'permission':perm,'resourceKind':kind,'resourceIdFrom':old.get('resourceIdFrom','server-resolved-parent-or-current-dataset' if perm else 'server-current-principal-and-capability'),'commandMap':commands,'scopeRules':'current grant + role allowlist + actual resource/parent Dataset; no self grant; unknown deny','sourceAuthorization':deepcopy(op.get('x-authorization')),'cookieAlternatives':deepcopy(op.get('security',[])),'capabilityRules':'Each cookie alternative still requires server owner/resource/intent/dataset/expiry; multiple schemes in one object are AND, different objects OR. The server validates the current session and resource authorization for every request.','grantScopeKinds':EXTRA_SCOPE.get(perm, {'user.read':['Dataset','User'],'user.manage':['Dataset','User'],'access.read':['Dataset','User'],'access.manage':['Dataset','User'],'catalog.write':['Dataset','Product','Sku'],'stock.adjust':['Dataset','Sku'],'shipping.configure':['Dataset'],'demo.control':['Dataset'],'demo.reset':['Dataset'],'reports.read':['Dataset','Report'],'reconciliation.run':['Dataset'],'reconciliation.resolve':['Dataset','Case'],'audit.read':['Dataset','User','Order','Case'],'data.handle':['Dataset','User','Order','Case']}.get(perm,['Dataset','Order','Case'] if perm else [])),'scopeResolution':'Typed RefundCase/Operation/Shipment/Return resolves parent Case/Order; AssetRevision/Compatibility must resolve same Dataset. ResourceKind is proof/object type, grantScopeKinds is authorization domain; never compare unrelated ids.','phase2PermissionsRequireAdoption':prefix in ['Fitting','AssetWorkflow']}

def build():
 ledger=yaml.safe_load((PLAN/'PLANNING_READINESS.yaml').read_text())
 source_lock={v['path']:v['sha256'] for v in ledger['sources']}
 for path,digest in source_lock.items():
  assert hashlib.sha256((ROOT/path).read_bytes()).hexdigest()==digest, 'source changed: review baseline before rebuild '+path
 docs={n:yaml.safe_load((PLAN/n).read_text()) for _,n,_ in SOURCES}
 roles=deepcopy(docs['contracts/API_ACCOUNT_ACCESS.openapi.yaml']['x-role-permission-allowlist'])
 roles['Owner']+=list(EXTRA_SCOPE)
 # Candidate adds explicit fee configuration proof, formerly recent with no matching enum.
 api={'openapi':'3.1.0','info':{'title':'KOHAMI canonical integration candidate','version':'0.1.0-candidate.1','description':'Unadopted local mock planning candidate; core95 + conditional Phase2_36. Not runtime/client authority.'},'servers':[{'url':'https://localhost:3000/api/v1'}],'x-authority':'PROPOSED','x-canonical-status':'CORE_ADOPTED_PHASE2_PROPOSED','x-core-adoption':{'decision':'Human採用修正方向並批准M0分段實作','scope':'coreDEC01-03;34M0operations;Phase2excluded'},'x-human-adoption':None,'x-product-tests':'NOT_EXECUTED','x-role-permission-allowlist':roles,'x-default-authorization':'DENY_UNKNOWN','x-png-processing-profile':{'path':'docs/planning/kohami/fitting/PNG_PROCESSING_PROFILE.yaml','sha256':hashlib.sha256((PLAN/'fitting/PNG_PROCESSING_PROFILE.yaml').read_bytes()).hexdigest(),'adopted':False,'decoder_worker_runtime':'SOURCE_UNBOUND'},'paths':{},'components':{'schemas':{},'parameters':{},'responses':{},'securitySchemes':{}}}
 mapping={'version':1,'authority':'PROPOSED','status':'CORE_ADOPTED_PHASE2_PROPOSED','human_adoption':None,'product_checks':'NOT_EXECUTED','profiles':{'core':97,'phase2':36},'source_baselines':[],'supporting_input_baselines':{p:h for p,h in source_lock.items() if p not in ['docs/planning/kohami/'+n for _,n,_ in SOURCES]},'operations':[]}
 for prefix,name,phase in SOURCES:
  src=docs[name];sha=hashlib.sha256((PLAN/name).read_bytes()).hexdigest();mapping['source_baselines'].append({'path':'docs/planning/kohami/'+name,'sha256':sha,'namespace':prefix,'phase':phase})
  c=rename(src['components'],prefix)
  for section,items in c.items():
   for key,value in items.items():
    if section=='securitySchemes':
     prior=api['components'][section].get(key)
     if prior:assert (prior['in'],prior['name'])==(value['in'],value['name'])
     else:api['components'][section][key]=value
    else:api['components'][section][prefix+key]=value
  for path,item in src['paths'].items():
   for method,original in item.items():
    assert method in METHODS
    op=rename(original,prefix);oid=op['operationId'];operation_phase=phase;policy=auth_policy(op,path,method,prefix,roles)
    # Fix OP17 according to accepted B: password yields only restricted stage for admin.
    if oid=='createSession':
     op['responses']['200'].setdefault('headers',{})['Set-Cookie']={'schema':{'type':'string'},'description':'Session issuance follows current account status and server authorization; clear mutually exclusive old cookies.'}
     policy.update(stage='SessionAsAllowedByCurrentAccountAndAuthorization',permission=None)
     op['x-cookie-issuance']=['kohami.session (server-authorized session)']
    if oid=='getSession':policy['stage']='OptionalSessionOrRestrictedStageProjection'
    if oid in ['createAccessGrant']:policy['resourceIdFrom']='body.targetUserId'
    if oid in ['getAccessGrant','revokeAccessGrant']:policy['resourceIdFrom']='server.grant.targetUserId'
    if oid in ['getManagedUser','transitionManagedUser']:policy['resourceIdFrom']='path.id (actual User)'
    if oid in ['executeProductRefund','executePaymentCompensation','executeShippingOnlyRefund','decideRefundCase']:policy['resourceIdFrom']='path.id (exact RefundCase, Case/Order scope resolved server-side)'
    if oid in ['listManagedUsers','listAccessGrants']:policy.update(resourceKind='Dataset',resourceIdFrom='currentDataset with per-row User scope filter')
    if oid in ['createFeeVersion','enqueueDemoScenario','resetDataset','previewDatasetReset']:policy['resourceIdFrom']='server.currentDataset'
    for response in op['responses'].values():
     if '$ref' in response:continue
     for h,v in response.get('headers',{}).items():
      if h.lower()=='cache-control' and path not in ['/products','/products/{slug}']:v['schema']={'type':'string','const':'no-store'}
    if operation_phase=='core':
     for status,code in [('500','INTERNAL_ERROR'),('503','SERVICE_UNAVAILABLE')]+([('413','PAYLOAD_TOO_LARGE'),('415','UNSUPPORTED_MEDIA_TYPE')] if 'requestBody' in op else []):
      if status not in op['responses']:op['responses'][status]={'description':'Generic bounded-input/service failure; no private implementation details','content':{'application/problem+json':{'schema':{'$ref':'#/components/schemas/Problem'}}}}
    domain='fitting' if phase=='phase2' else 'account' if prefix=='Account' else 'refund' if 'refund' in path or 'compensation' in path else 'commerce'
    error_map={}
    for status,response in op['responses'].items():
     if not status.isdigit() or int(status)<400:continue
     if '$ref' in response:response=deepcopy(api['components']['responses'][response['$ref'].split('/')[-1]]);op['responses'][status]=response
     codes=list(BASE_ERROR.get(status,['OPERATION_REJECTED']))+DOMAIN_ERROR.get(domain,{}).get(status,[])
     codes=sorted(set(codes));error_map[status]=codes
     response.setdefault('content',{})['application/problem+json']={'schema':{'allOf':[{'$ref':'#/components/schemas/Problem'},{'properties':{'status':{'const':int(status)},'code':{'enum':codes}}}]}}
    if oid in ['uploadAssetImportSlot','completeAssetImport','acceptAssetImportDraft','createFittingExport','downloadOwnFittingExport']:
     op['x-processing-guard']='Exact configured PNG/canvas/recipe/engine hashes; source/decode/delivery checked in isolated worker; incomplete binding rejects real acceptance/delivery; current rights and dependency hold gates apply.'
    if oid=='createAlignmentCandidate':op['x-normalization']='Candidate micro-unit1e6 half-away-from-zero; server read-back actual normalized values; no automatic stretch/crop/mirror; source spec/hash review required.'
    op['x-canonical-policy']=policy;op['x-phase']=operation_phase;op['x-activation']='APPROVED_FOR_M0_IMPLEMENTATION_RUNTIME_CHECKS_REQUIRED' if operation_phase=='core' else 'OPTIONAL_NOT_ADOPTED' if operation_phase=='optional' else 'BLOCKED_UNTIL_HUMAN_ADOPTION_AND_RUNTIME_CHECKS'
    op['x-error-code-map']=error_map;op['x-source']={'path':'docs/planning/kohami/'+name,'sha256':sha,'operationId':oid}
    params=parameter_list(op,api['components']);headers={x['name']:x for x in params if x['in']=='header'}
    body_schemas=[v['schema'] for v in op.get('requestBody',{}).get('content',{}).values()]
    expected=sorted({k for s in body_schemas for k in schema_properties(s,api['components']) if k.startswith('expected')})
    version={'target':op.get('x-version-target', 'Cart' if oid in ['getCurrentCart','setCartItem','removeCartItem','setCartPromotion','createQuote','createCheckout'] else 'Checkout' if oid in ['getCheckoutStatus','createPaymentAttempt'] else 'Slot' if oid=='uploadAssetImportSlot' else 'ImportSession' if oid in ['completeAssetImport','acceptAssetImportDraft','abortAssetImport'] else 'AssetRevision' if oid in ['createAssetRightsRecord','transitionAssetRightsRecord'] else None),'ifMatchRequired':headers.get('If-Match',{}).get('required',False),'bodyExpectedFields':expected,'rule':'ETag opaque aggregate-specific; body/header same target; dependency versions independently resolved; current auth before business replay'}
    version['readSources']={'Cart':['getCurrentCart'],'Checkout':['getCheckoutStatus'],'Quote':['createQuote'],'Grant':['getAccessGrant'],'User':['getManagedUser'],'TargetAccessSet':['getManagedUser'],'RefundCase':['getRefundCases','getCompensationCases','getShippingRefundCases'],'RefundOperation':['getRefundOperation']}.get(version['target'],[])
    version['dependencyFields']=['quoteId','cartVersion'] if oid=='createCheckout' else ['cartVersion'] if oid=='createQuote' else []
    op['x-version-contract']=version
    replay={'businessKeyRequired':headers.get('Idempotency-Key',{}).get('required',False),'proofReplayAllowed':False,'rule':'current authorization before replay; same key + actual fingerprint only; no deadline extension'};op['x-replay-contract']=replay
    consumers=['Nuxt '+('admin' if path.startswith('/admin/') else 'identity' if path.startswith('/auth/') else 'account' if path.startswith('/account/') else 'storefront')+' logical consumer','ASP.NET '+prefix+' proposed handler']
    if oid in ['completeAssetImport','createFittingExport','queueReconciliationJob','createPaymentAttempt','executeProductRefund','executePaymentCompensation','executeShippingOnlyRefund']:consumers.append('SQL outbox/local worker current-state consumer')
    row={'operationId':oid,'method':method.upper(),'path':path,'phase':operation_phase,'source':op['x-source'],'authorization':policy,'version':version,'replay':replay,'requestSchemas':body_schemas,'responseSchemas':{k:v.get('content',{}) for k,v in op['responses'].items()},'errorCodeMap':error_map,'consumers':consumers,'implementedConsumerSource':None,'privateCache':'no-store' if path not in ['/products','/products/{slug}'] else 'public-published-only; no identity state','dataEvents':'current-state/Audit/outbox if mutation; source semantics retained; no automatic event publication','adoption':None}
    assert method not in api['paths'].setdefault(path,{})
    api['paths'][path][method]=op;mapping['operations'].append(row)
 # Canonical Problem shape, retain original names as local aliases.
 api['components']['schemas']['Problem']=deepcopy(api['components']['schemas']['CommerceProblem']);api['components']['schemas']['Problem']['properties']['type']={'type':'string','format':'uri'}
 api['components']['schemas']['Problem']['properties']['code']['maxLength']=100
 api['components']['schemas']['Problem']['examples']=[{'type':'about:blank','title':'無法完成模擬操作','status':409,'code':'KEY_CONFLICT','traceId':'trace_demo'}]
 for prefix,_,_ in SOURCES:api['components']['schemas'][prefix+'Problem']={'$ref':'#/components/schemas/Problem'}
 session=deepcopy(api['components']['schemas']['CommerceSession']);session['examples']=[{'state':'Anonymous','mode':'mock','userId':None,'permissions':[],'expiresAt':None}];session['allOf']=[]
 for state in session['properties']['state']['enum']:
  rules={}
  if state=='Anonymous':rules={'userId':{'type':'null'},'expiresAt':{'type':'null'},'permissions':{'maxItems':0}}
  else:rules={'userId':{'type':'string','minLength':1},'expiresAt':{'type':'string','format':'date-time'}}
  session['allOf'].append({'if':{'properties':{'state':{'const':state}},'required':['state']},'then':{'properties':rules}})
 api['components']['schemas']['Session']=session;api['components']['schemas']['CommerceSession']={'$ref':'#/components/schemas/Session'}
 # Conditional Phase2 grant/schema integration with role and resource-scope predicates.
 scopes=api['components']['schemas']['AccountGrantScope']['properties']['kind']['enum'];scopes+=['AssetRevision','Compatibility']
 for key in ['AccountGrantCreate','AccountGrantRead']:
  s=api['components']['schemas'][key];s['properties']['permission']['enum']+=list(EXTRA_SCOPE)
  owner=s['allOf'][0]['then']['properties'];assert s['allOf'][0]['if']['properties']['role']['const']=='Owner'
  owner['permission']['enum']+=list(EXTRA_SCOPE);owner['scope']['properties']['kind']['enum']+=['AssetRevision','Compatibility']
  for permission,kinds in EXTRA_SCOPE.items():s['allOf'].append({'if':{'properties':{'permission':{'const':permission}},'required':['permission']},'then':{'properties':{'role':{'const':'Owner'},'scope':{'properties':{'kind':{'enum':kinds}}}}}})
 # AdminAsset exact effective rights pointer unified from identical source schemas.
 assert api['components']['schemas']['FittingAdminAsset']==api['components']['schemas']['AssetWorkflowAdminAsset'] or docs['contracts/API_FITTING.openapi.yaml']['components']['schemas']['AdminAsset']==docs['contracts/API_ASSET_WORKFLOW.openapi.yaml']['components']['schemas']['AdminAsset']
 asset=deepcopy(api['components']['schemas']['FittingAdminAsset']);asset['properties']['effectiveRightsRecordId']={'type':['string','null'],'pattern':'^[A-Za-z0-9_-]+$','minLength':1,'maxLength':100};asset['required'].append('effectiveRightsRecordId')
 for e in asset.get('examples',[]):e['effectiveRightsRecordId']=None
 asset['allOf']=[{'if':{'properties':{'rightsState':{'const':'Active'}},'required':['rightsState']},'then':{'properties':{'effectiveRightsRecordId':{'type':'string'}}},'else':{'properties':{'effectiveRightsRecordId':{'type':'null'}}}}]
 api['components']['schemas']['AdminAsset']=asset
 for key in ['FittingAdminAsset','AssetWorkflowAdminAsset']:api['components']['schemas'][key]={'$ref':'#/components/schemas/AdminAsset'}
 # Repair nested AdminAsset samples as well as component examples.
 def asset_sample(node):
  if {'cleanupState','rightsState','evidenceReferences','state','version'} <= set(node) and isinstance(node['rightsState'],str):
   node['effectiveRightsRecordId']='rights_demo_current' if node['rightsState']=='Active' else None
  return node
 api=walk(api,asset_sample)
 # Event union is registry candidate, no unknown-type widening and no new PNG/registry events.
 event=json.loads((PLAN/'fitting/FITTING_EVENT_CONTRACT.schema.json').read_text());workflow=deepcopy(docs['contracts/API_ASSET_WORKFLOW.openapi.yaml']['components']['schemas']['WorkflowEvent'])
 events={'$schema':'https://json-schema.org/draft/2020-12/schema','title':'KOHAMI fitting event registry candidate','x-authority':'PROPOSED','x-runtime':'NOT_EXECUTED','x-adopted':False,'oneOf':[event,workflow]}
 api['components']['schemas']['CanonicalFittingEvent']=deepcopy(events);api['components']['schemas']['AssetWorkflowWorkflowEvent']={'$ref':'#/components/schemas/CanonicalFittingEvent'}
 api['x-phase-profiles']={'core':{'operations':97,'phase2Routes':'not registered','phase2Grants':'runtime deny until separately adopted'},'phase2':{'operations':36,'activation':'Human adoption + current grants + assets + pilot/runtime evidence'}}
 mapping['build']={'tool':'scripts/build_kohami_canonical.py','mode':'planning-only','product_generator':None,'product_generator_version':None}
 return {'contracts/API_CANONICAL.openapi.yaml':text_yaml(api),'contracts/CANONICAL_OPERATION_MAP.yaml':text_yaml(mapping),'fitting/FITTING_EVENT_CANONICAL.schema.json':json.dumps(events,ensure_ascii=False,indent=2)+'\n'}

def main():
 parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');args=parser.parse_args();outputs=build()
 for n,content in outputs.items():
  if args.check:assert (PLAN/n).read_text()==content,'candidate differs: reviewed rebuild required '+n
  else:(PLAN/n).write_text(content)
 print(json.dumps({'result':'PASS','scope':'candidate_reproducibility_only' if args.check else 'unadopted_planning_generation','operations':131,'core':95,'phase2':36,'outputs':list(outputs),'product_tests':'NOT_EXECUTED'}))
if __name__=='__main__':main()
