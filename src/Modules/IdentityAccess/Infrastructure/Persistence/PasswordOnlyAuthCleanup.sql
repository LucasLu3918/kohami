-- Reissue sessions against the current password-authenticated session contract.
DECLARE @now datetimeoffset=SYSUTCDATETIME();
DECLARE @constraint sysname,@sql nvarchar(max);
WHILE EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('m0.Sessions') AND definition LIKE '%AdminPasswordVerified%')
BEGIN
 SELECT TOP(1) @constraint=name,@sql=N'ALTER TABLE m0.Sessions DROP CONSTRAINT '+QUOTENAME(name)
 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('m0.Sessions') AND definition LIKE '%AdminPasswordVerified%';
 EXEC sys.sp_executesql @sql;
END;
WHILE EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('m0.Users') AND definition LIKE '%RebindRequired%')
BEGIN
 SELECT TOP(1) @constraint=name,@sql=N'ALTER TABLE m0.Users DROP CONSTRAINT '+QUOTENAME(name)
 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('m0.Users') AND definition LIKE '%RebindRequired%';
 EXEC sys.sp_executesql @sql;
END;
IF COL_LENGTH('m0.Sessions','MfaVerifiedAt') IS NOT NULL
 EXEC sys.sp_executesql N'UPDATE m0.Sessions SET AuthenticatedAt=COALESCE(AuthenticatedAt,MfaVerifiedAt,@now);',N'@now datetimeoffset',@now;
ELSE UPDATE m0.Sessions SET AuthenticatedAt=COALESCE(AuthenticatedAt,@now);
UPDATE m0.Sessions SET Stage='AdminAuthenticated',RevokedAt=COALESCE(RevokedAt,@now)
 WHERE Stage IN('AdminPasswordVerified','RecoveryRebind');
UPDATE m0.Sessions SET Stage='AdminAuthenticated' WHERE Stage='AdminMfaVerified';
UPDATE m0.Users SET Status=CASE WHEN EmailConfirmed=1 THEN 'Active' ELSE 'Suspended' END WHERE Status='RebindRequired';
ALTER TABLE m0.Sessions ALTER COLUMN AuthenticatedAt datetimeoffset NOT NULL;
ALTER TABLE m0.Users ADD CONSTRAINT CK_Users_Status_PasswordOnly CHECK(Status IN('Active','SetupOnly','Suspended','Closed'));
ALTER TABLE m0.Sessions ADD CONSTRAINT CK_Sessions_Stage_PasswordOnly CHECK(Stage IN('AdminAuthenticated','MemberAuthenticated'));
GO
IF OBJECT_ID('m0.RecentProofs','U') IS NOT NULL DROP TABLE m0.RecentProofs;
IF OBJECT_ID('m0.RecoveryCodes','U') IS NOT NULL DROP TABLE m0.RecoveryCodes;
IF OBJECT_ID('ops.DevelopmentFixtureBootstrapOwner','P') IS NOT NULL DROP PROCEDURE ops.DevelopmentFixtureBootstrapOwner;
IF OBJECT_ID('ops.InspectDevelopmentFixture','P') IS NOT NULL DROP PROCEDURE ops.InspectDevelopmentFixture;
IF OBJECT_ID('ops.DevelopmentFixtureBootstrapUse','U') IS NOT NULL DROP TABLE ops.DevelopmentFixtureBootstrapUse;
GO
DECLARE @table sysname,@column sysname,@default sysname,@sql nvarchar(max);
DECLARE defaults CURSOR LOCAL FAST_FORWARD FOR
 SELECT QUOTENAME(OBJECT_SCHEMA_NAME(c.object_id))+N'.'+QUOTENAME(OBJECT_NAME(c.object_id)),c.name,dc.name
 FROM sys.columns c JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id
 WHERE (c.object_id=OBJECT_ID('m0.Users') AND c.name IN('MfaVersion','LastAcceptedStep','RecoveryEpoch'))
    OR (c.object_id=OBJECT_ID('ops.DevelopmentFixtureMarker') AND c.name IN('Enabled','BootstrapConsumed'));
OPEN defaults; FETCH NEXT FROM defaults INTO @table,@column,@default;
WHILE @@FETCH_STATUS=0
BEGIN
 SET @sql=N'ALTER TABLE '+@table+N' DROP CONSTRAINT '+QUOTENAME(@default); EXEC sys.sp_executesql @sql;
 FETCH NEXT FROM defaults INTO @table,@column,@default;
END;
CLOSE defaults; DEALLOCATE defaults;
IF COL_LENGTH('m0.Users','MfaVersion') IS NOT NULL ALTER TABLE m0.Users DROP COLUMN MfaVersion;
IF COL_LENGTH('m0.Users','EncryptedSeed') IS NOT NULL ALTER TABLE m0.Users DROP COLUMN EncryptedSeed;
IF COL_LENGTH('m0.Users','CredentialId') IS NOT NULL ALTER TABLE m0.Users DROP COLUMN CredentialId;
IF COL_LENGTH('m0.Users','LastAcceptedStep') IS NOT NULL ALTER TABLE m0.Users DROP COLUMN LastAcceptedStep;
IF COL_LENGTH('m0.Users','RecoveryEpoch') IS NOT NULL ALTER TABLE m0.Users DROP COLUMN RecoveryEpoch;
IF COL_LENGTH('m0.Challenges','EncryptedSeed') IS NOT NULL ALTER TABLE m0.Challenges DROP COLUMN EncryptedSeed;
IF COL_LENGTH('m0.Challenges','Purpose') IS NOT NULL ALTER TABLE m0.Challenges DROP COLUMN Purpose;
IF COL_LENGTH('m0.Challenges','ResourceKind') IS NOT NULL ALTER TABLE m0.Challenges DROP COLUMN ResourceKind;
IF COL_LENGTH('m0.Challenges','ResourceId') IS NOT NULL ALTER TABLE m0.Challenges DROP COLUMN ResourceId;
IF COL_LENGTH('ops.DevelopmentFixtureMarker','Enabled') IS NOT NULL ALTER TABLE ops.DevelopmentFixtureMarker DROP COLUMN Enabled;
IF COL_LENGTH('ops.DevelopmentFixtureMarker','BootstrapConsumed') IS NOT NULL ALTER TABLE ops.DevelopmentFixtureMarker DROP COLUMN BootstrapConsumed;
IF DATABASE_PRINCIPAL_ID('kohami_m0_fixture_bootstrap') IS NOT NULL DROP USER kohami_m0_fixture_bootstrap;
IF DATABASE_PRINCIPAL_ID('m0_fixture_bootstrap') IS NOT NULL DROP ROLE m0_fixture_bootstrap;
GO
