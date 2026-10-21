IF OBJECT_ID('ops.Issue01AcceptanceWindows') IS NULL
 CREATE TABLE ops.Issue01AcceptanceWindows(
  WindowId uniqueidentifier NOT NULL PRIMARY KEY,
  Dataset nvarchar(100) NOT NULL,
  Fingerprint char(64) NOT NULL,
  OpenedAt datetimeoffset NOT NULL,
  ExpiresAt datetimeoffset NOT NULL,
  ClosedAt datetimeoffset NULL
 );
GO
IF OBJECT_ID('ops.Issue01FixturePrices') IS NULL
 CREATE TABLE ops.Issue01FixturePrices(
  WindowId uniqueidentifier NOT NULL,
  Sku nvarchar(100) NOT NULL,
  OriginalPrice int NOT NULL,
  CONSTRAINT PK_Issue01FixturePrices PRIMARY KEY(WindowId,Sku)
 );
GO
CREATE OR ALTER PROCEDURE ops.OpenIssue01AcceptanceWindow
 @WindowId uniqueidentifier,@Dataset nvarchar(100),@Fingerprint char(64)
AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;
 IF DB_NAME()<>'Kohami' OR @Dataset<>'kohami_local' OR @Fingerprint<>LOWER(CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CONVERT(nvarchar(100),SERVERPROPERTY('ServerName')),'|Kohami|kohami_local')),2)) THROW 51000,'ISSUE01_FIXTURE_SCOPE_INVALID',1;
 BEGIN TRANSACTION;
 DECLARE @lock int;EXEC @lock=sys.sp_getapplock @Resource='kohami.identity_access.security',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
 IF @lock<0 THROW 51000,'ISSUE01_FIXTURE_LOCK_FAILED',1;
 IF NOT EXISTS(SELECT 1 FROM identity_access.SecurityState WITH(UPDLOCK,HOLDLOCK) WHERE Id=1 AND Maintenance=1)
  THROW 51000,'ISSUE01_FIXTURE_MUST_START_CLOSED',1;
 IF EXISTS(SELECT 1 FROM ops.Issue01AcceptanceWindows WITH(UPDLOCK,HOLDLOCK) WHERE ClosedAt IS NULL)
  THROW 51000,'ISSUE01_FIXTURE_WINDOW_ALREADY_OPEN',1;
 INSERT ops.Issue01AcceptanceWindows(WindowId,Dataset,Fingerprint,OpenedAt,ExpiresAt)
 VALUES(@WindowId,@Dataset,@Fingerprint,SYSDATETIMEOFFSET(),DATEADD(minute,10,SYSDATETIMEOFFSET()));
 UPDATE identity_access.SecurityState SET Maintenance=0 WHERE Id=1;
 COMMIT;
END;
GO
CREATE OR ALTER PROCEDURE ops.CloseIssue01AcceptanceWindow
 @WindowId uniqueidentifier,@Dataset nvarchar(100),@Fingerprint char(64)
AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;
 IF DB_NAME()<>'Kohami' OR @Dataset<>'kohami_local' OR @Fingerprint<>LOWER(CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CONVERT(nvarchar(100),SERVERPROPERTY('ServerName')),'|Kohami|kohami_local')),2)) THROW 51000,'ISSUE01_FIXTURE_SCOPE_INVALID',1;
 BEGIN TRANSACTION;
 DECLARE @lock int;EXEC @lock=sys.sp_getapplock @Resource='kohami.identity_access.security',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
 IF @lock<0 THROW 51000,'ISSUE01_FIXTURE_LOCK_FAILED',1;
 IF NOT EXISTS(SELECT 1 FROM ops.Issue01AcceptanceWindows WITH(UPDLOCK,HOLDLOCK) WHERE WindowId=@WindowId AND Dataset=@Dataset AND Fingerprint=@Fingerprint)
  THROW 51000,'ISSUE01_FIXTURE_WINDOW_NOT_FOUND',1;
 UPDATE identity_access.SecurityState SET Maintenance=1 WHERE Id=1;
 UPDATE inventory SET Price=fixture.OriginalPrice
 FROM commerce.Inventory inventory JOIN ops.Issue01FixturePrices fixture ON fixture.Sku=inventory.Sku AND inventory.Dataset='kohami_local'
 WHERE fixture.WindowId=@WindowId;
 DELETE ops.Issue01FixturePrices WHERE WindowId=@WindowId;
 UPDATE ops.Issue01AcceptanceWindows SET ClosedAt=COALESCE(ClosedAt,SYSDATETIMEOFFSET()) WHERE WindowId=@WindowId;
 COMMIT;
END;
GO
CREATE OR ALTER PROCEDURE ops.SetIssue01FixturePrice
 @WindowId uniqueidentifier,@Dataset nvarchar(100),@Fingerprint char(64),@Sku nvarchar(100),@Price int
AS
BEGIN
 SET NOCOUNT ON;SET XACT_ABORT ON;
 IF DB_NAME()<>'Kohami' OR @Dataset<>'kohami_local' OR @Fingerprint<>LOWER(CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CONVERT(nvarchar(100),SERVERPROPERTY('ServerName')),'|Kohami|kohami_local')),2)) THROW 51000,'ISSUE01_FIXTURE_SCOPE_INVALID',1;
 IF @Sku NOT IN('sku_demo_01','sku_demo_02','sku_demo_03') OR @Price<1 OR @Price>1000000 THROW 51000,'ISSUE01_FIXTURE_PRICE_INVALID',1;
 BEGIN TRANSACTION;
 DECLARE @lock int;EXEC @lock=sys.sp_getapplock @Resource='kohami.identity_access.security',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
 IF @lock<0 THROW 51000,'ISSUE01_FIXTURE_LOCK_FAILED',1;
 IF NOT EXISTS(SELECT 1 FROM ops.Issue01AcceptanceWindows WITH(UPDLOCK,HOLDLOCK) WHERE WindowId=@WindowId AND Dataset=@Dataset AND Fingerprint=@Fingerprint AND ClosedAt IS NULL AND ExpiresAt>SYSDATETIMEOFFSET())
  THROW 51000,'ISSUE01_FIXTURE_WINDOW_EXPIRED',1;
 IF NOT EXISTS(SELECT 1 FROM identity_access.SecurityState WHERE Id=1 AND Maintenance=0) THROW 51000,'ISSUE01_FIXTURE_WINDOW_CLOSED',1;
 IF NOT EXISTS(SELECT 1 FROM ops.Issue01FixturePrices WHERE WindowId=@WindowId AND Sku=@Sku)
 BEGIN
  INSERT ops.Issue01FixturePrices(WindowId,Sku,OriginalPrice)
  SELECT @WindowId,Sku,Price FROM commerce.Inventory WHERE Dataset=@Dataset AND Sku=@Sku;
 END;
 UPDATE commerce.Inventory SET Price=@Price WHERE Dataset=@Dataset AND Sku=@Sku;
 IF @@ROWCOUNT<>1 THROW 51000,'ISSUE01_FIXTURE_SKU_NOT_FOUND',1;
 COMMIT;
END;
GO
