IF DB_NAME()<>'Kohami' THROW 51240,'DATABASE_SCOPE_INVALID',1;
IF DATABASE_PRINCIPAL_ID('role_kohami_worker') IS NULL
    THROW 51241,'WORKER_RUNTIME_ROLE_REQUIRED',1;
IF OBJECT_ID('identity_access.security_purge_expired_logs','P') IS NULL
    THROW 51242,'SECURITY_LOG_PURGE_REQUIRED',1;
GRANT EXECUTE ON OBJECT::identity_access.security_purge_expired_logs TO role_kohami_worker;
