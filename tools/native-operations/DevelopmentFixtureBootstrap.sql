IF OBJECT_ID('ops.DevelopmentFixtureMarker','U') IS NULL
CREATE TABLE ops.DevelopmentFixtureMarker(
 FixtureId uniqueidentifier NOT NULL PRIMARY KEY,
 DatasetId nvarchar(40) NOT NULL CHECK(DatasetId='demo_m0'),
 InstanceFingerprint char(64) NOT NULL,
 Enabled bit NOT NULL DEFAULT 1,
 BootstrapConsumed bit NOT NULL DEFAULT 0,
 CreatedAt datetimeoffset NOT NULL DEFAULT SYSUTCDATETIME()
);
GO
IF OBJECT_ID('ops.DevelopmentFixtureBootstrapUse','U') IS NULL
CREATE TABLE ops.DevelopmentFixtureBootstrapUse(
 OperationId uniqueidentifier NOT NULL PRIMARY KEY,
 ActivationHash char(64) NOT NULL UNIQUE,
 FixtureId uniqueidentifier NOT NULL,
 InstanceFingerprint char(64) NOT NULL,
 ConsumedAt datetimeoffset NOT NULL DEFAULT SYSUTCDATETIME()
);
GO
CREATE OR ALTER PROCEDURE ops.InspectDevelopmentFixture
 @FixtureId uniqueidentifier
WITH EXECUTE AS OWNER
AS
BEGIN
 SET NOCOUNT ON;
 IF DB_NAME()<>'KohamiM0' OR ORIGINAL_LOGIN()<>'kohami_m0_fixture_bootstrap' OR APP_NAME()<>'KOHAMI-DevFixtureBootstrap'
  THROW 51100,'DEV_FIXTURE_TARGET_REJECTED',1;
 SELECT m.FixtureId,m.DatasetId,m.InstanceFingerprint,m.Enabled,m.BootstrapConsumed,
   s.Maintenance,s.OwnerPrepared,s.BootstrapActivated,
   CONVERT(bit,CASE WHEN EXISTS(SELECT 1 FROM m0.Grants g WHERE g.Role='Owner' AND g.RevokedAt IS NULL) THEN 1 ELSE 0 END) OwnerExists,
   (SELECT COUNT(*) FROM m0.Grants g WHERE g.Role='Owner' AND g.RevokedAt IS NULL AND g.ValidUntil>SYSUTCDATETIME()) OwnerGrantCount,
   CONVERT(bit,CASE WHEN EXISTS(SELECT 1 FROM m0.SecurityAudit a WHERE a.Action='DevelopmentFixtureOwnerBootstrap') THEN 1 ELSE 0 END) BootstrapAuditExists
 FROM ops.DevelopmentFixtureMarker m CROSS JOIN m0.SecurityState s
 WHERE m.FixtureId=@FixtureId AND s.Id=1;
END;
GO
CREATE OR ALTER PROCEDURE ops.DevelopmentFixtureBootstrapOwner
 @FixtureId uniqueidentifier,
 @InstanceFingerprint char(64),
 @OperationId uniqueidentifier,
 @ActivationHash char(64),
 @ExpiresAt datetimeoffset,
 @UserId nvarchar(100),
 @Email nvarchar(254),
 @PasswordHash nvarchar(1000),
 @EncryptedTotpSeed nvarchar(max)
WITH EXECUTE AS OWNER
AS
BEGIN
 SET NOCOUNT ON;
 SET XACT_ABORT ON;
 DECLARE @now datetimeoffset=SYSUTCDATETIME();
 DECLARE @actualFingerprint char(64)=LOWER(CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CONVERT(nvarchar(100),SERVERPROPERTY('ServerName')),'|KohamiM0|demo_m0')),2));
 IF DB_NAME()<>'KohamiM0' OR ORIGINAL_LOGIN()<>'kohami_m0_fixture_bootstrap'
  THROW 51100,'DEV_FIXTURE_TARGET_REJECTED',1;
 IF APP_NAME()<>'KOHAMI-DevFixtureBootstrap'
  THROW 51100,'DEV_FIXTURE_CLIENT_REJECTED',1;
 IF @InstanceFingerprint<>@actualFingerprint OR @ActivationHash NOT LIKE REPLICATE('[0-9a-f]',64)
  THROW 51100,'DEV_FIXTURE_PROOF_REJECTED',1;
 IF @ExpiresAt<=@now OR @ExpiresAt>DATEADD(minute,10,@now)
  THROW 51100,'DEV_FIXTURE_ACTIVATION_EXPIRED',1;
 IF @UserId NOT LIKE 'devfixture_owner[_]%' OR @Email NOT LIKE '%@example.test' OR LEN(@Email)>254
  OR LEN(@PasswordHash)<80 OR NULLIF(@EncryptedTotpSeed,N'') IS NULL
  THROW 51100,'DEV_FIXTURE_IDENTITY_REJECTED',1;

 BEGIN TRY
  BEGIN TRANSACTION;
  DECLARE @lock int;
  EXEC @lock=sys.sp_getapplock @Resource='kohami.m0.security',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
  IF @lock<0 THROW 51100,'DEV_FIXTURE_LOCK_FAILED',1;
  IF NOT EXISTS(SELECT 1 FROM ops.DevelopmentFixtureMarker WITH(UPDLOCK,HOLDLOCK)
      WHERE FixtureId=@FixtureId AND DatasetId='demo_m0' AND InstanceFingerprint=@actualFingerprint
        AND Enabled=1 AND BootstrapConsumed=0)
   THROW 51100,'DEV_FIXTURE_MARKER_REJECTED',1;
  IF EXISTS(SELECT 1 FROM ops.DevelopmentFixtureBootstrapUse WHERE OperationId=@OperationId OR ActivationHash=@ActivationHash)
   THROW 51100,'DEV_FIXTURE_REPLAY_REJECTED',1;
  IF EXISTS(SELECT 1 FROM m0.Grants WHERE Role='Owner' AND RevokedAt IS NULL)
    OR EXISTS(SELECT 1 FROM m0.SecurityAudit WHERE Action IN('InitialOwnerPrepare','InitialOwnerActivate','DevelopmentFixtureOwnerBootstrap'))
   THROW 51100,'DEV_FIXTURE_OWNER_EXISTS',1;
  IF NOT EXISTS(SELECT 1 FROM m0.SecurityState WHERE Id=1 AND Maintenance=1 AND OwnerPrepared=0
      AND BootstrapActivated=0 AND BootstrapOperationId IS NULL AND BootstrapUserId IS NULL)
   THROW 51100,'DEV_FIXTURE_MAINTENANCE_STATE_REJECTED',1;
  IF EXISTS(SELECT 1 FROM m0.Users WHERE UserId=@UserId OR Email=@Email)
   THROW 51100,'DEV_FIXTURE_IDENTITY_EXISTS',1;

  INSERT ops.DevelopmentFixtureBootstrapUse(OperationId,ActivationHash,FixtureId,InstanceFingerprint)
   VALUES(@OperationId,@ActivationHash,@FixtureId,@actualFingerprint);
  INSERT m0.Users(UserId,Email,PasswordHash,EmailConfirmed,SecurityHold,Status,SecurityVersion,EncryptedSeed)
   VALUES(@UserId,@Email,@PasswordHash,1,0,'Active',1,@EncryptedTotpSeed);
  INSERT m0.Grants(GrantId,UserId,Role,Permission,ScopeKind,ScopeId,ValidFrom,ValidUntil,GrantedBy,Reason)
   SELECT 'fixture_'+CONVERT(nvarchar(36),NEWID()),@UserId,'Owner',p.Permission,'Dataset','demo_m0',@now,DATEADD(hour,2,@now),'dev_fixture_bootstrap','isolated Issue 02 acceptance'
   FROM (VALUES('orders.read'),('shipment.manage'),('support.respond'),('return.inspect'),('refund.execute'),('refund.check'),('demo.control')) p(Permission);
  INSERT m0.SecurityAudit(UserId,Action,At,OperationId,SecurityVersion)
   VALUES(@UserId,'DevelopmentFixtureOwnerBootstrap',@now,@OperationId,1);
  UPDATE ops.DevelopmentFixtureMarker SET BootstrapConsumed=1,Enabled=0 WHERE FixtureId=@FixtureId;
  UPDATE m0.SecurityState SET Maintenance=0,MaintenanceTargetUserId=NULL WHERE Id=1;
  COMMIT TRANSACTION;
 END TRY
 BEGIN CATCH
  IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
  THROW;
 END CATCH;
END;
GO
IF DATABASE_PRINCIPAL_ID('m0_fixture_bootstrap') IS NULL CREATE ROLE m0_fixture_bootstrap;
GO
GRANT EXECUTE ON OBJECT::ops.DevelopmentFixtureBootstrapOwner TO m0_fixture_bootstrap;
GRANT EXECUTE ON OBJECT::ops.InspectDevelopmentFixture TO m0_fixture_bootstrap;
GO
