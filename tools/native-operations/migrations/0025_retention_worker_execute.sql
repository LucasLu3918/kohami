IF DB_NAME()<>'Kohami' THROW 51250,'DATABASE_SCOPE_INVALID',1;
IF DATABASE_PRINCIPAL_ID('role_kohami_worker') IS NULL
    THROW 51251,'WORKER_RUNTIME_ROLE_REQUIRED',1;
IF OBJECT_ID('commerce.purge_report_access_audit','P') IS NULL
    THROW 51252,'REPORT_AUDIT_PURGE_REQUIRED',1;
IF OBJECT_ID('commerce.purge_expired_guest_proofs','P') IS NULL
    THROW 51253,'GUEST_PROOF_PURGE_REQUIRED',1;
GRANT EXECUTE ON OBJECT::commerce.purge_report_access_audit TO role_kohami_worker;
GRANT EXECUTE ON OBJECT::commerce.purge_expired_guest_proofs TO role_kohami_worker;
