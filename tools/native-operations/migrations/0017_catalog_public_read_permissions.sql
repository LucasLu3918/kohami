IF DB_NAME() <> 'Kohami' THROW 50000,'DATABASE_SCOPE_INVALID',1;
IF DATABASE_PRINCIPAL_ID('role_kohami_api') IS NULL
    THROW 51170,'CATALOG_API_ROLE_REQUIRED',1;

GRANT SELECT ON OBJECT::identity_access.CatalogProducts TO role_kohami_api;
GRANT SELECT ON OBJECT::identity_access.CatalogProductSkus TO role_kohami_api;
GRANT SELECT ON OBJECT::commerce.Inventory TO role_kohami_api;
