CREATE OR ALTER PROCEDURE m0.security_catalog_read_allowed
 @SessionId nvarchar(100),@UserId nvarchar(100),@SecurityVersion bigint,
 @DatasetId nvarchar(40),@Now datetimeoffset
AS BEGIN
 SET NOCOUNT ON;
 IF @@TRANCOUNT=0 OR APPLOCK_MODE('public','kohami.m0.security','Transaction')<>'Exclusive'
  THROW 51000,'SECURITY_TRANSACTION_REQUIRED',1;
 SELECT CONVERT(bit,CASE WHEN @DatasetId='demo_m0' AND EXISTS(
  SELECT 1 FROM m0.Sessions s JOIN m0.Users u ON u.UserId=s.UserId
   JOIN m0.Grants g ON g.UserId=u.UserId CROSS JOIN m0.SecurityState ss
  WHERE s.SessionId=@SessionId AND s.UserId=@UserId AND s.SecurityVersion=@SecurityVersion
   AND s.Stage='AdminAuthenticated' AND s.RevokedAt IS NULL
   AND s.ExpiresAt>@Now AND s.AbsoluteUntil>@Now
   AND u.SecurityVersion=s.SecurityVersion AND u.Status='Active' AND u.SecurityHold=0
   AND u.EmailConfirmed=1 AND ss.Maintenance=0
   AND g.Role='Merchandiser' AND g.Permission='catalog.read'
   AND g.ScopeKind='Dataset' AND g.ScopeId=@DatasetId
   AND g.RevokedAt IS NULL AND g.ValidFrom<=@Now AND g.ValidUntil>@Now)
  THEN 1 ELSE 0 END);
END;
GO
GRANT EXECUTE ON OBJECT::m0.security_catalog_read_allowed TO m0_api;
GO
