IF DB_NAME() <> 'KohamiM0' THROW 50000,'DATABASE_SCOPE_INVALID',1;
GO
-- The member wallet payment handler writes these two rows inside the same
-- serialized checkout transaction after a successful wallet debit.
GRANT INSERT ON OBJECT::m0commerce.Collections TO m0_api;
GRANT INSERT ON OBJECT::m0commerce.RefundUnits TO m0_api;
GO
