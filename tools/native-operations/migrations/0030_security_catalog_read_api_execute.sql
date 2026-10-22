IF DB_NAME() <> 'Kohami' THROW 51300,'DATABASE_SCOPE_INVALID',1;
IF DATABASE_PRINCIPAL_ID('role_kohami_api') IS NULL
    THROW 51301,'API_RUNTIME_ROLE_REQUIRED',1;
IF OBJECT_ID('identity_access.security_catalog_read_allowed','P') IS NULL
    THROW 51302,'CATALOG_READ_AUTHORIZATION_REQUIRED',1;
GO
/* The API may call the authorization bit procedure; it receives no table read grant. */
GRANT EXECUTE ON OBJECT::identity_access.security_catalog_read_allowed TO role_kohami_api;
