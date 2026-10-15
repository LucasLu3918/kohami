"""Check nonsecret security mechanism proposals; no keys/users/certs/DB operations."""
from pathlib import Path
from copy import deepcopy
import hashlib
import json
import re
import yaml

ROOT=Path(__file__).resolve().parents[1]
PLAN=ROOT/'docs/planning/kohami'
profile=yaml.safe_load((PLAN/'options'/'SECURITY_OPERATIONS_PROFILE_OPTION.yaml').read_text())
account=yaml.safe_load((PLAN/'contracts/API_ACCOUNT_ACCESS.openapi.yaml').read_text())

def check(p):
    def safe_metadata(value):
        if isinstance(value,dict):
            for key,item in value.items():
                normalized=re.sub(r'[^a-z0-9]','',key.lower())
                assert normalized not in {'password','passwordvalue','privatekey','privatekeyvalue','pfxbase64','totpseed','seedvalue','permittoken','cookievalue','connectionstring','recoverycode','secretvalue'}, 'secret-value fields forbidden'
                safe_metadata(item)
        elif isinstance(value,list):
            for item in value:safe_metadata(item)
        elif isinstance(value,str):
            assert not re.search(r'BEGIN (?:RSA |EC |ENCRYPTED )?PRIVATE KEY',value), 'private material forbidden'
    safe_metadata(p)
    assert p['kind']=='SECURITY_OPERATIONS_OPTION'
    assert p['authority']=='DEFERRED_OPTION' and p['adopted'] is False
    assert p['runtime_status']=='NOT_EXECUTED' and p['command_binding'] is None
    assert p['scope']['mode']=='mock' and p['scope']['network']=='LOOPBACK_ONLY'
    for k in ['legacy_database_modified','public_bootstrap_endpoint_allowed','public_native_recovery_allowed','native_authority_from_owner_cookie_allowed']:
        assert p['scope'][k] is False
    a=p['native_authority']
    for k in ['os_reauthentication_required','per_action_native_confirmation_required','issuer_isolation_evidence_required','permit_single_use']:
        assert a[k] is True
    assert a['issuer_key_consumer']==['native_operations'] and a['issuer_binding'] is None and a['broker_binding'] is None
    assert a['client_approved_boolean_is_authority'] is False and a['permit_ttl_adopted'] is False
    assert set(a['permit_binding'])=={'action','target','datasetId','instanceDbFingerprint','previewHash','securityEpoch','expiresAt'}
    b=p['bootstrap']
    for k in ['needs_no_owner_and_no_prior_bootstrap_journal','dataset_lock_and_unique_journal_required','setup_only_before_mfa','password_and_totp_before_activation','profile_configured_required','ordinary_no_self_grant','native_grant_exception_requires_separate_action_confirmation']:
        assert b[k] is True
    assert b['email_verification_required_before_active'] and not b['native_email_input_implies_confirmed']
    assert p['initial_native_provisioning']['os_provisioning_authority_required']
    assert not p['initial_native_provisioning']['runtime_api_can_self_initialize']
    assert not p['initial_native_provisioning']['new_issuer_can_self_approve_initial_authority']
    assert p['initial_native_provisioning']['operator_broker_helper_source_binding'] is None
    assert b['setup_stage_permissions']==[] and b['automatic_default_password_allowed'] is False
    assert b['grant_duration_adopted'] is False
    assert b['profiles']['CoreDemoOwner']['permissions']==account['x-role-permission-allowlist']['Owner']
    assert len(b['profiles']['CoreDemoOwner']['permissions'])==23
    assert set(b['profiles']['MaintenanceOnly']['permissions'])=={'access.read','user.read','access.manage'}
    for v in b['profiles'].values():
        assert v['phase']=='core' and v['adopted'] is False
        assert not any(k.startswith(('fitting.','asset.')) for k in v['permissions'])
    actions=p['actions'];assert len(actions)==len({v['action'] for v in actions})==7
    for v in actions:
        assert v['requires_native_authority'] and v['requires_maintenance_and_fencing'] and v['requires_current_epoch_and_scope']
        assert v['can_issue_admin_session_directly'] is False
        assert v['implementation_binding'] is None and v['runtime_status']=='NOT_EXECUTED'
    credential=next(v for v in actions if v['action']=='CredentialRecovery')
    assert credential['can_reset_credential'] and not credential['can_issue_grant']
    recovery=p['recovery'];assert recovery['result_stage']=='RebindRequired' and recovery['normal_password_and_offline_code_path_first']
    for k in ['direct_admin_session_allowed','automatic_grant_restore_allowed','automatic_email_or_order_change_allowed','automatic_suspended_closed_reactivation_allowed','unknown_refund_release_allowed','started_operation_replay_allowed','old_password_backup_restore_allowed']:
        assert recovery[k] is False
    m=p['maintenance']
    for k in ['stop_new_business_commands','fence_workers_and_dispatch','track_started_system_result_same_source','preview_before_apply','host_intent_before_db_apply','db_journal_audit_and_changes_same_transaction']:
        assert m[k] is True
    assert m['missing_checkpoint_result']=='BLOCKED' and m['fixture_reset_can_delete_native_audit'] is False
    keys=p['key_materials'];assert {v['id'] for v in keys}=={f'KEY-{i:02}' for i in range(1,9)}
    assert all(v['implementation_binding'] is None and v['runtime_status']=='NOT_EXECUTED' for v in keys)
    assert next(v for v in keys if v['id']=='KEY-07')['consumers']==['native_operations']
    kp=p['key_policies']
    for k in ['session_and_totp_rings_separate','explicit_at_rest_protection','stable_protection_namespace','old_decrypt_private_keys_retained_by_dependency_review']:
        assert kp[k] is True
    for k in ['ring_persistence_implies_automatic_encryption','user_security_version_changes_replace_totp_seed','automatic_rewrap_old_ring_files_claimed','missing_keys_auto_empty_ring_allowed','dangerous_unprotect_normal_recovery_allowed','secret_values_in_repo_chat_argv_logs_or_profile_allowed','user_secrets_claimed_encrypted_vault','posix_same_uid_claimed_process_isolation','retention_rotation_policy_adopted']:
        assert kp[k] is False
    assert 'CredentialVersion' in kp['totp_binding'] and 'SecurityVersion' not in kp['totp_binding']
    tls=p['tls'];assert tls['browser_origin']=='https://localhost:3000'
    assert tls['sql_product_encrypt'] and not tls['sql_product_trust_server_certificate']
    assert tls['legacy_test_trust_exception'] and not tls['legacy_exception_is_product_pass']
    assert not tls['global_dev_cert_clean_allowed']
    assert tls['sql_tls_binding'] is None and tls['nuxt_cert_consumer_binding'] is None
    assert len(p['sql_roles'])==8
    for name in p['sql_runtime_roles']:
        r=p['sql_roles'][name]
        assert not r['broad_fixed_roles_allowed'] and not r['server_admin_allowed'] and not r['ddl_allowed']
        assert not r['backup_allowed'] and not r['restore_allowed'] and not r['native_ops_allowed']
        assert not r['direct_immutable_update_delete_allowed']
    assert not p['sql_roles']['worker_runtime']['identity_grant_admin_allowed']
    assert not p['sql_roles']['migrator']['db_owner_default'] and not p['sql_roles']['migrator']['runtime_consumer_allowed']
    assert not p['sql_roles']['backup_operator']['restore_allowed']
    assert not p['sql_roles']['restore_operator']['overwrite_original_allowed'] and not p['sql_roles']['restore_operator']['runtime_consumer_allowed']
    assert not p['sql_roles']['native_operations']['direct_general_dml_allowed']
    assert p['physical_sql_object_bindings'] is None and p['product_permission_tests']=='NOT_EXECUTED'
    for prefix,ids in p['acceptance'].items():
        assert ids==[f'{prefix}-AC{i:02}' for i in range(1,9)]
    for path,expected in p['input_baselines'].items():
        f=Path(path);assert not f.is_absolute() and '..' not in f.parts
        assert hashlib.sha256((ROOT/f).read_bytes()).hexdigest()==expected,path

check(profile)
negative=[]
def reject(mutate):
    p=deepcopy(profile);mutate(p)
    try:check(p)
    except AssertionError:negative.append(True)
    else:raise AssertionError('unsafe or unsupported planning claim accepted')
reject(lambda p:p.update(adopted=True))
reject(lambda p:p.update(runtime_status='PASS'))
reject(lambda p:p.update(command_binding='unverified_command'))
reject(lambda p:p['scope'].update(public_bootstrap_endpoint_allowed=True))
reject(lambda p:p['native_authority'].update(per_action_native_confirmation_required=False))
reject(lambda p:p['native_authority'].update(client_approved_boolean_is_authority=True))
reject(lambda p:p['native_authority'].update(issuer_key_consumer=['api_runtime']))
reject(lambda p:p['bootstrap'].update(automatic_default_password_allowed=True))
reject(lambda p:p['bootstrap'].update(setup_stage_permissions=['refund.execute']))
reject(lambda p:p['bootstrap'].update(password_and_totp_before_activation=False))
reject(lambda p:p['bootstrap']['profiles']['CoreDemoOwner']['permissions'].append('fitting.manage'))
reject(lambda p:p['recovery'].update(automatic_grant_restore_allowed=True))
reject(lambda p:p['recovery'].update(automatic_suspended_closed_reactivation_allowed=True))
reject(lambda p:p['recovery'].update(unknown_refund_release_allowed=True))
reject(lambda p:p['maintenance'].update(host_intent_before_db_apply=False))
reject(lambda p:p['maintenance'].update(fixture_reset_can_delete_native_audit=True))
reject(lambda p:p['key_policies'].update(missing_keys_auto_empty_ring_allowed=True))
reject(lambda p:p['key_policies'].update(automatic_rewrap_old_ring_files_claimed=True))
reject(lambda p:p['key_policies'].update(posix_same_uid_claimed_process_isolation=True))
reject(lambda p:p['tls'].update(sql_product_trust_server_certificate=True))
reject(lambda p:p['tls'].update(legacy_exception_is_product_pass=True))
reject(lambda p:p['sql_roles']['api_runtime'].update(broad_fixed_roles_allowed=True))
reject(lambda p:p['sql_roles']['worker_runtime'].update(native_ops_allowed=True))
reject(lambda p:p['sql_roles']['backup_operator'].update(restore_allowed=True))
reject(lambda p:p['sql_roles']['restore_operator'].update(overwrite_original_allowed=True))
reject(lambda p:p['input_baselines'].update({'infra/sql-server/manage.sh':'0'*64}))
reject(lambda p:p['initial_native_provisioning'].update(new_issuer_can_self_approve_initial_authority=True))
reject(lambda p:p['initial_native_provisioning'].update(runtime_api_can_self_initialize=True))
reject(lambda p:p['bootstrap'].update(native_email_input_implies_confirmed=True))
reject(lambda p:p.update(secret_value=None))
reject(lambda p:p['key_materials'][0].update(private_key_value=None))
for f,prefix in [('options/OWNER_BOOTSTRAP_RECOVERY_OPTION.md','OPS'),('security/KEY_CERT_LIFECYCLE_PLAN.md','KEY'),('security/SQL_LEAST_PRIVILEGE_PLAN.md','SQL')]:
    ids=re.findall(r'\| ('+prefix+r'-AC\d{2}) \|',(PLAN/f).read_text())
    assert len(ids)==len(set(ids))==8
ledger=yaml.safe_load((PLAN/'PLANNING_READINESS.yaml').read_text())
assert ledger['product_status']=='NOT_EXECUTED' and ledger['canonical']['status']=='NOT_CREATED'
for ref in ledger.get('mechanism_sources',[]):
    assert ref['status']=='PROPOSED_SOURCE_UNBOUND' and ref['runtime_status']=='NOT_EXECUTED'
    path=Path(ref['path']);assert not path.is_absolute() and '..' not in path.parts
    assert hashlib.sha256((ROOT/path).read_bytes()).hexdigest()==ref['sha256']
print(json.dumps({'result':'PASS','scope':'nonsecret_mechanism_proposal_only','native_actions':7,'key_categories':8,'sql_roles':8,'planned_acceptance_cases':24,'negative_claim_cases':len(negative),'source_baselines':'UNCHANGED','commands_keys_certs_users_sql_operations':'NOT_EXECUTED','adoption':'NOT_INFERRED'}))
