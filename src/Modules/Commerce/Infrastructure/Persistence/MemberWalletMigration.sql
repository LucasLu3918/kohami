IF DB_NAME() <> 'KohamiM0' THROW 50000,'DATABASE_SCOPE_INVALID',1;
GO
ALTER TABLE m0commerce.Collections ADD PaymentMethod varchar(20) NOT NULL
    CONSTRAINT DF_Collections_PaymentMethod DEFAULT 'ExternalMock'
    CONSTRAINT CK_Collections_PaymentMethod CHECK(PaymentMethod IN ('ExternalMock','Wallet'));
CREATE TABLE m0commerce.WalletAccounts (
    Dataset varchar(30) NOT NULL CHECK(Dataset='demo_m0'),
    UserId nvarchar(100) NOT NULL,
    AvailableTwd bigint NOT NULL CHECK(AvailableTwd>=0),
    Version bigint NOT NULL CHECK(Version>0),
    UpdatedAt datetimeoffset NOT NULL,
    PRIMARY KEY(Dataset,UserId)
);
CREATE TABLE m0commerce.WalletLedger (
    Dataset varchar(30) NOT NULL CHECK(Dataset='demo_m0'),
    EntryId varchar(100) NOT NULL,
    UserId nvarchar(100) NOT NULL,
    EntryType varchar(24) NOT NULL CHECK(EntryType IN ('SyntheticCredit','CheckoutDebit','RefundCredit')),
    AmountTwd bigint NOT NULL CHECK(AmountTwd>0),
    Currency char(3) NOT NULL CHECK(Currency='TWD'),
    SourceId varchar(100) NOT NULL,
    CreatedAt datetimeoffset NOT NULL,
    PRIMARY KEY(Dataset,EntryId),
    UNIQUE(Dataset,EntryType,SourceId),
    FOREIGN KEY(Dataset,UserId) REFERENCES m0commerce.WalletAccounts(Dataset,UserId)
);
CREATE INDEX IX_WalletLedger_UserCreated ON m0commerce.WalletLedger(Dataset,UserId,CreatedAt DESC,EntryId DESC);
GO
CREATE OR ALTER PROCEDURE m0commerce.wallet_debit
    @UserId nvarchar(100), @EntryId varchar(100), @SourceId varchar(100),
    @AmountTwd bigint, @CreatedAt datetimeoffset, @AvailableTwd bigint OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    IF @@TRANCOUNT=0 THROW 51000,'WALLET_TRANSACTION_REQUIRED',1;
    IF @AmountTwd<=0 THROW 51000,'WALLET_AMOUNT_INVALID',1;
    DECLARE @balance TABLE(AvailableTwd bigint NOT NULL);
    UPDATE m0commerce.WalletAccounts WITH(UPDLOCK,HOLDLOCK)
      SET AvailableTwd=AvailableTwd-@AmountTwd,Version=Version+1,UpdatedAt=@CreatedAt
      OUTPUT inserted.AvailableTwd INTO @balance
      WHERE Dataset='demo_m0' AND UserId=@UserId AND AvailableTwd>=@AmountTwd;
    IF @@ROWCOUNT<>1 RETURN 0;
    INSERT m0commerce.WalletLedger(Dataset,EntryId,UserId,EntryType,AmountTwd,Currency,SourceId,CreatedAt)
      VALUES('demo_m0',@EntryId,@UserId,'CheckoutDebit',@AmountTwd,'TWD',@SourceId,@CreatedAt);
    SELECT @AvailableTwd=AvailableTwd FROM m0commerce.WalletAccounts WHERE Dataset='demo_m0' AND UserId=@UserId;
    RETURN 1;
END;
GO
GRANT SELECT ON OBJECT::m0commerce.WalletAccounts TO m0_api;
GRANT SELECT ON OBJECT::m0commerce.WalletLedger TO m0_api;
GRANT EXECUTE ON OBJECT::m0commerce.wallet_debit TO m0_api;
