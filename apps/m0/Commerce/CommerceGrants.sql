-- Execute as provisioner only. These roles are bound to separate API / worker users by native tooling.
IF DB_NAME() <> 'KohamiM0' THROW 50000,'DATABASE_SCOPE_INVALID',1;
IF DATABASE_PRINCIPAL_ID('m0_api') IS NULL CREATE ROLE m0_api;
IF DATABASE_PRINCIPAL_ID('m0_worker') IS NULL CREATE ROLE m0_worker;
GRANT SELECT,INSERT,UPDATE ON OBJECT::m0commerce.Documents TO m0_api;
GRANT SELECT,INSERT ON OBJECT::m0commerce.Idempotency TO m0_api;
GRANT SELECT,UPDATE ON OBJECT::m0commerce.Inventory TO m0_api;
GRANT SELECT,INSERT,UPDATE ON OBJECT::m0commerce.Reservations TO m0_api;
GRANT SELECT,UPDATE ON OBJECT::m0commerce.Collections TO m0_api;
GRANT SELECT,UPDATE ON OBJECT::m0commerce.RefundUnits TO m0_api;
GRANT SELECT,INSERT,UPDATE ON OBJECT::m0commerce.Jobs TO m0_api;
GRANT SELECT,INSERT,UPDATE ON OBJECT::m0commerce.GuestRequests TO m0_api;
GRANT INSERT ON OBJECT::m0commerce.PrivateDeliveries TO m0_api;
GRANT SELECT,INSERT,UPDATE ON OBJECT::m0commerce.GuestCapabilities TO m0_api;
GRANT SELECT,INSERT,UPDATE ON OBJECT::m0commerce.Documents TO m0_worker;
GRANT SELECT,UPDATE ON OBJECT::m0commerce.Inventory TO m0_worker;
GRANT SELECT,UPDATE ON OBJECT::m0commerce.Reservations TO m0_worker;
GRANT SELECT,INSERT,UPDATE ON OBJECT::m0commerce.Collections TO m0_worker;
GRANT SELECT,INSERT,UPDATE ON OBJECT::m0commerce.RefundUnits TO m0_worker;
GRANT SELECT,INSERT,UPDATE ON OBJECT::m0commerce.Jobs TO m0_worker;
GRANT SELECT,INSERT,UPDATE ON OBJECT::m0commerce.Inbox TO m0_worker;
-- Neither service can SELECT mock proof deliveries, change schema or impersonate another SQL principal.

GRANT EXECUTE ON OBJECT::m0.security_dispatch_allowed TO m0_worker;

GRANT SELECT ON OBJECT::m0.SecurityState TO m0_api;
GRANT SELECT ON OBJECT::m0.SecurityState TO m0_worker;
GRANT SELECT ON OBJECT::m0commerce.PrivateDeliveries TO m0_native;
