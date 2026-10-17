IF DB_NAME() <> 'KohamiM0' THROW 50000,'DATABASE_SCOPE_INVALID',1;
IF OBJECT_ID('m0commerce.ReportAccessAudit') IS NULL
BEGIN
 CREATE TABLE m0commerce.ReportAccessAudit (
  Id bigint IDENTITY NOT NULL PRIMARY KEY,
  Dataset varchar(30) NOT NULL CHECK(Dataset='demo_m0'),
  ActorUserId nvarchar(100) NULL,
  RequestedAt datetimeoffset NOT NULL,
  AuthorizationOutcome varchar(10) NOT NULL CHECK(AuthorizationOutcome IN ('Authorized','Denied')),
  FromDate date NULL,
  ToDate date NULL,
  CHECK((FromDate IS NULL AND ToDate IS NULL) OR (FromDate IS NOT NULL AND ToDate IS NOT NULL AND ToDate>=FromDate)));
 CREATE INDEX IX_ReportAccessAudit_RequestedAt ON m0commerce.ReportAccessAudit(RequestedAt);
END;
GO
CREATE OR ALTER PROCEDURE m0commerce.purge_report_access_audit @Now datetimeoffset AS
BEGIN
 SET NOCOUNT ON;
 DELETE FROM m0commerce.ReportAccessAudit WHERE RequestedAt<DATEADD(year,-1,@Now);
END;
GO
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_AuthFailures_At' AND object_id=OBJECT_ID('m0.AuthFailures'))
 CREATE INDEX IX_AuthFailures_At ON m0.AuthFailures(At);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_SecurityAudit_At' AND object_id=OBJECT_ID('m0.SecurityAudit'))
 CREATE INDEX IX_SecurityAudit_At ON m0.SecurityAudit(At);
GO
CREATE OR ALTER PROCEDURE m0.security_purge_expired_logs @Now datetimeoffset AS
BEGIN
 SET NOCOUNT ON;
 IF @@TRANCOUNT=0 OR APPLOCK_MODE('public','kohami.m0.security','Transaction')<>'Exclusive' THROW 51000,'SECURITY_TRANSACTION_REQUIRED',1;
 DELETE FROM m0.AuthFailures WHERE At<DATEADD(day,-90,@Now);
 DELETE FROM m0.SecurityAudit WHERE At<DATEADD(year,-1,@Now);
END;
GO
GRANT INSERT ON OBJECT::m0commerce.ReportAccessAudit TO m0_api;
GRANT EXECUTE ON OBJECT::m0commerce.purge_report_access_audit TO m0_worker;
GRANT EXECUTE ON OBJECT::m0.security_purge_expired_logs TO m0_worker;
