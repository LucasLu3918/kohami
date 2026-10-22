IF DB_NAME() <> 'Kohami' THROW 51310,'DATABASE_SCOPE_INVALID',1;
IF DATABASE_PRINCIPAL_ID('role_kohami_api') IS NULL
    THROW 51311,'API_RUNTIME_ROLE_REQUIRED',1;
IF OBJECT_ID('commerce.WalletAccounts','U') IS NULL
    OR OBJECT_ID('commerce.WalletLedger','U') IS NULL
    THROW 51312,'WALLET_READ_SCHEMA_REQUIRED',1;
GO
/* The member API can read balances and ledger entries; credits and debits stay procedure-controlled. */
GRANT SELECT ON OBJECT::commerce.WalletAccounts TO role_kohami_api;
GRANT SELECT ON OBJECT::commerce.WalletLedger TO role_kohami_api;
