IF DB_NAME() <> 'Kohami' THROW 51200,'DATABASE_SCOPE_INVALID',1;
IF SCHEMA_ID('identity_access') IS NULL OR OBJECT_ID('identity_access.Users','U') IS NULL
    THROW 51201,'IDENTITY_SCHEMA_REQUIRED',1;

IF OBJECT_ID('identity_access.MemberProfiles','U') IS NULL
BEGIN
    CREATE TABLE identity_access.MemberProfiles
    (
        UserId nvarchar(100) NOT NULL PRIMARY KEY
            CONSTRAINT FK_MemberProfiles_Users REFERENCES identity_access.Users(UserId),
        DisplayName nvarchar(120) NULL,
        Phone nvarchar(32) NULL,
        CreatedAt datetimeoffset NOT NULL CONSTRAINT DF_MemberProfiles_CreatedAt DEFAULT SYSUTCDATETIME()
    );
END;
GO

CREATE OR ALTER PROCEDURE identity_access.security_register_member
    @UserId nvarchar(100),
    @Email nvarchar(254),
    @PasswordHash nvarchar(1000),
    @DisplayName nvarchar(120)=NULL,
    @Phone nvarchar(32)=NULL,
    @Now datetimeoffset
AS
BEGIN
    SET NOCOUNT ON;
    IF @@TRANCOUNT=0 OR APPLOCK_MODE('public','kohami.m0.security','Transaction')<>'Exclusive'
        THROW 51000,'SECURITY_TRANSACTION_REQUIRED',1;
    IF DB_NAME()<>'Kohami' OR @UserId NOT LIKE 'member[_]%' OR LEN(@Email)<3 OR LEN(@Email)>254 OR
       LEN(@PasswordHash)<80 OR LEN(@PasswordHash)>1000 OR LEN(@DisplayName)>120 OR LEN(@Phone)>32
        THROW 51202,'REGISTRATION_INPUT_INVALID',1;

    /* Return the same accepted receipt for duplicate addresses; do not reveal account existence. */
    IF EXISTS(SELECT 1 FROM identity_access.Users WITH(UPDLOCK,HOLDLOCK) WHERE Email=@Email)
    BEGIN
        SELECT 0;
        RETURN;
    END;

    INSERT identity_access.Users(UserId,Email,PasswordHash,EmailConfirmed,SecurityHold,Status,SecurityVersion)
        VALUES(@UserId,@Email,@PasswordHash,0,0,'Active',1);
    INSERT identity_access.MemberProfiles(UserId,DisplayName,Phone,CreatedAt)
        VALUES(@UserId,NULLIF(@DisplayName,N''),NULLIF(@Phone,N''),@Now);
    INSERT identity_access.SecurityAudit(UserId,Action,At,SecurityVersion)
        VALUES(@UserId,'MemberRegistered',@Now,1);
    SELECT 1;
END;
GO

/* Email syntax is sufficient for member sign-in; grants and status still fence admin accounts. */
CREATE OR ALTER PROCEDURE identity_access.security_create_session
    @UserId nvarchar(100),
    @SecurityVersion bigint,
    @SessionId nvarchar(100),
    @Nonce nvarchar(160),
    @Now datetimeoffset
AS
BEGIN
    SET NOCOUNT ON;
    IF @@TRANCOUNT=0 OR APPLOCK_MODE('public','kohami.m0.security','Transaction')<>'Exclusive'
        THROW 51000,'SECURITY_TRANSACTION_REQUIRED',1;
    DECLARE @status varchar(30),@confirmed bit;
    SELECT @status=Status,@confirmed=EmailConfirmed
    FROM identity_access.Users
    WHERE UserId=@UserId AND SecurityVersion=@SecurityVersion AND SecurityHold=0;
    IF @status IS NULL OR @status NOT IN('Active','SetupOnly')
        THROW 51002,'AUTHENTICATION_REQUIRED',1;

    DECLARE @admin bit=CASE WHEN EXISTS(SELECT 1 FROM identity_access.Grants WHERE UserId=@UserId) OR @status='SetupOnly' THEN 1 ELSE 0 END;
    IF @admin=1 AND @confirmed=0 THROW 51002,'AUTHENTICATION_REQUIRED',1;
    IF EXISTS(SELECT 1 FROM identity_access.SecurityState WHERE Maintenance=1 AND (MaintenanceTargetUserId IS NULL OR MaintenanceTargetUserId<>@UserId)) AND @status='Active'
        THROW 51003,'MAINTENANCE_BLOCKED',1;

    UPDATE identity_access.Sessions SET RevokedAt=@Now WHERE UserId=@UserId AND Stage<>'AdminAuthenticated' AND RevokedAt IS NULL;
    DECLARE @stage varchar(40)=CASE WHEN @admin=1 THEN 'AdminAuthenticated' ELSE 'MemberAuthenticated' END;
    INSERT identity_access.Sessions(SessionId,UserId,SecurityVersion,Stage,ExpiresAt,AbsoluteUntil,RequestNonce,AuthenticatedAt)
        VALUES(@SessionId,@UserId,@SecurityVersion,@stage,DATEADD(minute,15,@Now),DATEADD(hour,8,@Now),@Nonce,@Now);
END;
GO

IF DATABASE_PRINCIPAL_ID('role_kohami_api') IS NULL THROW 51203,'API_RUNTIME_ROLE_REQUIRED',1;
GRANT EXECUTE ON OBJECT::identity_access.security_register_member TO role_kohami_api;
