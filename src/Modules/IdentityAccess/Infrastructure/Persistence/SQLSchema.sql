-- Migrator only. API/worker grants are exact EXECUTE, no table DML or native EXECUTE.
IF SCHEMA_ID('m0') IS NULL EXEC('CREATE SCHEMA m0 AUTHORIZATION dbo');
GO
IF OBJECT_ID('m0.SecurityState') IS NULL CREATE TABLE m0.SecurityState(Id int NOT NULL PRIMARY KEY CHECK(Id=1),Maintenance bit NOT NULL DEFAULT 1,OwnerPrepared bit NOT NULL DEFAULT 0,BootstrapActivated bit NOT NULL DEFAULT 0,BootstrapOperationId uniqueidentifier NULL,BootstrapUserId nvarchar(100) NULL,MaintenanceTargetUserId nvarchar(100) NULL,Epoch bigint NOT NULL DEFAULT 1);
IF NOT EXISTS(SELECT 1 FROM m0.SecurityState WHERE Id=1) INSERT m0.SecurityState(Id) VALUES(1);
IF OBJECT_ID('m0.Users') IS NULL CREATE TABLE m0.Users(UserId nvarchar(100) PRIMARY KEY,Email nvarchar(254) NOT NULL UNIQUE,PasswordHash nvarchar(1000) NOT NULL,EmailConfirmed bit NOT NULL DEFAULT 0,SecurityHold bit NOT NULL DEFAULT 0,Status varchar(30) NOT NULL CHECK(Status IN('Active','SetupOnly','RebindRequired','Suspended','Closed')),SecurityVersion bigint NOT NULL DEFAULT 1,MfaVersion bigint NOT NULL DEFAULT 1,EncryptedSeed nvarchar(max) NULL,CredentialId nvarchar(100) NULL,LastAcceptedStep bigint NOT NULL DEFAULT -1,RecoveryEpoch bigint NOT NULL DEFAULT 0);
IF OBJECT_ID('m0.Grants') IS NULL CREATE TABLE m0.Grants(GrantId nvarchar(100) PRIMARY KEY,UserId nvarchar(100) NOT NULL REFERENCES m0.Users(UserId),Role nvarchar(30) NOT NULL,Permission nvarchar(100) NOT NULL,ScopeKind nvarchar(30) NOT NULL,ScopeId nvarchar(100) NOT NULL,ValidFrom datetimeoffset NOT NULL,ValidUntil datetimeoffset NOT NULL,RevokedAt datetimeoffset NULL,Version bigint NOT NULL DEFAULT 1,GrantedBy nvarchar(100) NOT NULL,Reason nvarchar(500) NOT NULL,CHECK(ValidUntil>ValidFrom));
IF OBJECT_ID('m0.Sessions') IS NULL CREATE TABLE m0.Sessions(SessionId nvarchar(100) PRIMARY KEY,UserId nvarchar(100) NOT NULL REFERENCES m0.Users(UserId),SecurityVersion bigint NOT NULL,Stage varchar(40) NOT NULL CHECK(Stage IN('AdminPasswordVerified','AdminMfaVerified','RecoveryRebind','MemberAuthenticated')),ExpiresAt datetimeoffset NOT NULL,AbsoluteUntil datetimeoffset NOT NULL,RequestNonce nvarchar(160) NOT NULL,MfaVerifiedAt datetimeoffset NULL,RevokedAt datetimeoffset NULL);
IF OBJECT_ID('m0.Challenges') IS NULL CREATE TABLE m0.Challenges(ChallengeId nvarchar(100) PRIMARY KEY,UserId nvarchar(100) NOT NULL REFERENCES m0.Users(UserId),SessionId nvarchar(100) NOT NULL,Intent varchar(40) NOT NULL,RequestNonce nvarchar(160) NOT NULL,ExpiresAt datetimeoffset NOT NULL,SecurityVersion bigint NOT NULL,Attempts int NOT NULL DEFAULT 0,ConsumedAt datetimeoffset NULL,EncryptedSeed nvarchar(max) NULL,ProofHash varchar(64) NULL,Purpose nvarchar(100) NULL,ResourceKind nvarchar(40) NULL,ResourceId nvarchar(100) NULL);
IF OBJECT_ID('m0.RecentProofs') IS NULL CREATE TABLE m0.RecentProofs(SessionId nvarchar(100) NOT NULL,UserId nvarchar(100) NOT NULL,SecurityVersion bigint NOT NULL,Purpose nvarchar(100) NOT NULL,ResourceKind nvarchar(40) NOT NULL,ResourceId nvarchar(100) NOT NULL,RequestNonce nvarchar(160) NOT NULL,VerifiedAt datetimeoffset NOT NULL,ValidUntil datetimeoffset NOT NULL,PRIMARY KEY(SessionId,Purpose,ResourceKind,ResourceId));
IF OBJECT_ID('m0.RecoveryCodes') IS NULL CREATE TABLE m0.RecoveryCodes(UserId nvarchar(100) NOT NULL REFERENCES m0.Users(UserId),SetVersion bigint NOT NULL,CodeHash varchar(64) NOT NULL,UsedAt datetimeoffset NULL,RevokedAt datetimeoffset NULL,PRIMARY KEY(UserId,SetVersion,CodeHash));
IF OBJECT_ID('m0.SecurityAudit') IS NULL CREATE TABLE m0.SecurityAudit(Id bigint IDENTITY PRIMARY KEY,UserId nvarchar(100) NOT NULL,Action varchar(80) NOT NULL,At datetimeoffset NOT NULL,OperationId uniqueidentifier NULL,SecurityVersion bigint NOT NULL);
IF OBJECT_ID('m0.AuthFailures') IS NULL CREATE TABLE m0.AuthFailures(Id bigint IDENTITY PRIMARY KEY,AccountKey varchar(64) NOT NULL,BrowserKey varchar(64) NOT NULL,IpKey varchar(64) NOT NULL,At datetimeoffset NOT NULL);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_AuthFailures_Account' AND object_id=OBJECT_ID('m0.AuthFailures')) CREATE INDEX IX_AuthFailures_Account ON m0.AuthFailures(AccountKey,At);
GO
CREATE OR ALTER PROCEDURE m0.security_lock AS BEGIN SET NOCOUNT ON;DECLARE @r int;EXEC @r=sys.sp_getapplock @Resource='kohami.m0.security',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;IF @r<0 THROW 51000,'SECURITY_LOCK_BLOCKED',1;END;
GO
CREATE OR ALTER PROCEDURE m0.security_load_email @Email nvarchar(254) AS BEGIN SET NOCOUNT ON;SELECT (SELECT * FROM m0.Users WHERE Email=@Email FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);END;
GO
CREATE OR ALTER PROCEDURE m0.security_load_session @SessionId nvarchar(100),@Now datetimeoffset AS BEGIN SET NOCOUNT ON;
SELECT (SELECT u.*,s.SessionId,s.Stage,s.ExpiresAt,s.AbsoluteUntil,s.RequestNonce,s.MfaVerifiedAt,st.Maintenance,
JSON_QUERY((SELECT g.* FROM m0.Grants g WHERE g.UserId=u.UserId AND g.RevokedAt IS NULL AND g.ValidFrom<=@Now AND g.ValidUntil>@Now FOR JSON PATH)) Grants,
JSON_QUERY((SELECT p.Purpose,p.ResourceKind,p.ResourceId,p.RequestNonce,p.VerifiedAt,p.ValidUntil FROM m0.RecentProofs p WHERE p.SessionId=s.SessionId AND p.SecurityVersion=u.SecurityVersion AND p.ValidUntil>@Now FOR JSON PATH)) RecentProofs,
JSON_QUERY((SELECT TOP 1 c.ChallengeId,c.ExpiresAt FROM m0.Challenges c WHERE c.SessionId=s.SessionId AND c.Intent='login' AND c.ConsumedAt IS NULL AND c.Attempts<5 AND c.ExpiresAt>@Now ORDER BY c.ExpiresAt DESC FOR JSON PATH,WITHOUT_ARRAY_WRAPPER)) LoginChallenge
FROM m0.Sessions s JOIN m0.Users u ON u.UserId=s.UserId CROSS JOIN m0.SecurityState st WHERE s.SessionId=@SessionId AND s.RevokedAt IS NULL AND s.SecurityVersion=u.SecurityVersion AND s.ExpiresAt>@Now AND s.AbsoluteUntil>@Now AND u.SecurityHold=0 AND u.Status IN('Active','SetupOnly','RebindRequired') FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);END;
GO
CREATE OR ALTER PROCEDURE m0.security_throttle @AccountKey varchar(64),@BrowserKey varchar(64),@IpKey varchar(64),@Now datetimeoffset,@Failed bit=0 AS BEGIN SET NOCOUNT ON;
IF @@TRANCOUNT=0 OR APPLOCK_MODE('public','kohami.m0.security','Transaction')<>'Exclusive' THROW 51000,'SECURITY_TRANSACTION_REQUIRED',1;
IF (SELECT COUNT(*) FROM m0.AuthFailures WHERE AccountKey=@AccountKey AND At>DATEADD(minute,-15,@Now))>=5 OR (SELECT COUNT(*) FROM m0.AuthFailures WHERE BrowserKey=@BrowserKey AND At>DATEADD(minute,-15,@Now))>=20 OR (SELECT COUNT(*) FROM m0.AuthFailures WHERE IpKey=@IpKey AND At>DATEADD(minute,-15,@Now))>=20 THROW 51001,'RATE_LIMITED',1;
IF @Failed=1 INSERT m0.AuthFailures(AccountKey,BrowserKey,IpKey,At) VALUES(@AccountKey,@BrowserKey,@IpKey,@Now);END;
GO
CREATE OR ALTER PROCEDURE m0.security_create_session @UserId nvarchar(100),@SecurityVersion bigint,@SessionId nvarchar(100),@Nonce nvarchar(160),@Now datetimeoffset AS BEGIN SET NOCOUNT ON;
IF @@TRANCOUNT=0 OR APPLOCK_MODE('public','kohami.m0.security','Transaction')<>'Exclusive' THROW 51000,'SECURITY_TRANSACTION_REQUIRED',1;
DECLARE @status varchar(30),@seed nvarchar(max),@confirmed bit;SELECT @status=Status,@seed=EncryptedSeed,@confirmed=EmailConfirmed FROM m0.Users WHERE UserId=@UserId AND SecurityVersion=@SecurityVersion AND SecurityHold=0;
IF @status IS NULL OR @status NOT IN('Active','SetupOnly','RebindRequired') OR (@status='Active' AND @confirmed=0) THROW 51002,'AUTHENTICATION_REQUIRED',1;
-- Administrative grants may expire/revoke without removing the MFA requirement.
DECLARE @admin bit=CASE WHEN EXISTS(SELECT 1 FROM m0.Grants WHERE UserId=@UserId) OR @status IN('SetupOnly','RebindRequired') THEN 1 ELSE 0 END;
IF EXISTS(SELECT 1 FROM m0.SecurityState WHERE Maintenance=1 AND (MaintenanceTargetUserId IS NULL OR MaintenanceTargetUserId<>@UserId)) AND @status='Active' THROW 51003,'MAINTENANCE_BLOCKED',1;
UPDATE m0.Sessions SET RevokedAt=@Now WHERE UserId=@UserId AND Stage<>'AdminMfaVerified' AND RevokedAt IS NULL;
DECLARE @stage varchar(40)=CASE WHEN @status='RebindRequired' THEN 'RecoveryRebind' WHEN @admin=1 THEN 'AdminPasswordVerified' ELSE 'MemberAuthenticated' END;
INSERT m0.Sessions(SessionId,UserId,SecurityVersion,Stage,ExpiresAt,AbsoluteUntil,RequestNonce) VALUES(@SessionId,@UserId,@SecurityVersion,@stage,DATEADD(minute,CASE WHEN @admin=1 THEN 5 ELSE 15 END,@Now),DATEADD(minute,CASE WHEN @admin=1 THEN 5 ELSE 480 END,@Now),@Nonce);
END;
GO
CREATE OR ALTER PROCEDURE m0.security_touch_session @SessionId nvarchar(100),@Now datetimeoffset AS BEGIN SET NOCOUNT ON;
IF @@TRANCOUNT=0 OR APPLOCK_MODE('public','kohami.m0.security','Transaction')<>'Exclusive' THROW 51000,'SECURITY_TRANSACTION_REQUIRED',1;UPDATE m0.Sessions SET ExpiresAt=CASE WHEN DATEADD(minute,15,@Now)<AbsoluteUntil THEN DATEADD(minute,15,@Now) ELSE AbsoluteUntil END WHERE SessionId=@SessionId AND Stage IN('AdminMfaVerified','MemberAuthenticated') AND RevokedAt IS NULL AND ExpiresAt>@Now AND AbsoluteUntil>@Now;END;
GO
CREATE OR ALTER PROCEDURE m0.security_logout @SessionId nvarchar(100),@Now datetimeoffset AS BEGIN SET NOCOUNT ON;
IF @@TRANCOUNT=0 OR APPLOCK_MODE('public','kohami.m0.security','Transaction')<>'Exclusive' THROW 51000,'SECURITY_TRANSACTION_REQUIRED',1;UPDATE m0.Sessions SET RevokedAt=@Now WHERE SessionId=@SessionId;DELETE m0.RecentProofs WHERE SessionId=@SessionId;UPDATE m0.Challenges SET ConsumedAt=@Now WHERE SessionId=@SessionId AND ConsumedAt IS NULL;END;
GO
CREATE OR ALTER PROCEDURE m0.security_create_challenge @ChallengeId nvarchar(100),@UserId nvarchar(100),@SessionId nvarchar(100),@Intent varchar(40),@Nonce nvarchar(160),@Now datetimeoffset,@ExpiresAt datetimeoffset,@EncryptedSeed nvarchar(max)=NULL,@ProofHash varchar(64)=NULL,@Purpose nvarchar(100)=NULL,@ResourceKind nvarchar(40)=NULL,@ResourceId nvarchar(100)=NULL AS BEGIN SET NOCOUNT ON;
IF @@TRANCOUNT=0 OR APPLOCK_MODE('public','kohami.m0.security','Transaction')<>'Exclusive' THROW 51000,'SECURITY_TRANSACTION_REQUIRED',1;
DECLARE @sv bigint,@status varchar(30),@seed nvarchar(max);SELECT @sv=SecurityVersion,@status=Status,@seed=EncryptedSeed FROM m0.Users WHERE UserId=@UserId;
IF @sv IS NULL OR @status NOT IN('Active','SetupOnly','RebindRequired') OR @Intent NOT IN('login','enroll','reauth','recovery','email') OR @ExpiresAt<=@Now OR @ExpiresAt>DATEADD(minute,5,@Now) THROW 51002,'AUTHENTICATION_REQUIRED',1;
IF @Intent<>'email' AND NOT EXISTS(SELECT 1 FROM m0.Sessions WHERE SessionId=@SessionId AND UserId=@UserId AND SecurityVersion=@sv AND RevokedAt IS NULL AND ExpiresAt>@Now AND AbsoluteUntil>@Now) THROW 51002,'AUTHENTICATION_REQUIRED',1;
IF @Intent IN('login','recovery') AND (@seed IS NULL OR NOT EXISTS(SELECT 1 FROM m0.Sessions WHERE SessionId=@SessionId AND Stage='AdminPasswordVerified')) THROW 51004,'MFA_PROOF_INVALID',1;
IF @Intent='enroll' AND @seed IS NOT NULL AND NOT EXISTS(SELECT 1 FROM m0.Sessions s JOIN m0.RecentProofs p ON p.SessionId=s.SessionId WHERE s.SessionId=@SessionId AND s.Stage='AdminMfaVerified' AND p.SecurityVersion=@sv AND p.Purpose='authenticator.replace' AND p.ResourceKind='Credential' AND p.ResourceId=(SELECT CredentialId FROM m0.Users WHERE UserId=@UserId) AND p.ValidUntil>@Now) THROW 51004,'MFA_PROOF_INVALID',1;
UPDATE m0.Challenges SET ConsumedAt=@Now WHERE UserId=@UserId AND Intent=@Intent AND ConsumedAt IS NULL AND (@Intent='email' OR SessionId=@SessionId);
INSERT m0.Challenges(ChallengeId,UserId,SessionId,Intent,RequestNonce,ExpiresAt,SecurityVersion,EncryptedSeed,ProofHash,Purpose,ResourceKind,ResourceId) VALUES(@ChallengeId,@UserId,@SessionId,@Intent,@Nonce,@ExpiresAt,@sv,@EncryptedSeed,@ProofHash,@Purpose,@ResourceKind,@ResourceId);
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
CREATE OR ALTER PROCEDURE m0.security_accept_totp @UserId nvarchar(100),@SessionId nvarchar(100),@ChallengeId nvarchar(100),@Nonce nvarchar(160),@Intent varchar(40),@Step bigint,@Now datetimeoffset,@NewSessionId nvarchar(100)=NULL,@NewNonce nvarchar(160)=NULL,@CredentialId nvarchar(100)=NULL,@CodeHashes nvarchar(max)=NULL AS BEGIN SET NOCOUNT ON;
IF @@TRANCOUNT=0 OR APPLOCK_MODE('public','kohami.m0.security','Transaction')<>'Exclusive' THROW 51000,'SECURITY_TRANSACTION_REQUIRED',1;
DECLARE @sv bigint,@seed nvarchar(max),@purpose nvarchar(100),@kind nvarchar(40),@resource nvarchar(100),@pending nvarchar(max),@parentUntil datetimeoffset;
SELECT @sv=u.SecurityVersion,@seed=u.EncryptedSeed FROM m0.Users u WHERE UserId=@UserId AND SecurityHold=0 AND Status IN('Active','SetupOnly','RebindRequired');
SELECT @parentUntil=CASE WHEN ExpiresAt<AbsoluteUntil THEN ExpiresAt ELSE AbsoluteUntil END FROM m0.Sessions WHERE SessionId=@SessionId AND UserId=@UserId AND SecurityVersion=@sv AND RevokedAt IS NULL AND ExpiresAt>@Now AND AbsoluteUntil>@Now;
SELECT @purpose=Purpose,@kind=ResourceKind,@resource=ResourceId,@pending=EncryptedSeed FROM m0.Challenges WHERE ChallengeId=@ChallengeId AND UserId=@UserId AND SessionId=@SessionId AND Intent=@Intent AND SecurityVersion=@sv AND RequestNonce=@Nonce AND ExpiresAt>@Now AND ConsumedAt IS NULL AND Attempts<5;
IF @sv IS NULL OR @parentUntil IS NULL OR NOT EXISTS(SELECT 1 FROM m0.Challenges WHERE ChallengeId=@ChallengeId AND SessionId=@SessionId AND UserId=@UserId AND Intent=@Intent AND SecurityVersion=@sv AND RequestNonce=@Nonce AND ExpiresAt>@Now AND ConsumedAt IS NULL AND Attempts<5) THROW 51004,'MFA_PROOF_INVALID',1;
IF @Step<CAST(DATEDIFF_BIG(second,'19700101',@Now)/30 AS bigint)-1 OR @Step>CAST(DATEDIFF_BIG(second,'19700101',@Now)/30 AS bigint)+1 THROW 51004,'MFA_PROOF_INVALID',1;
IF @Intent='enroll' BEGIN
IF @pending IS NULL OR (@seed IS NOT NULL AND NOT EXISTS(SELECT 1 FROM m0.RecentProofs WHERE SessionId=@SessionId AND SecurityVersion=@sv AND Purpose='authenticator.replace' AND ResourceKind='Credential' AND ResourceId=(SELECT CredentialId FROM m0.Users WHERE UserId=@UserId) AND ValidUntil>@Now)) THROW 51004,'MFA_PROOF_INVALID',1;
IF (SELECT COUNT(*) FROM OPENJSON(@CodeHashes))<>10 THROW 51004,'MFA_PROOF_INVALID',1;
UPDATE m0.Users SET EncryptedSeed=@pending,CredentialId=@CredentialId,LastAcceptedStep=@Step,SecurityVersion=SecurityVersion+1,MfaVersion=MfaVersion+1,Status=CASE WHEN Status='RebindRequired' THEN 'Active' ELSE Status END WHERE UserId=@UserId;
UPDATE m0.RecoveryCodes SET RevokedAt=@Now WHERE UserId=@UserId AND RevokedAt IS NULL;
INSERT m0.RecoveryCodes(UserId,SetVersion,CodeHash) SELECT @UserId,(SELECT MfaVersion FROM m0.Users WHERE UserId=@UserId),value FROM OPENJSON(@CodeHashes);
UPDATE m0.Sessions SET RevokedAt=@Now WHERE UserId=@UserId AND RevokedAt IS NULL;DELETE m0.RecentProofs WHERE UserId=@UserId;UPDATE m0.Challenges SET ConsumedAt=@Now WHERE UserId=@UserId AND ConsumedAt IS NULL;
END ELSE BEGIN
IF @seed IS NULL OR @Step<=(SELECT LastAcceptedStep FROM m0.Users WHERE UserId=@UserId) THROW 51004,'MFA_PROOF_INVALID',1;
UPDATE m0.Users SET LastAcceptedStep=@Step WHERE UserId=@UserId;
UPDATE m0.Challenges SET ConsumedAt=@Now WHERE ChallengeId=@ChallengeId;
IF @Intent='login' BEGIN
IF NOT EXISTS(SELECT 1 FROM m0.Users WHERE UserId=@UserId AND EmailConfirmed=1) OR NOT EXISTS(SELECT 1 FROM m0.Sessions WHERE SessionId=@SessionId AND Stage='AdminPasswordVerified') THROW 51004,'MFA_PROOF_INVALID',1;
UPDATE m0.Sessions SET RevokedAt=@Now WHERE SessionId=@SessionId;
INSERT m0.Sessions(SessionId,UserId,SecurityVersion,Stage,ExpiresAt,AbsoluteUntil,RequestNonce,MfaVerifiedAt) VALUES(@NewSessionId,@UserId,@sv,'AdminMfaVerified',DATEADD(minute,15,@Now),DATEADD(hour,8,@Now),@NewNonce,@Now);
END ELSE IF @Intent='reauth' BEGIN
IF NOT EXISTS(SELECT 1 FROM m0.Sessions WHERE SessionId=@SessionId AND Stage='AdminMfaVerified') THROW 51004,'MFA_PROOF_INVALID',1;
DELETE m0.RecentProofs WHERE SessionId=@SessionId AND Purpose=@purpose AND ResourceKind=@kind AND ResourceId=@resource;
INSERT m0.RecentProofs(SessionId,UserId,SecurityVersion,Purpose,ResourceKind,ResourceId,RequestNonce,VerifiedAt,ValidUntil) VALUES(@SessionId,@UserId,@sv,@purpose,@kind,@resource,@Nonce,@Now,DATEADD(minute,5,@Now));
END ELSE THROW 51004,'MFA_PROOF_INVALID',1;
END;
INSERT m0.SecurityAudit(UserId,Action,At,SecurityVersion) SELECT UserId,@Intent,@Now,SecurityVersion FROM m0.Users WHERE UserId=@UserId;END;
GO
CREATE OR ALTER PROCEDURE m0.security_accept_recovery @UserId nvarchar(100),@SessionId nvarchar(100),@ChallengeId nvarchar(100),@Nonce nvarchar(160),@CodeHash varchar(64),@Now datetimeoffset,@NewSessionId nvarchar(100),@NewNonce nvarchar(160) AS BEGIN SET NOCOUNT ON;
IF @@TRANCOUNT=0 OR APPLOCK_MODE('public','kohami.m0.security','Transaction')<>'Exclusive' THROW 51000,'SECURITY_TRANSACTION_REQUIRED',1;
DECLARE @sv bigint;SELECT @sv=SecurityVersion FROM m0.Users WHERE UserId=@UserId AND SecurityHold=0 AND Status IN('Active','SetupOnly');
IF NOT EXISTS(SELECT 1 FROM m0.Sessions WHERE SessionId=@SessionId AND UserId=@UserId AND Stage='AdminPasswordVerified' AND SecurityVersion=@sv AND RevokedAt IS NULL AND ExpiresAt>@Now AND AbsoluteUntil>@Now) OR NOT EXISTS(SELECT 1 FROM m0.Challenges WHERE ChallengeId=@ChallengeId AND SessionId=@SessionId AND UserId=@UserId AND Intent='recovery' AND SecurityVersion=@sv AND RequestNonce=@Nonce AND ExpiresAt>@Now AND ConsumedAt IS NULL AND Attempts<5) OR NOT EXISTS(SELECT 1 FROM m0.RecoveryCodes WHERE UserId=@UserId AND CodeHash=@CodeHash AND SetVersion=(SELECT MfaVersion FROM m0.Users WHERE UserId=@UserId) AND UsedAt IS NULL AND RevokedAt IS NULL) THROW 51004,'MFA_PROOF_INVALID',1;
UPDATE m0.RecoveryCodes SET UsedAt=@Now WHERE UserId=@UserId AND CodeHash=@CodeHash AND UsedAt IS NULL;
UPDATE m0.RecoveryCodes SET RevokedAt=@Now WHERE UserId=@UserId AND RevokedAt IS NULL;
UPDATE m0.Users SET EncryptedSeed=NULL,CredentialId=NULL,LastAcceptedStep=-1,SecurityVersion=SecurityVersion+1,MfaVersion=MfaVersion+1,RecoveryEpoch=RecoveryEpoch+1,Status='RebindRequired' WHERE UserId=@UserId;
UPDATE m0.Sessions SET RevokedAt=@Now WHERE UserId=@UserId AND RevokedAt IS NULL;UPDATE m0.Challenges SET ConsumedAt=@Now WHERE UserId=@UserId AND ConsumedAt IS NULL;DELETE m0.RecentProofs WHERE UserId=@UserId;
INSERT m0.Sessions(SessionId,UserId,SecurityVersion,Stage,ExpiresAt,AbsoluteUntil,RequestNonce) VALUES(@NewSessionId,@UserId,@sv+1,'RecoveryRebind',DATEADD(minute,5,@Now),DATEADD(minute,5,@Now),@NewNonce);
INSERT m0.SecurityAudit(UserId,Action,At,SecurityVersion) VALUES(@UserId,'RecoveryRebind',@Now,@sv+1);END;
GO
CREATE OR ALTER PROCEDURE m0.security_get_grant @GrantId nvarchar(100) AS BEGIN SET NOCOUNT ON;SELECT (SELECT * FROM m0.Grants WHERE GrantId=@GrantId FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);END;
GO
CREATE OR ALTER PROCEDURE m0.security_revoke_grant @GrantId nvarchar(100),@Actor nvarchar(100),@ActorSessionId nvarchar(100),@ActorSecurityVersion bigint,@Version bigint,@Reason nvarchar(500),@Now datetimeoffset AS BEGIN SET NOCOUNT ON;
IF @@TRANCOUNT=0 OR APPLOCK_MODE('public','kohami.m0.security','Transaction')<>'Exclusive' THROW 51000,'SECURITY_TRANSACTION_REQUIRED',1;
DECLARE @u nvarchar(100),@role nvarchar(30);SELECT @u=UserId,@role=Role FROM m0.Grants WHERE GrantId=@GrantId AND Version=@Version AND RevokedAt IS NULL;
IF @u IS NULL THROW 51005,'VERSION_CHANGED',1;
IF NOT EXISTS(SELECT 1 FROM m0.Sessions s JOIN m0.Users a ON a.UserId=s.UserId JOIN m0.Grants g ON g.UserId=a.UserId WHERE a.UserId=@Actor AND a.SecurityVersion=@ActorSecurityVersion AND a.Status='Active' AND a.SecurityHold=0 AND a.EmailConfirmed=1 AND a.EncryptedSeed IS NOT NULL AND s.SessionId=@ActorSessionId AND s.SecurityVersion=a.SecurityVersion AND s.Stage='AdminMfaVerified' AND s.RevokedAt IS NULL AND s.ExpiresAt>@Now AND s.AbsoluteUntil>@Now AND g.Role='Owner' AND g.Permission='access.manage' AND g.RevokedAt IS NULL AND g.ValidFrom<=@Now AND g.ValidUntil>@Now AND ((g.ScopeKind='Dataset' AND g.ScopeId='demo_m0') OR (g.ScopeKind='User' AND g.ScopeId=@u))) OR EXISTS(SELECT 1 FROM m0.SecurityState WHERE Maintenance=1) OR NOT EXISTS(SELECT 1 FROM m0.RecentProofs WHERE SessionId=@ActorSessionId AND UserId=@Actor AND SecurityVersion=@ActorSecurityVersion AND Purpose='access.manage' AND ResourceKind='User' AND ResourceId=@u AND ValidUntil>@Now) THROW 51006,'PERMISSION_CHANGED',1;
IF @u=@Actor THROW 51006,'SELF_GRANT_FORBIDDEN',1;
IF @role='Owner' AND NOT EXISTS(SELECT 1 FROM m0.Grants g JOIN m0.Users u ON u.UserId=g.UserId WHERE g.UserId<>@u AND g.Role='Owner' AND g.Permission='access.manage' AND g.ScopeKind='Dataset' AND g.ScopeId='demo_m0' AND g.RevokedAt IS NULL AND g.ValidFrom<=@Now AND g.ValidUntil>@Now AND u.Status='Active' AND u.SecurityHold=0 AND u.EmailConfirmed=1 AND u.EncryptedSeed IS NOT NULL) THROW 51007,'LAST_OWNER_PROTECTED',1;
UPDATE m0.Grants SET RevokedAt=@Now,Version=Version+1,Reason=@Reason WHERE GrantId=@GrantId;UPDATE m0.Users SET SecurityVersion=SecurityVersion+1 WHERE UserId=@u;UPDATE m0.Sessions SET RevokedAt=@Now WHERE UserId=@u AND RevokedAt IS NULL;DELETE m0.RecentProofs WHERE UserId=@u;
INSERT m0.SecurityAudit(UserId,Action,At,SecurityVersion) SELECT UserId,'GrantRevoked',@Now,SecurityVersion FROM m0.Users WHERE UserId=@u;END;
GO
CREATE OR ALTER PROCEDURE m0.security_dispatch_allowed @UserId nvarchar(100),@SecurityVersion bigint,@Permission nvarchar(100),@ResourceKind nvarchar(40),@ResourceId nvarchar(100),@DatasetId nvarchar(100),@Now datetimeoffset AS BEGIN SET NOCOUNT ON;
SELECT CAST(CASE WHEN @Permission IN('access.read','access.manage','user.read','user.manage','orders.read','refund.request','refund.execute','refund.check','shipment.manage','return.inspect','catalog.write','stock.adjust','shipping.configure','audit.read','reconciliation.read','reconciliation.run','reconciliation.check','reconciliation.resolve','reports.read','support.respond','data.handle','demo.control','demo.reset') AND @DatasetId='demo_m0' AND EXISTS(SELECT 1 FROM m0.SecurityState WHERE Maintenance=0) AND EXISTS(SELECT 1 FROM m0.Users u JOIN m0.Grants g ON g.UserId=u.UserId WHERE u.UserId=@UserId AND u.SecurityVersion=@SecurityVersion AND u.Status='Active' AND u.SecurityHold=0 AND u.EmailConfirmed=1 AND u.EncryptedSeed IS NOT NULL AND ((g.Role='Owner' AND g.ScopeKind IN('Dataset','User','Product','Sku','Order','Case','Report')) OR (g.Role='CustomerSupport' AND g.Permission IN('orders.read','refund.request','refund.check','shipment.manage','return.inspect','support.respond','reconciliation.read') AND g.ScopeKind IN('Order','Case')) OR (g.Role='Merchandiser' AND ((g.Permission='catalog.write' AND g.ScopeKind IN('Product','Sku')) OR (g.Permission='stock.adjust' AND g.ScopeKind='Sku'))) OR (g.Role='Analyst' AND g.Permission='reports.read' AND g.ScopeKind='Report')) AND g.Permission=@Permission AND g.RevokedAt IS NULL AND g.ValidFrom<=@Now AND g.ValidUntil>@Now AND ((g.ScopeKind='Dataset' AND g.ScopeId=@DatasetId) OR (g.ScopeId=@ResourceId AND (g.ScopeKind=@ResourceKind OR g.ScopeKind='Case' AND @ResourceKind='RefundCase')))) THEN 1 ELSE 0 END AS bit);END;
GO
CREATE OR ALTER PROCEDURE m0.security_native_inspect AS BEGIN SET NOCOUNT ON;
SELECT (SELECT st.*,JSON_QUERY((SELECT UserId,Status,EmailConfirmed,SecurityVersion,MfaVersion,CASE WHEN EncryptedSeed IS NOT NULL THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END MfaEnabled FROM m0.Users FOR JSON PATH)) Users FROM m0.SecurityState st FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);END;
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
IF NOT EXISTS(SELECT 1 FROM m0.SecurityState WHERE OwnerPrepared=1 AND BootstrapActivated=0 AND BootstrapOperationId IS NOT NULL AND BootstrapUserId=@UserId AND Maintenance=1) OR NOT EXISTS(SELECT 1 FROM m0.Users u JOIN m0.Sessions s ON s.UserId=u.UserId WHERE u.UserId=@UserId AND u.SecurityHold=0 AND u.Status IN('SetupOnly','Active') AND u.EmailConfirmed=1 AND u.EncryptedSeed IS NOT NULL AND s.SecurityVersion=u.SecurityVersion AND s.Stage='AdminMfaVerified' AND s.RevokedAt IS NULL AND s.ExpiresAt>SYSUTCDATETIME() AND s.AbsoluteUntil>SYSUTCDATETIME() AND s.MfaVerifiedAt>DATEADD(minute,-5,SYSUTCDATETIME())) THROW 51010,'OWNER_SETUP_INCOMPLETE',1;
IF NOT EXISTS(SELECT 1 FROM m0.Grants WHERE UserId=@UserId AND Role='Owner' AND RevokedAt IS NULL AND ValidUntil>SYSUTCDATETIME()) THROW 51010,'OWNER_SETUP_INCOMPLETE',1;
UPDATE m0.Users SET Status='Active' WHERE UserId=@UserId;UPDATE m0.SecurityState SET Maintenance=0,MaintenanceTargetUserId=NULL,BootstrapActivated=1 WHERE Id=1;
INSERT m0.SecurityAudit(UserId,Action,At,OperationId,SecurityVersion) SELECT UserId,'InitialOwnerActivate',SYSUTCDATETIME(),@OperationId,SecurityVersion FROM m0.Users WHERE UserId=@UserId;END;
GO
CREATE OR ALTER PROCEDURE m0.security_native_recover @UserId nvarchar(100),@OperationId uniqueidentifier AS BEGIN SET NOCOUNT ON;
IF @@TRANCOUNT=0 OR APPLOCK_MODE('public','kohami.m0.security','Transaction')<>'Exclusive' THROW 51000,'NATIVE_TRANSACTION_REQUIRED',1;
IF NOT EXISTS(SELECT 1 FROM m0.Users WHERE UserId=@UserId AND SecurityHold=0 AND Status IN('Active','SetupOnly','RebindRequired')) OR NOT EXISTS(SELECT 1 FROM m0.Grants WHERE UserId=@UserId AND Role='Owner') THROW 51009,'NATIVE_INPUT_INVALID',1;
UPDATE m0.Users SET Status='RebindRequired',EncryptedSeed=NULL,CredentialId=NULL,SecurityVersion=SecurityVersion+1,MfaVersion=MfaVersion+1,RecoveryEpoch=RecoveryEpoch+1,LastAcceptedStep=-1 WHERE UserId=@UserId;
UPDATE m0.Sessions SET RevokedAt=SYSUTCDATETIME() WHERE UserId=@UserId AND RevokedAt IS NULL;UPDATE m0.Challenges SET ConsumedAt=SYSUTCDATETIME() WHERE UserId=@UserId AND ConsumedAt IS NULL;UPDATE m0.RecoveryCodes SET RevokedAt=SYSUTCDATETIME() WHERE UserId=@UserId AND RevokedAt IS NULL;DELETE m0.RecentProofs WHERE UserId=@UserId;
UPDATE m0.SecurityState SET Maintenance=1,MaintenanceTargetUserId=@UserId WHERE Id=1;
INSERT m0.SecurityAudit(UserId,Action,At,OperationId,SecurityVersion) SELECT UserId,'CredentialRecovery',SYSUTCDATETIME(),@OperationId,SecurityVersion FROM m0.Users WHERE UserId=@UserId;END;
GO
CREATE OR ALTER PROCEDURE m0.security_native_renew @UserId nvarchar(100),@OperationId uniqueidentifier,@ValidUntil datetimeoffset AS BEGIN SET NOCOUNT ON;
IF @@TRANCOUNT=0 OR APPLOCK_MODE('public','kohami.m0.security','Transaction')<>'Exclusive' THROW 51000,'NATIVE_TRANSACTION_REQUIRED',1;
IF @ValidUntil<=SYSUTCDATETIME() OR @ValidUntil>DATEADD(day,7,SYSUTCDATETIME()) OR NOT EXISTS(SELECT 1 FROM m0.Users WHERE UserId=@UserId AND SecurityHold=0 AND Status='Active' AND EncryptedSeed IS NOT NULL AND EmailConfirmed=1) OR EXISTS(SELECT 1 FROM m0.Grants WHERE UserId=@UserId AND Role='Owner' AND RevokedAt IS NOT NULL) THROW 51009,'NATIVE_INPUT_INVALID',1;
UPDATE m0.Grants SET ValidUntil=@ValidUntil,Version=Version+1 WHERE UserId=@UserId AND Role='Owner' AND ScopeKind='Dataset' AND ScopeId='demo_m0' AND RevokedAt IS NULL;
IF @@ROWCOUNT<>23 THROW 51009,'NATIVE_INPUT_INVALID',1;
UPDATE m0.Users SET SecurityVersion=SecurityVersion+1 WHERE UserId=@UserId;UPDATE m0.Sessions SET RevokedAt=SYSUTCDATETIME() WHERE UserId=@UserId AND RevokedAt IS NULL;DELETE m0.RecentProofs WHERE UserId=@UserId;
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
UPDATE m0.Users SET SecurityVersion=SecurityVersion+1,Status='Suspended' WHERE UserId=@OldUserId;UPDATE m0.Sessions SET RevokedAt=SYSUTCDATETIME() WHERE UserId=@OldUserId AND RevokedAt IS NULL;DELETE m0.RecentProofs WHERE UserId=@OldUserId;
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
