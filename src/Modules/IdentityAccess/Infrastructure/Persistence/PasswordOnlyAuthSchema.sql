-- Migrator only. API/worker grants are exact EXECUTE, no table DML or native EXECUTE.
IF SCHEMA_ID('m0') IS NULL EXEC('CREATE SCHEMA m0 AUTHORIZATION dbo');
GO
IF OBJECT_ID('m0.SecurityState') IS NULL CREATE TABLE m0.SecurityState(Id int NOT NULL PRIMARY KEY CHECK(Id=1),Maintenance bit NOT NULL DEFAULT 1,OwnerPrepared bit NOT NULL DEFAULT 0,BootstrapActivated bit NOT NULL DEFAULT 0,BootstrapOperationId uniqueidentifier NULL,BootstrapUserId nvarchar(100) NULL,MaintenanceTargetUserId nvarchar(100) NULL,Epoch bigint NOT NULL DEFAULT 1);
IF NOT EXISTS(SELECT 1 FROM m0.SecurityState WHERE Id=1) INSERT m0.SecurityState(Id) VALUES(1);
IF OBJECT_ID('m0.Users') IS NULL CREATE TABLE m0.Users(UserId nvarchar(100) PRIMARY KEY,Email nvarchar(254) NOT NULL UNIQUE,PasswordHash nvarchar(1000) NOT NULL,EmailConfirmed bit NOT NULL DEFAULT 0,SecurityHold bit NOT NULL DEFAULT 0,Status varchar(30) NOT NULL CHECK(Status IN('Active','SetupOnly','Suspended','Closed')),SecurityVersion bigint NOT NULL DEFAULT 1);
IF OBJECT_ID('m0.Grants') IS NULL CREATE TABLE m0.Grants(GrantId nvarchar(100) PRIMARY KEY,UserId nvarchar(100) NOT NULL REFERENCES m0.Users(UserId),Role nvarchar(30) NOT NULL,Permission nvarchar(100) NOT NULL,ScopeKind nvarchar(30) NOT NULL,ScopeId nvarchar(100) NOT NULL,ValidFrom datetimeoffset NOT NULL,ValidUntil datetimeoffset NOT NULL,RevokedAt datetimeoffset NULL,Version bigint NOT NULL DEFAULT 1,GrantedBy nvarchar(100) NOT NULL,Reason nvarchar(500) NOT NULL,CHECK(ValidUntil>ValidFrom));
IF OBJECT_ID('m0.Sessions') IS NULL CREATE TABLE m0.Sessions(SessionId nvarchar(100) PRIMARY KEY,UserId nvarchar(100) NOT NULL REFERENCES m0.Users(UserId),SecurityVersion bigint NOT NULL,Stage varchar(40) NOT NULL CHECK(Stage IN('AdminAuthenticated','MemberAuthenticated')),ExpiresAt datetimeoffset NOT NULL,AbsoluteUntil datetimeoffset NOT NULL,RequestNonce nvarchar(160) NOT NULL,AuthenticatedAt datetimeoffset NOT NULL,RevokedAt datetimeoffset NULL);
IF OBJECT_ID('m0.Challenges') IS NULL CREATE TABLE m0.Challenges(ChallengeId nvarchar(100) PRIMARY KEY,UserId nvarchar(100) NOT NULL REFERENCES m0.Users(UserId),SessionId nvarchar(100) NOT NULL,Intent varchar(40) NOT NULL,RequestNonce nvarchar(160) NOT NULL,ExpiresAt datetimeoffset NOT NULL,SecurityVersion bigint NOT NULL,Attempts int NOT NULL DEFAULT 0,ConsumedAt datetimeoffset NULL,ProofHash varchar(64) NULL);
IF OBJECT_ID('m0.SecurityAudit') IS NULL CREATE TABLE m0.SecurityAudit(Id bigint IDENTITY PRIMARY KEY,UserId nvarchar(100) NOT NULL,Action varchar(80) NOT NULL,At datetimeoffset NOT NULL,OperationId uniqueidentifier NULL,SecurityVersion bigint NOT NULL);
IF OBJECT_ID('m0.AuthFailures') IS NULL CREATE TABLE m0.AuthFailures(Id bigint IDENTITY PRIMARY KEY,AccountKey varchar(64) NOT NULL,BrowserKey varchar(64) NOT NULL,IpKey varchar(64) NOT NULL,At datetimeoffset NOT NULL);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_AuthFailures_Account' AND object_id=OBJECT_ID('m0.AuthFailures')) CREATE INDEX IX_AuthFailures_Account ON m0.AuthFailures(AccountKey,At);
GO
CREATE OR ALTER PROCEDURE m0.security_lock AS BEGIN SET NOCOUNT ON;DECLARE @r int;EXEC @r=sys.sp_getapplock @Resource='kohami.m0.security',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;IF @r<0 THROW 51000,'SECURITY_LOCK_BLOCKED',1;END;
GO
CREATE OR ALTER PROCEDURE m0.security_load_email @Email nvarchar(254) AS BEGIN SET NOCOUNT ON;SELECT (SELECT * FROM m0.Users WHERE Email=@Email FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);END;
GO
CREATE OR ALTER PROCEDURE m0.security_load_session @SessionId nvarchar(100),@Now datetimeoffset AS BEGIN SET NOCOUNT ON;
SELECT (SELECT u.*,s.SessionId,s.Stage,s.ExpiresAt,s.AbsoluteUntil,s.RequestNonce,s.AuthenticatedAt,st.Maintenance,
JSON_QUERY((SELECT g.* FROM m0.Grants g WHERE g.UserId=u.UserId AND g.RevokedAt IS NULL AND g.ValidFrom<=@Now AND g.ValidUntil>@Now FOR JSON PATH)) Grants
FROM m0.Sessions s JOIN m0.Users u ON u.UserId=s.UserId CROSS JOIN m0.SecurityState st WHERE s.SessionId=@SessionId AND s.RevokedAt IS NULL AND s.SecurityVersion=u.SecurityVersion AND s.ExpiresAt>@Now AND s.AbsoluteUntil>@Now AND u.SecurityHold=0 AND u.Status IN('Active','SetupOnly') FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);END;
GO
CREATE OR ALTER PROCEDURE m0.security_throttle @AccountKey varchar(64),@BrowserKey varchar(64),@IpKey varchar(64),@Now datetimeoffset,@Failed bit=0 AS BEGIN SET NOCOUNT ON;
IF @@TRANCOUNT=0 OR APPLOCK_MODE('public','kohami.m0.security','Transaction')<>'Exclusive' THROW 51000,'SECURITY_TRANSACTION_REQUIRED',1;
IF (SELECT COUNT(*) FROM m0.AuthFailures WHERE AccountKey=@AccountKey AND At>DATEADD(minute,-15,@Now))>=5 OR (SELECT COUNT(*) FROM m0.AuthFailures WHERE BrowserKey=@BrowserKey AND At>DATEADD(minute,-15,@Now))>=20 OR (SELECT COUNT(*) FROM m0.AuthFailures WHERE IpKey=@IpKey AND At>DATEADD(minute,-15,@Now))>=20 THROW 51001,'RATE_LIMITED',1;
IF @Failed=1 INSERT m0.AuthFailures(AccountKey,BrowserKey,IpKey,At) VALUES(@AccountKey,@BrowserKey,@IpKey,@Now);END;
GO
CREATE OR ALTER PROCEDURE m0.security_create_session @UserId nvarchar(100),@SecurityVersion bigint,@SessionId nvarchar(100),@Nonce nvarchar(160),@Now datetimeoffset AS BEGIN SET NOCOUNT ON;
IF @@TRANCOUNT=0 OR APPLOCK_MODE('public','kohami.m0.security','Transaction')<>'Exclusive' THROW 51000,'SECURITY_TRANSACTION_REQUIRED',1;
DECLARE @status varchar(30),@confirmed bit;SELECT @status=Status,@confirmed=EmailConfirmed FROM m0.Users WHERE UserId=@UserId AND SecurityVersion=@SecurityVersion AND SecurityHold=0;
IF @status IS NULL OR @status NOT IN('Active','SetupOnly') OR (@status='Active' AND @confirmed=0) THROW 51002,'AUTHENTICATION_REQUIRED',1;
-- Administrative grants are checked on every request against current role, permission, and scope.
DECLARE @admin bit=CASE WHEN EXISTS(SELECT 1 FROM m0.Grants WHERE UserId=@UserId) OR @status='SetupOnly' THEN 1 ELSE 0 END;
IF EXISTS(SELECT 1 FROM m0.SecurityState WHERE Maintenance=1 AND (MaintenanceTargetUserId IS NULL OR MaintenanceTargetUserId<>@UserId)) AND @status='Active' THROW 51003,'MAINTENANCE_BLOCKED',1;
UPDATE m0.Sessions SET RevokedAt=@Now WHERE UserId=@UserId AND Stage<>'AdminAuthenticated' AND RevokedAt IS NULL;
DECLARE @stage varchar(40)=CASE WHEN @admin=1 THEN 'AdminAuthenticated' ELSE 'MemberAuthenticated' END;
INSERT m0.Sessions(SessionId,UserId,SecurityVersion,Stage,ExpiresAt,AbsoluteUntil,RequestNonce,AuthenticatedAt) VALUES(@SessionId,@UserId,@SecurityVersion,@stage,DATEADD(minute,15,@Now),DATEADD(hour,8,@Now),@Nonce,@Now);
END;
GO
CREATE OR ALTER PROCEDURE m0.security_touch_session @SessionId nvarchar(100),@Now datetimeoffset AS BEGIN SET NOCOUNT ON;
IF @@TRANCOUNT=0 OR APPLOCK_MODE('public','kohami.m0.security','Transaction')<>'Exclusive' THROW 51000,'SECURITY_TRANSACTION_REQUIRED',1;UPDATE m0.Sessions SET ExpiresAt=CASE WHEN DATEADD(minute,15,@Now)<AbsoluteUntil THEN DATEADD(minute,15,@Now) ELSE AbsoluteUntil END WHERE SessionId=@SessionId AND Stage IN('AdminAuthenticated','MemberAuthenticated') AND RevokedAt IS NULL AND ExpiresAt>@Now AND AbsoluteUntil>@Now;END;
GO
CREATE OR ALTER PROCEDURE m0.security_logout @SessionId nvarchar(100),@Now datetimeoffset AS BEGIN SET NOCOUNT ON;
IF @@TRANCOUNT=0 OR APPLOCK_MODE('public','kohami.m0.security','Transaction')<>'Exclusive' THROW 51000,'SECURITY_TRANSACTION_REQUIRED',1;UPDATE m0.Sessions SET RevokedAt=@Now WHERE SessionId=@SessionId;UPDATE m0.Challenges SET ConsumedAt=@Now WHERE SessionId=@SessionId AND ConsumedAt IS NULL;END;
GO
CREATE OR ALTER PROCEDURE m0.security_create_challenge @ChallengeId nvarchar(100),@UserId nvarchar(100),@SessionId nvarchar(100),@Intent varchar(40),@Nonce nvarchar(160),@Now datetimeoffset,@ExpiresAt datetimeoffset,@ProofHash varchar(64)=NULL AS BEGIN SET NOCOUNT ON;
IF @@TRANCOUNT=0 OR APPLOCK_MODE('public','kohami.m0.security','Transaction')<>'Exclusive' THROW 51000,'SECURITY_TRANSACTION_REQUIRED',1;
DECLARE @sv bigint,@status varchar(30);SELECT @sv=SecurityVersion,@status=Status FROM m0.Users WHERE UserId=@UserId;
IF @sv IS NULL OR @status NOT IN('Active','SetupOnly') OR @Intent<>'email' OR @ExpiresAt<=@Now OR @ExpiresAt>DATEADD(minute,5,@Now) THROW 51002,'AUTHENTICATION_REQUIRED',1;
UPDATE m0.Challenges SET ConsumedAt=@Now WHERE UserId=@UserId AND Intent='email' AND ConsumedAt IS NULL;
INSERT m0.Challenges(ChallengeId,UserId,SessionId,Intent,RequestNonce,ExpiresAt,SecurityVersion,ProofHash) VALUES(@ChallengeId,@UserId,@SessionId,'email',@Nonce,@ExpiresAt,@sv,@ProofHash);
END;
GO
CREATE OR ALTER PROCEDURE m0.security_load_challenge @ChallengeId nvarchar(100),@SessionId nvarchar(100),@Intent varchar(40),@Now datetimeoffset AS BEGIN SET NOCOUNT ON;SELECT (SELECT c.* FROM m0.Challenges c JOIN m0.Users u ON u.UserId=c.UserId WHERE c.ChallengeId=@ChallengeId AND c.SessionId=@SessionId AND c.Intent=@Intent AND c.SecurityVersion=u.SecurityVersion AND c.ConsumedAt IS NULL AND c.Attempts<5 AND c.ExpiresAt>@Now FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);END;
GO
CREATE OR ALTER PROCEDURE m0.security_fail_challenge @ChallengeId nvarchar(100) AS BEGIN SET NOCOUNT ON;
IF @@TRANCOUNT=0 OR APPLOCK_MODE('public','kohami.m0.security','Transaction')<>'Exclusive' THROW 51000,'SECURITY_TRANSACTION_REQUIRED',1;UPDATE m0.Challenges SET Attempts=Attempts+1 WHERE ChallengeId=@ChallengeId AND ConsumedAt IS NULL AND Attempts<5;END;
GO
CREATE OR ALTER PROCEDURE m0.security_confirm_email @ChallengeId nvarchar(100),@SessionId nvarchar(100),@Nonce nvarchar(160),@ProofHash varchar(64),@Now datetimeoffset AS BEGIN SET NOCOUNT ON;
IF @@TRANCOUNT=0 OR APPLOCK_MODE('public','kohami.m0.security','Transaction')<>'Exclusive' THROW 51000,'SECURITY_TRANSACTION_REQUIRED',1;
DECLARE @u nvarchar(100);SELECT @u=c.UserId FROM m0.Challenges c JOIN m0.Users u ON u.UserId=c.UserId WHERE ChallengeId=@ChallengeId AND SessionId=@SessionId AND Intent='email' AND RequestNonce=@Nonce AND ProofHash=@ProofHash AND ConsumedAt IS NULL AND Attempts<5 AND ExpiresAt>@Now AND c.SecurityVersion=u.SecurityVersion;
IF @u IS NULL BEGIN UPDATE m0.Challenges SET Attempts=Attempts+1 WHERE ChallengeId=@ChallengeId AND SessionId=@SessionId AND Intent='email' AND Attempts<5;SELECT 0;RETURN;END;
UPDATE m0.Challenges SET ConsumedAt=@Now WHERE ChallengeId=@ChallengeId;UPDATE m0.Users SET EmailConfirmed=1 WHERE UserId=@u;INSERT m0.SecurityAudit(UserId,Action,At,SecurityVersion) SELECT UserId,'EmailConfirmed',@Now,SecurityVersion FROM m0.Users WHERE UserId=@u;SELECT 1;END;
GO
CREATE OR ALTER PROCEDURE m0.security_get_grant @GrantId nvarchar(100) AS BEGIN SET NOCOUNT ON;SELECT (SELECT * FROM m0.Grants WHERE GrantId=@GrantId FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);END;
GO
CREATE OR ALTER PROCEDURE m0.security_revoke_grant @GrantId nvarchar(100),@Actor nvarchar(100),@ActorSessionId nvarchar(100),@ActorSecurityVersion bigint,@Version bigint,@Reason nvarchar(500),@Now datetimeoffset AS BEGIN SET NOCOUNT ON;
IF @@TRANCOUNT=0 OR APPLOCK_MODE('public','kohami.m0.security','Transaction')<>'Exclusive' THROW 51000,'SECURITY_TRANSACTION_REQUIRED',1;
DECLARE @u nvarchar(100),@role nvarchar(30);SELECT @u=UserId,@role=Role FROM m0.Grants WHERE GrantId=@GrantId AND Version=@Version AND RevokedAt IS NULL;
IF @u IS NULL THROW 51005,'VERSION_CHANGED',1;
IF NOT EXISTS(SELECT 1 FROM m0.Sessions s JOIN m0.Users a ON a.UserId=s.UserId JOIN m0.Grants g ON g.UserId=a.UserId WHERE a.UserId=@Actor AND a.SecurityVersion=@ActorSecurityVersion AND a.Status='Active' AND a.SecurityHold=0 AND a.EmailConfirmed=1 AND s.SessionId=@ActorSessionId AND s.SecurityVersion=a.SecurityVersion AND s.Stage='AdminAuthenticated' AND s.RevokedAt IS NULL AND s.ExpiresAt>@Now AND s.AbsoluteUntil>@Now AND g.Role='Owner' AND g.Permission='access.manage' AND g.RevokedAt IS NULL AND g.ValidFrom<=@Now AND g.ValidUntil>@Now AND ((g.ScopeKind='Dataset' AND g.ScopeId='demo_m0') OR (g.ScopeKind='User' AND g.ScopeId=@u))) OR EXISTS(SELECT 1 FROM m0.SecurityState WHERE Maintenance=1) THROW 51006,'PERMISSION_CHANGED',1;
IF @u=@Actor THROW 51006,'SELF_GRANT_FORBIDDEN',1;
IF @role='Owner' AND NOT EXISTS(SELECT 1 FROM m0.Grants g JOIN m0.Users u ON u.UserId=g.UserId WHERE g.UserId<>@u AND g.Role='Owner' AND g.Permission='access.manage' AND g.ScopeKind='Dataset' AND g.ScopeId='demo_m0' AND g.RevokedAt IS NULL AND g.ValidFrom<=@Now AND g.ValidUntil>@Now AND u.Status='Active' AND u.SecurityHold=0 AND u.EmailConfirmed=1) THROW 51007,'LAST_OWNER_PROTECTED',1;
UPDATE m0.Grants SET RevokedAt=@Now,Version=Version+1,Reason=@Reason WHERE GrantId=@GrantId;UPDATE m0.Users SET SecurityVersion=SecurityVersion+1 WHERE UserId=@u;UPDATE m0.Sessions SET RevokedAt=@Now WHERE UserId=@u AND RevokedAt IS NULL;
INSERT m0.SecurityAudit(UserId,Action,At,SecurityVersion) SELECT UserId,'GrantRevoked',@Now,SecurityVersion FROM m0.Users WHERE UserId=@u;END;
GO
CREATE OR ALTER PROCEDURE m0.security_dispatch_allowed @UserId nvarchar(100),@SecurityVersion bigint,@Permission nvarchar(100),@ResourceKind nvarchar(40),@ResourceId nvarchar(100),@DatasetId nvarchar(100),@Now datetimeoffset AS BEGIN SET NOCOUNT ON;
SELECT CAST(CASE WHEN @Permission IN('access.read','access.manage','user.read','user.manage','orders.read','refund.request','refund.execute','refund.check','shipment.manage','return.inspect','catalog.write','stock.adjust','shipping.configure','audit.read','reconciliation.read','reconciliation.run','reconciliation.check','reconciliation.resolve','reports.read','support.respond','data.handle','demo.control','demo.reset') AND @DatasetId='demo_m0' AND EXISTS(SELECT 1 FROM m0.SecurityState WHERE Maintenance=0) AND EXISTS(SELECT 1 FROM m0.Users u JOIN m0.Grants g ON g.UserId=u.UserId WHERE u.UserId=@UserId AND u.SecurityVersion=@SecurityVersion AND u.Status='Active' AND u.SecurityHold=0 AND u.EmailConfirmed=1 AND ((g.Role='Owner' AND g.ScopeKind IN('Dataset','User','Product','Sku','Order','Case','Report')) OR (g.Role='CustomerSupport' AND g.Permission IN('orders.read','refund.request','refund.check','shipment.manage','return.inspect','support.respond','reconciliation.read') AND g.ScopeKind IN('Order','Case')) OR (g.Role='Merchandiser' AND ((g.Permission='catalog.write' AND g.ScopeKind IN('Product','Sku')) OR (g.Permission='stock.adjust' AND g.ScopeKind='Sku'))) OR (g.Role='Analyst' AND g.Permission='reports.read' AND g.ScopeKind='Report')) AND g.Permission=@Permission AND g.RevokedAt IS NULL AND g.ValidFrom<=@Now AND g.ValidUntil>@Now AND ((g.ScopeKind='Dataset' AND g.ScopeId=@DatasetId) OR (g.ScopeId=@ResourceId AND (g.ScopeKind=@ResourceKind OR g.ScopeKind='Case' AND @ResourceKind='RefundCase')))) THEN 1 ELSE 0 END AS bit);END;
GO
CREATE OR ALTER PROCEDURE m0.security_native_inspect AS BEGIN SET NOCOUNT ON;
SELECT (SELECT st.*,JSON_QUERY((SELECT UserId,Status,EmailConfirmed,SecurityVersion FROM m0.Users FOR JSON PATH)) Users FROM m0.SecurityState st FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);END;
GO
CREATE OR ALTER PROCEDURE m0.security_native_prepare_owner @UserId nvarchar(100),@Email nvarchar(254),@PasswordHash nvarchar(1000),@OperationId uniqueidentifier AS BEGIN SET NOCOUNT ON;
IF @@TRANCOUNT=0 OR APPLOCK_MODE('public','kohami.m0.security','Transaction')<>'Exclusive' THROW 51000,'NATIVE_TRANSACTION_REQUIRED',1;
IF EXISTS(SELECT 1 FROM m0.SecurityState WHERE OwnerPrepared=1 OR BootstrapOperationId IS NOT NULL) OR EXISTS(SELECT 1 FROM m0.Grants WHERE Role='Owner') THROW 51008,'BOOTSTRAP_ALREADY_EXISTS',1;
IF @Email NOT LIKE '%@example.test' OR LEN(@PasswordHash)<80 THROW 51009,'NATIVE_INPUT_INVALID',1;
INSERT m0.Users(UserId,Email,PasswordHash,Status) VALUES(@UserId,@Email,@PasswordHash,'SetupOnly');
UPDATE m0.SecurityState SET Maintenance=1,OwnerPrepared=1,BootstrapOperationId=@OperationId,BootstrapUserId=@UserId,MaintenanceTargetUserId=@UserId WHERE Id=1;
INSERT m0.Grants(GrantId,UserId,Role,Permission,ScopeKind,ScopeId,ValidFrom,ValidUntil,GrantedBy,Reason)
SELECT 'grant_'+CONVERT(nvarchar(36),NEWID()),@UserId,'Owner',value,'Dataset','demo_m0',SYSUTCDATETIME(),DATEADD(day,7,SYSUTCDATETIME()),'native_operator','Native InitialOwnerPrepare'
FROM OPENJSON('["access.read","access.manage","user.read","user.manage","orders.read","refund.request","refund.execute","refund.check","shipment.manage","return.inspect","catalog.write","stock.adjust","shipping.configure","audit.read","reconciliation.read","reconciliation.run","reconciliation.check","reconciliation.resolve","reports.read","support.respond","data.handle","demo.control","demo.reset"]');
INSERT m0.SecurityAudit(UserId,Action,At,OperationId,SecurityVersion) VALUES(@UserId,'InitialOwnerPrepare',SYSUTCDATETIME(),@OperationId,1);END;
GO
CREATE OR ALTER PROCEDURE m0.security_native_activate_owner @UserId nvarchar(100),@OperationId uniqueidentifier AS BEGIN SET NOCOUNT ON;
IF @@TRANCOUNT=0 OR APPLOCK_MODE('public','kohami.m0.security','Transaction')<>'Exclusive' THROW 51000,'NATIVE_TRANSACTION_REQUIRED',1;
IF NOT EXISTS(SELECT 1 FROM m0.SecurityState WHERE OwnerPrepared=1 AND BootstrapActivated=0 AND BootstrapOperationId IS NOT NULL AND BootstrapUserId=@UserId AND Maintenance=1) OR NOT EXISTS(SELECT 1 FROM m0.Users u JOIN m0.Sessions s ON s.UserId=u.UserId WHERE u.UserId=@UserId AND u.SecurityHold=0 AND u.Status IN('SetupOnly','Active') AND u.EmailConfirmed=1 AND s.SecurityVersion=u.SecurityVersion AND s.Stage='AdminAuthenticated' AND s.RevokedAt IS NULL AND s.ExpiresAt>SYSUTCDATETIME() AND s.AbsoluteUntil>SYSUTCDATETIME() AND s.AuthenticatedAt>DATEADD(minute,-5,SYSUTCDATETIME())) THROW 51010,'OWNER_SETUP_INCOMPLETE',1;
IF NOT EXISTS(SELECT 1 FROM m0.Grants WHERE UserId=@UserId AND Role='Owner' AND RevokedAt IS NULL AND ValidUntil>SYSUTCDATETIME()) THROW 51010,'OWNER_SETUP_INCOMPLETE',1;
UPDATE m0.Users SET Status='Active' WHERE UserId=@UserId;UPDATE m0.SecurityState SET Maintenance=0,MaintenanceTargetUserId=NULL,BootstrapActivated=1 WHERE Id=1;
INSERT m0.SecurityAudit(UserId,Action,At,OperationId,SecurityVersion) SELECT UserId,'InitialOwnerActivate',SYSUTCDATETIME(),@OperationId,SecurityVersion FROM m0.Users WHERE UserId=@UserId;END;
GO
CREATE OR ALTER PROCEDURE m0.security_native_recover @UserId nvarchar(100),@OperationId uniqueidentifier AS BEGIN SET NOCOUNT ON;
IF @@TRANCOUNT=0 OR APPLOCK_MODE('public','kohami.m0.security','Transaction')<>'Exclusive' THROW 51000,'NATIVE_TRANSACTION_REQUIRED',1;
IF NOT EXISTS(SELECT 1 FROM m0.Users WHERE UserId=@UserId AND SecurityHold=0 AND Status IN('Active','SetupOnly')) OR NOT EXISTS(SELECT 1 FROM m0.Grants WHERE UserId=@UserId AND Role='Owner') THROW 51009,'NATIVE_INPUT_INVALID',1;
UPDATE m0.Users SET Status='Active',SecurityVersion=SecurityVersion+1 WHERE UserId=@UserId;
UPDATE m0.Sessions SET RevokedAt=SYSUTCDATETIME() WHERE UserId=@UserId AND RevokedAt IS NULL;UPDATE m0.Challenges SET ConsumedAt=SYSUTCDATETIME() WHERE UserId=@UserId AND ConsumedAt IS NULL;
UPDATE m0.SecurityState SET Maintenance=1,MaintenanceTargetUserId=@UserId WHERE Id=1;
INSERT m0.SecurityAudit(UserId,Action,At,OperationId,SecurityVersion) SELECT UserId,'CredentialRecovery',SYSUTCDATETIME(),@OperationId,SecurityVersion FROM m0.Users WHERE UserId=@UserId;END;
GO
CREATE OR ALTER PROCEDURE m0.security_native_renew @UserId nvarchar(100),@OperationId uniqueidentifier,@ValidUntil datetimeoffset AS BEGIN SET NOCOUNT ON;
IF @@TRANCOUNT=0 OR APPLOCK_MODE('public','kohami.m0.security','Transaction')<>'Exclusive' THROW 51000,'NATIVE_TRANSACTION_REQUIRED',1;
IF @ValidUntil<=SYSUTCDATETIME() OR @ValidUntil>DATEADD(day,7,SYSUTCDATETIME()) OR NOT EXISTS(SELECT 1 FROM m0.Users WHERE UserId=@UserId AND SecurityHold=0 AND Status='Active' AND EmailConfirmed=1) OR EXISTS(SELECT 1 FROM m0.Grants WHERE UserId=@UserId AND Role='Owner' AND RevokedAt IS NOT NULL) THROW 51009,'NATIVE_INPUT_INVALID',1;
UPDATE m0.Grants SET ValidUntil=@ValidUntil,Version=Version+1 WHERE UserId=@UserId AND Role='Owner' AND ScopeKind='Dataset' AND ScopeId='demo_m0' AND RevokedAt IS NULL;
IF @@ROWCOUNT<>23 THROW 51009,'NATIVE_INPUT_INVALID',1;
UPDATE m0.Users SET SecurityVersion=SecurityVersion+1 WHERE UserId=@UserId;UPDATE m0.Sessions SET RevokedAt=SYSUTCDATETIME() WHERE UserId=@UserId AND RevokedAt IS NULL;
UPDATE m0.SecurityState SET Maintenance=1,MaintenanceTargetUserId=@UserId WHERE Id=1;
INSERT m0.SecurityAudit(UserId,Action,At,OperationId,SecurityVersion) SELECT UserId,'RenewDemoOwnerGrant',SYSUTCDATETIME(),@OperationId,SecurityVersion FROM m0.Users WHERE UserId=@UserId;END;
GO
CREATE OR ALTER PROCEDURE m0.security_native_transfer @OldUserId nvarchar(100),@NewUserId nvarchar(100),@Email nvarchar(254),@PasswordHash nvarchar(1000),@OperationId uniqueidentifier AS BEGIN SET NOCOUNT ON;
IF @@TRANCOUNT=0 OR APPLOCK_MODE('public','kohami.m0.security','Transaction')<>'Exclusive' THROW 51000,'NATIVE_TRANSACTION_REQUIRED',1;
IF @OldUserId=@NewUserId OR NOT EXISTS(SELECT 1 FROM m0.Grants WHERE UserId=@OldUserId AND Role='Owner') OR @Email NOT LIKE '%@example.test' OR LEN(@PasswordHash)<80 THROW 51009,'NATIVE_INPUT_INVALID',1;
INSERT m0.Users(UserId,Email,PasswordHash,Status) VALUES(@NewUserId,@Email,@PasswordHash,'SetupOnly');
INSERT m0.Grants(GrantId,UserId,Role,Permission,ScopeKind,ScopeId,ValidFrom,ValidUntil,GrantedBy,Reason)
SELECT 'grant_'+CONVERT(nvarchar(36),NEWID()),@NewUserId,Role,Permission,ScopeKind,ScopeId,SYSUTCDATETIME(),DATEADD(day,7,SYSUTCDATETIME()),'native_operator','Native TransferDemoOwner' FROM m0.Grants WHERE UserId=@OldUserId AND Role='Owner' AND ScopeKind='Dataset' AND ScopeId='demo_m0' AND RevokedAt IS NULL;
IF @@ROWCOUNT<>23 THROW 51009,'NATIVE_INPUT_INVALID',1;
UPDATE m0.Grants SET RevokedAt=SYSUTCDATETIME(),Version=Version+1 WHERE UserId=@OldUserId AND Role='Owner' AND RevokedAt IS NULL;
UPDATE m0.Users SET SecurityVersion=SecurityVersion+1,Status='Suspended' WHERE UserId=@OldUserId;UPDATE m0.Sessions SET RevokedAt=SYSUTCDATETIME() WHERE UserId=@OldUserId AND RevokedAt IS NULL;
UPDATE m0.SecurityState SET Maintenance=1,MaintenanceTargetUserId=@NewUserId,BootstrapUserId=@NewUserId,BootstrapOperationId=@OperationId,BootstrapActivated=0 WHERE Id=1;
INSERT m0.SecurityAudit(UserId,Action,At,OperationId,SecurityVersion) SELECT UserId,'TransferDemoOwner',SYSUTCDATETIME(),@OperationId,SecurityVersion FROM m0.Users WHERE UserId=@NewUserId;END;
GO
IF OBJECT_ID('m0.SecurityReplays') IS NULL CREATE TABLE m0.SecurityReplays(Actor nvarchar(100) NOT NULL,BusinessKey nvarchar(160) NOT NULL,Fingerprint varchar(64) NOT NULL,At datetimeoffset NOT NULL,PRIMARY KEY(Actor,BusinessKey));
GO
CREATE OR ALTER PROCEDURE m0.security_get_replay @Actor nvarchar(100),@Key nvarchar(160) AS BEGIN SET NOCOUNT ON;SELECT (SELECT Fingerprint FROM m0.SecurityReplays WHERE Actor=@Actor AND BusinessKey=@Key FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);END;
GO
CREATE OR ALTER PROCEDURE m0.security_record_replay @Actor nvarchar(100),@Key nvarchar(160),@Fingerprint varchar(64),@Now datetimeoffset AS BEGIN SET NOCOUNT ON;
IF @@TRANCOUNT=0 OR APPLOCK_MODE('public','kohami.m0.security','Transaction')<>'Exclusive' THROW 51000,'SECURITY_TRANSACTION_REQUIRED',1;
INSERT m0.SecurityReplays(Actor,BusinessKey,Fingerprint,At) VALUES(@Actor,@Key,@Fingerprint,@Now);END;
GO