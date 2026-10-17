IF DB_NAME() <> 'KohamiM0' THROW 50000,'DATABASE_SCOPE_INVALID',1;
GO
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_GuestRequests_ExpiresAt' AND object_id=OBJECT_ID('m0commerce.GuestRequests'))
 CREATE INDEX IX_GuestRequests_ExpiresAt ON m0commerce.GuestRequests(ExpiresAt);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_PrivateDeliveries_RequestId' AND object_id=OBJECT_ID('m0commerce.PrivateDeliveries'))
 CREATE INDEX IX_PrivateDeliveries_RequestId ON m0commerce.PrivateDeliveries(Dataset,RequestId);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_GuestCapabilities_ExpiresAt' AND object_id=OBJECT_ID('m0commerce.GuestCapabilities'))
 CREATE INDEX IX_GuestCapabilities_ExpiresAt ON m0commerce.GuestCapabilities(ExpiresAt);
GO
CREATE OR ALTER PROCEDURE m0commerce.purge_expired_guest_proofs @Now datetimeoffset AS
BEGIN
 SET NOCOUNT ON;
 IF @@TRANCOUNT=0 THROW 51000,'GUEST_PROOF_RETENTION_TRANSACTION_REQUIRED',1;

 DELETE delivery
 FROM m0commerce.PrivateDeliveries AS delivery
 INNER JOIN m0commerce.GuestRequests AS request
  ON request.Dataset=delivery.Dataset AND request.Id=delivery.RequestId
 WHERE request.Dataset='demo_m0' AND request.ExpiresAt<=@Now;

 DELETE FROM m0commerce.GuestRequests
 WHERE Dataset='demo_m0' AND ExpiresAt<=@Now;

 DELETE FROM m0commerce.GuestCapabilities
 WHERE Dataset='demo_m0' AND ExpiresAt<=@Now;
END;
GO
GRANT EXECUTE ON OBJECT::m0commerce.purge_expired_guest_proofs TO m0_worker;
