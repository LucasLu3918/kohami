IF DB_NAME() <> 'Kohami' THROW 51290,'DATABASE_SCOPE_INVALID',1;
IF DATABASE_PRINCIPAL_ID('role_kohami_api') IS NULL
    THROW 51291,'API_RUNTIME_ROLE_REQUIRED',1;
IF OBJECT_ID('commerce.wallet_debit','P') IS NULL
   OR OBJECT_ID('commerce.Collections','U') IS NULL
   OR OBJECT_ID('commerce.RefundUnits','U') IS NULL
    THROW 51292,'WALLET_CHECKOUT_SCHEMA_REQUIRED',1;
GO
/* The API may call the atomic debit and create its payment accounting rows. */
GRANT EXECUTE ON OBJECT::commerce.wallet_debit TO role_kohami_api;
GRANT INSERT ON OBJECT::commerce.Collections TO role_kohami_api;
GRANT INSERT ON OBJECT::commerce.RefundUnits TO role_kohami_api;
