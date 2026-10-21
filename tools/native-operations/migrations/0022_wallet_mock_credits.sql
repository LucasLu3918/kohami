IF DB_NAME() <> 'Kohami' THROW 51220,'DATABASE_SCOPE_INVALID',1;
IF OBJECT_ID('commerce.WalletLedger','U') IS NULL OR OBJECT_ID('commerce.WalletAccounts','U') IS NULL
    THROW 51221,'WALLET_SCHEMA_REQUIRED',1;
GO
DECLARE @constraint sysname,@statement nvarchar(max);
DECLARE wallet_checks CURSOR LOCAL FAST_FORWARD FOR
    SELECT name FROM sys.check_constraints
    WHERE parent_object_id=OBJECT_ID('commerce.WalletLedger')
      AND definition LIKE '%SyntheticCredit%';
OPEN wallet_checks;
FETCH NEXT FROM wallet_checks INTO @constraint;
WHILE @@FETCH_STATUS=0
BEGIN
    SET @statement=N'ALTER TABLE commerce.WalletLedger DROP CONSTRAINT '+QUOTENAME(@constraint)+N';';
    EXEC sys.sp_executesql @statement;
    FETCH NEXT FROM wallet_checks INTO @constraint;
END;
CLOSE wallet_checks;
DEALLOCATE wallet_checks;
ALTER TABLE commerce.WalletLedger ADD CONSTRAINT CK_WalletLedger_EntryType
    CHECK(EntryType IN ('SyntheticCredit','CheckoutDebit','RefundCredit','MemberTopUp','AdminCredit'));
ALTER TABLE commerce.WalletLedger ADD ActorId nvarchar(100) NULL,Reason nvarchar(500) NULL;
GO
CREATE OR ALTER PROCEDURE commerce.wallet_mock_credit
    @UserId nvarchar(100),@ActorId nvarchar(100),@EntryId varchar(100),@SourceId varchar(100),
    @EntryType varchar(24),@AmountTwd bigint,@Reason nvarchar(500),@CreatedAt datetimeoffset
AS
BEGIN
    SET NOCOUNT ON;
    IF @@TRANCOUNT=0 THROW 51222,'WALLET_TRANSACTION_REQUIRED',1;
    IF @EntryType NOT IN ('MemberTopUp','AdminCredit') OR @AmountTwd<=0 OR @AmountTwd>50000
        THROW 51223,'WALLET_CREDIT_INVALID',1;
    IF @EntryType='MemberTopUp' AND @ActorId<>@UserId
        THROW 51224,'WALLET_MEMBER_ACTOR_INVALID',1;
    IF @Reason IS NULL OR LEN(@Reason) NOT BETWEEN 1 AND 500
        THROW 51225,'WALLET_REASON_INVALID',1;
    IF NOT EXISTS(SELECT 1 FROM identity_access.Users WITH(UPDLOCK,HOLDLOCK)
        WHERE UserId=@UserId AND Status='Active' AND SecurityHold=0 AND EmailConfirmed=1)
        THROW 51226,'WALLET_MEMBER_INVALID',1;
    IF EXISTS(SELECT 1 FROM commerce.WalletLedger WITH(UPDLOCK,HOLDLOCK)
        WHERE Dataset='kohami_local' AND (EntryId=@EntryId OR SourceId=@SourceId))
        THROW 51227,'WALLET_CREDIT_REPLAY',1;
    INSERT commerce.WalletAccounts(Dataset,UserId,AvailableTwd,Version,UpdatedAt)
        SELECT 'kohami_local',@UserId,0,1,@CreatedAt
        WHERE NOT EXISTS(SELECT 1 FROM commerce.WalletAccounts WITH(UPDLOCK,HOLDLOCK)
            WHERE Dataset='kohami_local' AND UserId=@UserId);
    UPDATE commerce.WalletAccounts WITH(UPDLOCK,HOLDLOCK)
        SET AvailableTwd=AvailableTwd+@AmountTwd,Version=Version+1,UpdatedAt=@CreatedAt
        WHERE Dataset='kohami_local' AND UserId=@UserId
          AND AvailableTwd<=9223372036854775807-@AmountTwd;
    IF @@ROWCOUNT<>1 THROW 51228,'WALLET_BALANCE_LIMIT',1;
    INSERT commerce.WalletLedger(Dataset,EntryId,UserId,EntryType,AmountTwd,Currency,SourceId,CreatedAt,ActorId,Reason)
        VALUES('kohami_local',@EntryId,@UserId,@EntryType,@AmountTwd,'TWD',@SourceId,@CreatedAt,@ActorId,@Reason);
    INSERT identity_access.SecurityAudit(UserId,Action,At,SecurityVersion)
        SELECT UserId,CASE WHEN @EntryType='AdminCredit' THEN 'WalletAdminCredit' ELSE 'WalletMemberTopUp' END,
            @CreatedAt,SecurityVersion FROM identity_access.Users WHERE UserId=@UserId;
END;
GO
GRANT EXECUTE ON OBJECT::commerce.wallet_mock_credit TO role_kohami_api;
