IF DB_NAME() <> 'Kohami' THROW 50000,'DATABASE_SCOPE_INVALID',1;
IF DATABASE_PRINCIPAL_ID('role_kohami_api') IS NULL
    THROW 51170,'API_RUNTIME_ROLE_REQUIRED',1;
IF DATABASE_PRINCIPAL_ID('role_kohami_worker') IS NULL
    THROW 51171,'WORKER_RUNTIME_ROLE_REQUIRED',1;

/* Restore the API's declared table-level permissions after the identity/schema rename. */
GRANT SELECT,INSERT,UPDATE ON OBJECT::commerce.Documents TO role_kohami_api;
GRANT SELECT,INSERT ON OBJECT::commerce.Idempotency TO role_kohami_api;
GRANT SELECT,UPDATE ON OBJECT::commerce.Inventory TO role_kohami_api;
GRANT SELECT,INSERT,UPDATE ON OBJECT::commerce.Reservations TO role_kohami_api;
GRANT SELECT,UPDATE ON OBJECT::commerce.Collections TO role_kohami_api;
GRANT SELECT,UPDATE ON OBJECT::commerce.RefundUnits TO role_kohami_api;
GRANT SELECT,INSERT,UPDATE ON OBJECT::commerce.Jobs TO role_kohami_api;
GRANT SELECT,INSERT,UPDATE ON OBJECT::commerce.GuestRequests TO role_kohami_api;
GRANT INSERT ON OBJECT::commerce.PrivateDeliveries TO role_kohami_api;
GRANT SELECT,INSERT,UPDATE ON OBJECT::commerce.GuestCapabilities TO role_kohami_api;
GRANT SELECT ON OBJECT::identity_access.SecurityState TO role_kohami_api;

/* Identity access stays limited to the procedures consumed by the API. */
GRANT EXECUTE ON OBJECT::identity_access.security_lock TO role_kohami_api;
GRANT EXECUTE ON OBJECT::identity_access.security_load_email TO role_kohami_api;
GRANT EXECUTE ON OBJECT::identity_access.security_load_session TO role_kohami_api;
GRANT EXECUTE ON OBJECT::identity_access.security_throttle TO role_kohami_api;
GRANT EXECUTE ON OBJECT::identity_access.security_create_session TO role_kohami_api;
GRANT EXECUTE ON OBJECT::identity_access.security_touch_session TO role_kohami_api;
GRANT EXECUTE ON OBJECT::identity_access.security_logout TO role_kohami_api;
GRANT EXECUTE ON OBJECT::identity_access.security_create_challenge TO role_kohami_api;
GRANT EXECUTE ON OBJECT::identity_access.security_load_challenge TO role_kohami_api;
GRANT EXECUTE ON OBJECT::identity_access.security_fail_challenge TO role_kohami_api;
GRANT EXECUTE ON OBJECT::identity_access.security_confirm_email TO role_kohami_api;
GRANT EXECUTE ON OBJECT::identity_access.security_get_grant TO role_kohami_api;
GRANT EXECUTE ON OBJECT::identity_access.security_revoke_grant TO role_kohami_api;
GRANT EXECUTE ON OBJECT::identity_access.security_dispatch_allowed TO role_kohami_api;
GRANT EXECUTE ON OBJECT::identity_access.security_get_replay TO role_kohami_api;
GRANT EXECUTE ON OBJECT::identity_access.security_record_replay TO role_kohami_api;

/* Keep worker permissions separate from API permissions and native operations. */
GRANT SELECT,INSERT,UPDATE ON OBJECT::commerce.Documents TO role_kohami_worker;
GRANT SELECT,UPDATE ON OBJECT::commerce.Inventory TO role_kohami_worker;
GRANT SELECT,UPDATE ON OBJECT::commerce.Reservations TO role_kohami_worker;
GRANT SELECT,INSERT,UPDATE ON OBJECT::commerce.Collections TO role_kohami_worker;
GRANT SELECT,INSERT,UPDATE ON OBJECT::commerce.RefundUnits TO role_kohami_worker;
GRANT SELECT,INSERT,UPDATE ON OBJECT::commerce.Jobs TO role_kohami_worker;
GRANT SELECT,INSERT,UPDATE ON OBJECT::commerce.Inbox TO role_kohami_worker;
GRANT SELECT ON OBJECT::identity_access.SecurityState TO role_kohami_worker;
GRANT EXECUTE ON OBJECT::identity_access.security_lock TO role_kohami_worker;
GRANT EXECUTE ON OBJECT::identity_access.security_dispatch_allowed TO role_kohami_worker;
