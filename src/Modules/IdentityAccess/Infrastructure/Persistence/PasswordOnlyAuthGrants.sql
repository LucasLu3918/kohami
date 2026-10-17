IF OBJECT_ID('m0.security_accept_totp','P') IS NOT NULL DROP PROCEDURE m0.security_accept_totp;
GO
IF OBJECT_ID('m0.security_accept_recovery','P') IS NOT NULL DROP PROCEDURE m0.security_accept_recovery;
GO
IF OBJECT_ID('m0.security_accept_recovery','P') IS NOT NULL DROP PROCEDURE m0.security_accept_recovery;
GO
-- Provisioner creates principals; this script grants only named procedures.
IF DATABASE_PRINCIPAL_ID('m0_api') IS NULL CREATE ROLE m0_api;
IF DATABASE_PRINCIPAL_ID('m0_worker') IS NULL CREATE ROLE m0_worker;
GO
GRANT EXECUTE ON OBJECT::m0.security_lock TO m0_api;
GRANT EXECUTE ON OBJECT::m0.security_load_email TO m0_api;
GRANT EXECUTE ON OBJECT::m0.security_load_session TO m0_api;
GRANT EXECUTE ON OBJECT::m0.security_throttle TO m0_api;
GRANT EXECUTE ON OBJECT::m0.security_create_session TO m0_api;
GRANT EXECUTE ON OBJECT::m0.security_touch_session TO m0_api;
GRANT EXECUTE ON OBJECT::m0.security_logout TO m0_api;
GRANT EXECUTE ON OBJECT::m0.security_create_challenge TO m0_api;
GRANT EXECUTE ON OBJECT::m0.security_load_challenge TO m0_api;
GRANT EXECUTE ON OBJECT::m0.security_fail_challenge TO m0_api;
GRANT EXECUTE ON OBJECT::m0.security_confirm_email TO m0_api;
GRANT EXECUTE ON OBJECT::m0.security_get_grant TO m0_api;
GRANT EXECUTE ON OBJECT::m0.security_revoke_grant TO m0_api;
GRANT EXECUTE ON OBJECT::m0.security_dispatch_allowed TO m0_api;
GRANT EXECUTE ON OBJECT::m0.security_lock TO m0_worker;
GRANT EXECUTE ON OBJECT::m0.security_dispatch_allowed TO m0_worker;
GRANT EXECUTE ON OBJECT::m0.security_get_replay TO m0_api;
GRANT EXECUTE ON OBJECT::m0.security_record_replay TO m0_api;
