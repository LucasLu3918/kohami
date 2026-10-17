-- Migrator only. Preserve the latest authenticated-activity timestamp, not request history.
IF COL_LENGTH('m0.Users','LastActivityAt') IS NULL
    ALTER TABLE m0.Users ADD LastActivityAt datetimeoffset NULL;
GO
UPDATE u
SET LastActivityAt = history.LastAuthenticatedAt
FROM m0.Users u
CROSS APPLY (
    SELECT MAX(s.AuthenticatedAt) AS LastAuthenticatedAt
    FROM m0.Sessions s
    WHERE s.UserId = u.UserId
) history
WHERE u.LastActivityAt IS NULL AND history.LastAuthenticatedAt IS NOT NULL;
GO
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_Users_LastActivityAt' AND object_id=OBJECT_ID('m0.Users'))
    CREATE INDEX IX_Users_LastActivityAt ON m0.Users(LastActivityAt) WHERE LastActivityAt IS NOT NULL;
GO
CREATE OR ALTER PROCEDURE m0.security_touch_session @SessionId nvarchar(100),@Now datetimeoffset AS BEGIN SET NOCOUNT ON;
IF @@TRANCOUNT=0 OR APPLOCK_MODE('public','kohami.m0.security','Transaction')<>'Exclusive' THROW 51000,'SECURITY_TRANSACTION_REQUIRED',1;
DECLARE @Touched TABLE(UserId nvarchar(100) NOT NULL);
UPDATE s SET ExpiresAt=CASE WHEN DATEADD(minute,15,@Now)<AbsoluteUntil THEN DATEADD(minute,15,@Now) ELSE AbsoluteUntil END
OUTPUT inserted.UserId INTO @Touched(UserId)
FROM m0.Sessions s
WHERE SessionId=@SessionId AND Stage IN('AdminAuthenticated','MemberAuthenticated')
AND RevokedAt IS NULL AND ExpiresAt>@Now AND AbsoluteUntil>@Now;
UPDATE u SET LastActivityAt=@Now
FROM m0.Users u JOIN @Touched t ON t.UserId=u.UserId
WHERE (u.LastActivityAt IS NULL OR u.LastActivityAt<=DATEADD(minute,-5,@Now))
AND (u.LastActivityAt IS NULL OR u.LastActivityAt<@Now);
END;
GO
