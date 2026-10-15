"""Validate the current proposal handoff snapshot; never create approval or execute product."""
from pathlib import Path
from copy import deepcopy
import hashlib
import json
import re
import yaml

ROOT = Path(__file__).resolve().parents[1]
PLAN = ROOT / 'docs/planning/kohami'
ledger = yaml.safe_load((PLAN/'PLANNING_READINESS.yaml').read_text())

def file_path(value):
    path = Path(value)
    assert not path.is_absolute() and '..' not in path.parts, value
    resolved = (ROOT/path).resolve()
    assert resolved.is_relative_to(ROOT) and resolved.is_file(), value
    return resolved

def unique(rows):
    ids = [v['id'] for v in rows]
    assert len(ids) == len(set(ids)), ids
    return set(ids)

def validate_snapshot(d):
    # These assertions bind this current proposal, not a permanent prohibition on
    # later human-approved adoption. Authority changes require a reviewed refresh.
    assert d['kind'] == 'PLANNING_READINESS_LEDGER'
    assert d['authority'] == 'PROPOSED' and d['status'] == 'READY_FOR_REVIEW'
    assert d['product_status'] == 'NOT_EXECUTED'
    canonical = d['canonical']
    assert canonical['status'] == 'CORE_ADOPTED_PHASE2_PROPOSED' and canonical['adopted'] is False
    assert canonical['candidate_commit'] is None and canonical['human_approval']['scope']=='DEC01-03 core only, corrected candidate; Phase2 excluded'
    planned = Path(canonical['proposed_path'])
    assert not planned.is_absolute() and '..' not in planned.parts
    assert (ROOT/planned).is_file(), 'canonical candidate missing'
    for artifact in canonical['artifact_sources']:
        path=file_path(artifact['path'])
        assert hashlib.sha256(path.read_bytes()).hexdigest()==artifact['sha256'], artifact['path']
    file_path(canonical['integration_report'])
    delta_ids = unique(canonical['deltas'])
    assert delta_ids == {f'CAN-{i:02}' for i in range(1,15)}
    for row in canonical['deltas']:
        assert row['status'] == 'DOCUMENTED_IN_CANDIDATE'
        file_path(row['source'])
    source_ids = unique(d['sources'])
    assert len(source_ids) == 9
    op_keys = set();op_ids = set();phase_counts={'core':0,'phase2':0}
    schema_owners={}
    for source in d['sources']:
        path=file_path(source['path'])
        assert hashlib.sha256(path.read_bytes()).hexdigest()==source['sha256'], source['path']
        assert source['authority']=='PROPOSED'
        if source['kind']!='openapi':
            continue
        api=yaml.safe_load(path.read_text())
        count=0
        for p,item in api['paths'].items():
            for method,op in item.items():
                count+=1
                if source['phase']=='excluded':
                    continue
                assert (method,p) not in op_keys and op['operationId'] not in op_ids
                op_keys.add((method,p));op_ids.add(op['operationId'])
        assert count==source['operations']
        assert len(api['components']['schemas'])==source['schemas']
        if source['phase']=='excluded':
            assert source['role']=='historical_baseline' and count==13
        else:
            assert source['role']=='current_proposal'
            phase_counts[source['phase']]+=count
            for n in api['components']['schemas']:
                schema_owners.setdefault(n,[]).append(source['id'])
    assert len(op_keys)==140 and phase_counts=={'core':104,'phase2':36}
    accepted_ids=unique(d['accepted_baselines']);assert len(accepted_ids)==8
    for v in d['accepted_baselines']:
        assert v['status']=='ACCEPTED' and v['reask'] is False
        file_path(v['source'])
    decisions=unique(d['open_decisions']);assert len(decisions)==7
    for v in d['open_decisions']:
        assert (v['status']=='ACCEPTED_CORE' and v['human_approval'] is not None) if v['id'] in ['DEC-01','DEC-02','DEC-03'] else (v['status']=='PROPOSED_NEEDS_ADOPTION' and v['human_approval'] is None)
        assert v['phase'] in phase_counts and set(v['related_deltas'])<=delta_ids
        file_path(v['source'])
    checks=unique(d['required_checks']);assert checks=={f'CHK-{i:02}' for i in range(1,9)}
    for v in d['required_checks']:
        assert v['runtime_status']=='NOT_EXECUTED'
        assert all(v[k] is None for k in ['product_command','candidate_commit','evidence_reference'])
    evidence_ids=unique(d['evidence_requirements']);assert len(evidence_ids)==11
    for v in d['evidence_requirements']:
        assert v['phase'] in phase_counts and v['runtime_status']=='NOT_EXECUTED'
        assert v['checks'] and set(v['checks'])<=checks
        assert all(v[k] is None for k in ['assigned_person','product_command','evidence_reference'])
        file_path(v['plan_source'])
    future_ids=unique(d['remaining_work']);assert len(future_ids)==5
    for v in d['remaining_work']:
        assert set(v['depends_on'])<=decisions|evidence_ids
    for phase in ['core','phase2']:
        v=d['phases'][phase]
        assert v['operation_count']==phase_counts[phase]
        assert v['canonical_status']==('CORE_ADOPTED_PHASE2_PROPOSED' if phase=='core' else 'CANDIDATE_READY_FOR_REVIEW') and v['implementation_status']==('IN_PROGRESS' if phase=='core' else 'NOT_EXECUTED')
        assert (v['gate1_human_approval']=='Human採用DEC01-03修正方向' and isinstance(v['gate2_implementation_authorization'],dict) and v['gate2_implementation_authorization']['scope']=='M0 core34/native7/12AC only') if phase=='core' else (v['gate1_human_approval'] is None and v['gate2_implementation_authorization'] is None)
        assert set(v['decision_ids'])=={x['id'] for x in d['open_decisions'] if x['phase']==phase}
        assert set(v['evidence_ids'])=={x['id'] for x in d['evidence_requirements'] if x['phase']==phase}
    assert d['phases']['formal_operations']['planning_status']=='DEFERRED_OUT_OF_LOCAL_MOCK'
    tickets=d['tickets']['entries'];assert len(tickets)==d['tickets']['count']==38
    unique(tickets)
    observed={'resolved':[],'claimed':[],'not_started':[]}
    actual=set(str(p.relative_to(ROOT)) for p in (ROOT/'docs/issues/kohami').glob('[0-9][0-9]-*.md'))
    assert actual=={v['path'] for v in tickets}
    for row in tickets:
        p=file_path(row['path'])
        assert hashlib.sha256(p.read_bytes()).hexdigest()==row['sha256'], 'ticket changed; review dependency status'
        match=re.search(r'(?:\*\*)?Work status:(?:\*\*)?\s*(\w+)',p.read_text(),re.I)
        current=match.group(1).lower() if match else 'not_started'
        assert current==row['work_status'] and current in observed
        observed[current].append(row['id'])
    assert observed['resolved']==['01','02','03'] and observed['claimed']==['06']
    assert len(observed['not_started'])==34
    return {'operations':len(op_keys),'core':phase_counts['core'],'phase2':phase_counts['phase2'],'same_named_schema_groups':sum(len(v)>1 for v in schema_owners.values())}

result=validate_snapshot(ledger)
negative=[]
def reject(mutate):
    value=deepcopy(ledger);mutate(value)
    try:
        validate_snapshot(value)
    except AssertionError:
        negative.append(True)
    else:
        raise AssertionError('invalid readiness claim was accepted')
reject(lambda v:v['canonical'].update(adopted=True))
reject(lambda v:v['canonical'].update(status='APPROVED'))
reject(lambda v:v.update(product_status='LOCAL_COMPLETE'))
reject(lambda v:v['open_decisions'][0].update(human_approval=None))
reject(lambda v:v['accepted_baselines'][4].update(reask=True))
reject(lambda v:v['required_checks'][0].update(runtime_status='PASS'))
reject(lambda v:v['phases']['core'].update(gate2_implementation_authorization='inferred-from-planning'))
reject(lambda v:v['phases']['core'].update(operation_count=140))
reject(lambda v:v['canonical']['deltas'].append(v['canonical']['deltas'][0]))
reject(lambda v:v['evidence_requirements'][0].update(checks=['UNKNOWN_CHECK']))
reject(lambda v:v['sources'][0].update(sha256='0'*64))
reject(lambda v:v['sources'][0].update(path='/private/arbitrary'))
reject(lambda v:v['tickets']['entries'][3].update(work_status='resolved'))
reject(lambda v:v['phases']['phase2'].update(evidence_ids=[]))
for file,prefix,n in [('contracts/CANONICAL_CONTRACT_PLAN.md','CAN',14),('delivery/IMPLEMENTATION_PLAN.md','DEC',7),('delivery/IMPLEMENTATION_PLAN.md','NEXT',5)]:
    ids=re.findall(r'\| ('+prefix+r'-\d{2}) \|',(PLAN/file).read_text())
    assert len(ids)==len(set(ids))==n
for f in ['delivery/ACCEPTANCE_EVIDENCE_TEMPLATE.yaml','fitting/FITTING_PILOT_EVIDENCE_TEMPLATE.yaml']:
    doc=yaml.safe_load((PLAN/f).read_text())
    assert 'NOT_EXECUTED' in doc['status']
    assert all(v['status']=='NOT_EXECUTED' for v in doc['checks'])
print(json.dumps({'result':'PASS','scope':'planning_snapshot_structure_only',**result,'accepted_baselines':8,'decision_groups':7,'open_decisions':4,'accepted_core_decisions':3,'evidence_groups':11,'canonical_deltas':14,'remaining_work_groups':5,'negative_claim_cases':len(negative),'tickets':'3_RESOLVED_1_CLAIMED_34_NOT_STARTED','canonical':'CORE_ADOPTED_PHASE2_PROPOSED','human_adoption':'CORE_EXPLICIT_PHASE2_NOT_INFERRED','product_checks':'NOT_EXECUTED'}))
