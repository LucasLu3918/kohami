IF OBJECT_ID('ops.PermitUses') IS NULL CREATE TABLE ops.PermitUses(PermitId uniqueidentifier NOT NULL PRIMARY KEY,OperationId uniqueidentifier NOT NULL UNIQUE,Action nvarchar(80) NOT NULL,Target nvarchar(160) NOT NULL,PreviewHash char(64) NOT NULL,Epoch bigint NOT NULL,ConsumedAt datetimeoffset NOT NULL);
IF OBJECT_ID('ops.Audit') IS NULL CREATE TABLE ops.Audit(OperationId uniqueidentifier NOT NULL PRIMARY KEY,Action nvarchar(80) NOT NULL,Target nvarchar(160) NOT NULL,PreviewHash char(64) NOT NULL,OldEpoch bigint NOT NULL,NewEpoch bigint NOT NULL,AppliedAt datetimeoffset NOT NULL,HostReconciled bit NOT NULL DEFAULT 0,ReplacementTarget nvarchar(100) NULL);
IF OBJECT_ID('ops.ReconcileUses') IS NULL CREATE TABLE ops.ReconcileUses(PermitId uniqueidentifier NOT NULL PRIMARY KEY,OperationId uniqueidentifier NOT NULL,PreviewHash char(64) NOT NULL,Action nvarchar(80) NOT NULL,Target nvarchar(160) NOT NULL,Epoch bigint NOT NULL,ConsumedAt datetimeoffset NOT NULL);
GO
CREATE OR ALTER PROCEDURE ops.Inspect
AS
BEGIN
 SET NOCOUNT ON;
 SELECT LOWER(CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CONVERT(nvarchar(100),SERVERPROPERTY('ServerName')),'|KohamiM0|demo_m0')),2)) Fingerprint,'demo_m0' DatasetId,DB_NAME() DatabaseName,CONVERT(nvarchar(100),SERVERPROPERTY('ServerName')) ServerName,Epoch,Maintenance,OwnerPrepared FROM m0.SecurityState WHERE Id=1;
END;
GO
CREATE OR ALTER PROCEDURE ops.Apply
 @PermitId uniqueidentifier,@OperationId uniqueidentifier,@Action nvarchar(80),@Target nvarchar(160),@Dataset nvarchar(100),@Fingerprint char(64),@PreviewHash char(64),@Epoch bigint,@IssuedAt datetimeoffset,@ExpiresAt datetimeoffset,@Email nvarchar(320)=NULL,@PasswordHash nvarchar(max)=NULL,@NewUserId nvarchar(100)=NULL
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 IF DB_NAME()<>'KohamiM0' OR @Dataset<>'demo_m0' OR @Fingerprint<>LOWER(CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CONVERT(nvarchar(100),SERVERPROPERTY('ServerName')),'|KohamiM0|demo_m0')),2)) THROW 51000,'NATIVE_SCOPE_INVALID',1;
 IF @IssuedAt>SYSDATETIMEOFFSET() OR @ExpiresAt<=SYSDATETIMEOFFSET() OR DATEDIFF(second,@IssuedAt,@ExpiresAt)<>600 THROW 51000,'PERMIT_EXPIRED',1;
 IF @Action NOT IN ('InitialOwnerPrepare','InitialOwnerActivate','CredentialRecovery','RenewDemoOwnerGrant','TransferDemoOwner','RotateKeyMaterial','RestoreSecurityCheckpoint') THROW 51000,'NATIVE_ACTION_INVALID',1;
 BEGIN TRANSACTION;
 DECLARE @lock int;
 EXEC @lock=sys.sp_getapplock @Resource='kohami.m0.security',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
 IF @lock<0 THROW 51000,'NATIVE_LOCK_FAILED',1;
 IF EXISTS(SELECT 1 FROM ops.PermitUses WHERE PermitId=@PermitId) THROW 51000,'PERMIT_REPLAY',1;
 IF NOT EXISTS(SELECT 1 FROM m0.SecurityState WITH(UPDLOCK,HOLDLOCK) WHERE Id=1 AND Epoch=@Epoch) THROW 51000,'NATIVE_EPOCH_CHANGED',1;
 UPDATE m0.SecurityState SET Maintenance=1,Epoch=Epoch+1,MaintenanceTargetUserId=NULL WHERE Id=1;
 INSERT ops.PermitUses VALUES(@PermitId,@OperationId,@Action,@Target,@PreviewHash,@Epoch,SYSDATETIMEOFFSET());
 IF @Action='InitialOwnerPrepare' EXEC m0.security_native_prepare_owner @Target,@Email,@PasswordHash,@OperationId;
 ELSE IF @Action='InitialOwnerActivate' EXEC m0.security_native_activate_owner @Target,@OperationId;
 ELSE IF @Action='CredentialRecovery' EXEC m0.security_native_recover @Target,@OperationId;
 ELSE IF @Action='RenewDemoOwnerGrant'
 BEGIN
  DECLARE @until datetimeoffset=DATEADD(day,7,SYSDATETIMEOFFSET());
  EXEC m0.security_native_renew @Target,@OperationId,@until;
 END
 ELSE IF @Action='TransferDemoOwner' EXEC m0.security_native_transfer @Target,@NewUserId,@Email,@PasswordHash,@OperationId;
 ELSE IF @Action='RotateKeyMaterial'
 BEGIN
  IF @Target <> 'SESSION_DP_RING' THROW 51000,'KEY_ROTATION_TARGET_INVALID',1;
  -- Host rotation happens after this checkpoint; maintenance remains closed until exact
  -- new-write/old-read/sentinel and backup dependencies are verified by native resume.
 END
 ELSE IF @Action='RestoreSecurityCheckpoint'
 BEGIN
  -- Never RESTORE or enable a clone here. A separate offline verified-new-target tool
  -- records the checkpoint. Missing post-cutoff ledger is rejected by host RestoreGuard.
  IF @Target NOT LIKE 'KohamiM0Restore[_]%' THROW 51000,'RESTORE_NEW_TARGET_REQUIRED',1;
 END
 INSERT ops.Audit(OperationId,Action,Target,PreviewHash,OldEpoch,NewEpoch,AppliedAt,ReplacementTarget) VALUES(@OperationId,@Action,@Target,@PreviewHash,@Epoch,@Epoch+1,SYSDATETIMEOFFSET(),@NewUserId);
 -- Activation is not yet dispatchable until host audit reconciliation succeeds.
 UPDATE m0.SecurityState SET Maintenance=1,MaintenanceTargetUserId=CASE
  WHEN @Action IN('InitialOwnerPrepare','InitialOwnerActivate','CredentialRecovery','RenewDemoOwnerGrant') THEN @Target
  WHEN @Action='TransferDemoOwner' THEN @NewUserId
  ELSE NULL END WHERE Id=1;
 COMMIT;
 SELECT @OperationId OperationId,@Epoch+1 Epoch,'CommittedAuditPending' Status;
END;
GO
CREATE OR ALTER PROCEDURE ops.InspectAudit @OperationId uniqueidentifier,@PreviewHash char(64)
AS
BEGIN
 SET NOCOUNT ON;
 SELECT Action,Target,NewEpoch FROM ops.Audit WHERE OperationId=@OperationId AND PreviewHash=@PreviewHash;
END;
GO
CREATE OR ALTER PROCEDURE ops.Reconcile @PermitId uniqueidentifier,@OperationId uniqueidentifier,@Action nvarchar(80),@Target nvarchar(160),@Dataset nvarchar(100),@Fingerprint char(64),@PreviewHash char(64),@PermitEpoch bigint,@Epoch bigint,@IssuedAt datetimeoffset,@ExpiresAt datetimeoffset
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 BEGIN TRANSACTION;
 DECLARE @lock int;EXEC @lock=sys.sp_getapplock @Resource='kohami.m0.security',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
 IF @lock<0 THROW 51000,'NATIVE_LOCK_FAILED',1;
 IF NOT EXISTS(SELECT 1 FROM ops.Audit WITH(UPDLOCK,HOLDLOCK) WHERE OperationId=@OperationId AND PreviewHash=@PreviewHash) THROW 51000,'HOST_CHECKPOINT_MISSING',1;
 IF DB_NAME()<>'KohamiM0' OR @Dataset<>'demo_m0' OR @Fingerprint<>LOWER(CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CONVERT(nvarchar(100),SERVERPROPERTY('ServerName')),'|KohamiM0|demo_m0')),2)) THROW 51000,'NATIVE_SCOPE_INVALID',1;
 IF @IssuedAt>SYSDATETIMEOFFSET() OR @ExpiresAt<=SYSDATETIMEOFFSET() OR DATEDIFF(second,@IssuedAt,@ExpiresAt)<>600 THROW 51000,'PERMIT_EXPIRED',1;
 IF @PermitEpoch NOT IN(@Epoch,@Epoch-1) OR NOT EXISTS(SELECT 1 FROM m0.SecurityState WHERE Id=1 AND Epoch=@Epoch) OR NOT EXISTS(SELECT 1 FROM ops.Audit a WHERE a.OperationId=@OperationId AND a.PreviewHash=@PreviewHash AND a.Action=@Action AND a.Target=@Target AND
  (a.NewEpoch=@Epoch OR (a.Action='TransferDemoOwner' AND a.NewEpoch=@Epoch-1 AND EXISTS(SELECT 1 FROM ops.Audit activation WHERE activation.Action='InitialOwnerActivate' AND activation.Target=a.ReplacementTarget AND activation.NewEpoch=@Epoch AND activation.HostReconciled=1)))) THROW 51000,'NATIVE_EPOCH_CHANGED',1;
 IF EXISTS(SELECT 1 FROM ops.ReconcileUses WHERE PermitId=@PermitId) THROW 51000,'PERMIT_REPLAY',1;
 IF EXISTS(SELECT 1 FROM ops.Audit a WHERE a.OperationId=@OperationId AND a.Action IN('InitialOwnerActivate','CredentialRecovery','RenewDemoOwnerGrant','TransferDemoOwner')
  AND NOT EXISTS(SELECT 1 FROM ops.Audit a2
   JOIN m0.Users u ON u.UserId=COALESCE(a2.ReplacementTarget,a2.Target)
   WHERE a2.OperationId=@OperationId AND u.Status='Active' AND u.EmailConfirmed=1 AND u.SecurityHold=0
   AND EXISTS(SELECT 1 FROM m0.Sessions se WHERE se.UserId=u.UserId AND se.SecurityVersion=u.SecurityVersion AND se.Stage='AdminAuthenticated' AND se.RevokedAt IS NULL AND se.ExpiresAt>SYSDATETIMEOFFSET() AND se.AbsoluteUntil>SYSDATETIMEOFFSET() AND se.AuthenticatedAt>DATEADD(minute,-5,SYSDATETIMEOFFSET()))
   AND EXISTS(SELECT 1 FROM m0.Grants g WHERE g.UserId=u.UserId AND g.Role='Owner' AND g.RevokedAt IS NULL AND g.ValidFrom<=SYSDATETIMEOFFSET() AND g.ValidUntil>SYSDATETIMEOFFSET())))
  THROW 51000,'OWNER_REBIND_INCOMPLETE',1;
 INSERT ops.ReconcileUses(PermitId,OperationId,PreviewHash,Action,Target,Epoch,ConsumedAt) VALUES(@PermitId,@OperationId,@PreviewHash,@Action,@Target,@Epoch,SYSDATETIMEOFFSET());
 UPDATE ops.Audit SET HostReconciled=1 WHERE OperationId=@OperationId;
 IF EXISTS(SELECT 1 FROM ops.Audit a JOIN m0.SecurityState st ON st.Id=1 AND (st.Epoch=a.NewEpoch OR (a.Action='TransferDemoOwner' AND st.Epoch=a.NewEpoch+1))
 JOIN m0.Users u ON u.UserId=COALESCE(a.ReplacementTarget,a.Target)
 WHERE a.OperationId=@OperationId AND a.Action IN('InitialOwnerActivate','CredentialRecovery','RenewDemoOwnerGrant','TransferDemoOwner')
 AND (a.NewEpoch=st.Epoch OR (a.Action='TransferDemoOwner' AND EXISTS(SELECT 1 FROM ops.Audit activation WHERE activation.Action='InitialOwnerActivate' AND activation.Target=a.ReplacementTarget AND activation.NewEpoch=st.Epoch AND activation.HostReconciled=1)))
 AND u.Status='Active' AND u.EmailConfirmed=1 AND u.SecurityHold=0
 AND EXISTS(SELECT 1 FROM m0.Sessions se WHERE se.UserId=u.UserId AND se.SecurityVersion=u.SecurityVersion AND se.Stage='AdminAuthenticated' AND se.RevokedAt IS NULL AND se.ExpiresAt>SYSDATETIMEOFFSET() AND se.AbsoluteUntil>SYSDATETIMEOFFSET() AND se.AuthenticatedAt>DATEADD(minute,-5,SYSDATETIMEOFFSET()))
 AND EXISTS(SELECT 1 FROM m0.Grants g WHERE g.UserId=u.UserId AND g.Role='Owner' AND g.RevokedAt IS NULL AND g.ValidFrom<=SYSDATETIMEOFFSET() AND g.ValidUntil>SYSDATETIMEOFFSET()))
 AND NOT EXISTS(SELECT 1 FROM ops.Audit WHERE HostReconciled=0)
 UPDATE m0.SecurityState SET Maintenance=0,MaintenanceTargetUserId=NULL WHERE Id=1;
 COMMIT;
END;
