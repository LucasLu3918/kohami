-- Migrator only. Private local mock notices and inactivity suspension preserve account data.
IF OBJECT_ID('m0.AccountInactivityNotices','U') IS NULL
    CREATE TABLE m0.AccountInactivityNotices(
        NoticeId nvarchar(100) NOT NULL PRIMARY KEY,
        UserId nvarchar(100) NOT NULL REFERENCES m0.Users(UserId),
        ActivityAt datetimeoffset NOT NULL,
        State varchar(20) NOT NULL CHECK(State IN('Pending','Sending','Sent','Cancelled','Completed')),
        CreatedAt datetimeoffset NOT NULL,
        LeaseOwner nvarchar(100) NULL,
        LeaseUntil datetimeoffset NULL,
        SentAt datetimeoffset NULL,
        CONSTRAINT UQ_AccountInactivityNotices_User_Activity UNIQUE(UserId,ActivityAt)
    );
GO
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_AccountInactivityNotices_State_Lease' AND object_id=OBJECT_ID('m0.AccountInactivityNotices'))
    CREATE INDEX IX_AccountInactivityNotices_State_Lease ON m0.AccountInactivityNotices(State,LeaseUntil,CreatedAt);
GO
CREATE OR ALTER PROCEDURE m0.account_inactivity_claim_notice @Now datetimeoffset,@LeaseOwner nvarchar(100) AS BEGIN SET NOCOUNT ON;
IF @@TRANCOUNT=0 OR APPLOCK_MODE('public','kohami.m0.security','Transaction')<>'Exclusive' THROW 51000,'SECURITY_TRANSACTION_REQUIRED',1;
UPDATE n SET State='Cancelled',LeaseOwner=NULL,LeaseUntil=NULL
FROM m0.AccountInactivityNotices n JOIN m0.Users u ON u.UserId=n.UserId
WHERE n.State IN('Pending','Sending','Sent') AND (u.Status<>'Active' OR u.SecurityHold=1 OR u.EmailConfirmed=0 OR u.LastActivityAt IS NULL OR u.LastActivityAt<>n.ActivityAt OR u.LastActivityAt>DATEADD(month,-24,@Now));
INSERT m0.AccountInactivityNotices(NoticeId,UserId,ActivityAt,State,CreatedAt)
SELECT 'inactive_'+LOWER(CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(u.UserId,'|',CONVERT(varchar(33),u.LastActivityAt,127))),2)),u.UserId,u.LastActivityAt,'Pending',@Now
FROM m0.Users u
WHERE u.Status='Active' AND u.SecurityHold=0 AND u.EmailConfirmed=1 AND u.LastActivityAt IS NOT NULL
AND u.LastActivityAt<=DATEADD(month,-24,@Now)
AND NOT EXISTS(SELECT 1 FROM m0.AccountInactivityNotices n WHERE n.UserId=u.UserId AND n.ActivityAt=u.LastActivityAt);
DECLARE @claimed TABLE(NoticeId nvarchar(100),UserId nvarchar(100),ActivityAt datetimeoffset);
;WITH next_notice AS(
    SELECT TOP(1) n.NoticeId,n.UserId,n.State,n.LeaseOwner,n.LeaseUntil,u.LastActivityAt,n.ActivityAt
    FROM m0.AccountInactivityNotices n WITH(UPDLOCK,ROWLOCK)
    JOIN m0.Users u ON u.UserId=n.UserId
    WHERE n.State IN('Pending','Sending') AND (n.State='Pending' OR n.LeaseUntil<=@Now)
      AND u.Status='Active' AND u.SecurityHold=0 AND u.EmailConfirmed=1
      AND u.LastActivityAt=n.ActivityAt AND u.LastActivityAt<=DATEADD(month,-24,@Now)
    ORDER BY n.CreatedAt,n.NoticeId
)
UPDATE n SET State='Sending',LeaseOwner=@LeaseOwner,LeaseUntil=DATEADD(minute,5,@Now)
OUTPUT inserted.NoticeId,inserted.UserId,inserted.ActivityAt INTO @claimed(NoticeId,UserId,ActivityAt)
FROM m0.AccountInactivityNotices n JOIN next_notice ON next_notice.NoticeId=n.NoticeId;
SELECT c.NoticeId,u.Email,c.ActivityAt FROM @claimed c JOIN m0.Users u ON u.UserId=c.UserId;
END;
GO
CREATE OR ALTER PROCEDURE m0.account_inactivity_mark_notice_sent @NoticeId nvarchar(100),@LeaseOwner nvarchar(100),@Now datetimeoffset AS BEGIN SET NOCOUNT ON;
IF @@TRANCOUNT=0 OR APPLOCK_MODE('public','kohami.m0.security','Transaction')<>'Exclusive' THROW 51000,'SECURITY_TRANSACTION_REQUIRED',1;
UPDATE n SET State='Sent',SentAt=@Now,LeaseOwner=NULL,LeaseUntil=NULL
FROM m0.AccountInactivityNotices n JOIN m0.Users u ON u.UserId=n.UserId
WHERE n.NoticeId=@NoticeId AND n.State='Sending' AND n.LeaseOwner=@LeaseOwner
  AND u.Status='Active' AND u.SecurityHold=0 AND u.EmailConfirmed=1
  AND u.LastActivityAt=n.ActivityAt AND u.LastActivityAt<=DATEADD(month,-24,@Now);
IF @@ROWCOUNT=1 BEGIN SELECT CAST(1 AS bit);RETURN;END;
UPDATE m0.AccountInactivityNotices SET State='Cancelled',LeaseOwner=NULL,LeaseUntil=NULL
WHERE NoticeId=@NoticeId AND State='Sending' AND LeaseOwner=@LeaseOwner;
SELECT CAST(0 AS bit);
END;
GO
CREATE OR ALTER PROCEDURE m0.account_inactivity_suspend_due @Now datetimeoffset AS BEGIN SET NOCOUNT ON;
IF @@TRANCOUNT=0 OR APPLOCK_MODE('public','kohami.m0.security','Transaction')<>'Exclusive' THROW 51000,'SECURITY_TRANSACTION_REQUIRED',1;
DECLARE @suspended TABLE(UserId nvarchar(100),SecurityVersion bigint);
;WITH due AS(
    SELECT u.UserId,u.SecurityVersion,n.NoticeId,n.State
    FROM m0.Users u WITH(UPDLOCK,ROWLOCK)
    JOIN m0.AccountInactivityNotices n ON n.UserId=u.UserId
    WHERE n.State='Sent' AND n.SentAt<=DATEADD(day,-30,@Now)
      AND u.Status='Active' AND u.SecurityHold=0 AND u.EmailConfirmed=1
      AND u.LastActivityAt=n.ActivityAt AND u.LastActivityAt<=DATEADD(month,-24,@Now)
      AND (NOT EXISTS(SELECT 1 FROM m0.Grants own WHERE own.UserId=u.UserId AND own.Role='Owner' AND own.Permission='access.manage' AND own.ScopeKind='Dataset' AND own.ScopeId='demo_m0' AND own.RevokedAt IS NULL AND own.ValidFrom<=@Now AND own.ValidUntil>@Now)
           OR EXISTS(SELECT 1 FROM m0.Grants otherGrant JOIN m0.Users otherUser ON otherUser.UserId=otherGrant.UserId WHERE otherGrant.UserId<>u.UserId AND otherGrant.Role='Owner' AND otherGrant.Permission='access.manage' AND otherGrant.ScopeKind='Dataset' AND otherGrant.ScopeId='demo_m0' AND otherGrant.RevokedAt IS NULL AND otherGrant.ValidFrom<=@Now AND otherGrant.ValidUntil>@Now AND otherUser.Status='Active' AND otherUser.SecurityHold=0 AND otherUser.EmailConfirmed=1))
)
UPDATE u SET Status='Suspended',SecurityVersion=u.SecurityVersion+1
OUTPUT inserted.UserId,inserted.SecurityVersion INTO @suspended(UserId,SecurityVersion)
FROM m0.Users u JOIN due ON due.UserId=u.UserId;
UPDATE s SET RevokedAt=@Now FROM m0.Sessions s JOIN @suspended x ON x.UserId=s.UserId WHERE s.RevokedAt IS NULL;
DELETE p FROM m0.RecentProofs p JOIN @suspended x ON x.UserId=p.UserId;
UPDATE c SET ConsumedAt=@Now FROM m0.Challenges c JOIN @suspended x ON x.UserId=c.UserId WHERE c.ConsumedAt IS NULL;
UPDATE n SET State='Completed' FROM m0.AccountInactivityNotices n JOIN @suspended x ON x.UserId=n.UserId WHERE n.State='Sent';
INSERT m0.SecurityAudit(UserId,Action,At,SecurityVersion)
SELECT UserId,'AccountInactiveSuspended',@Now,SecurityVersion FROM @suspended;
SELECT COUNT(*) FROM @suspended;
END;
GO
CREATE OR ALTER PROCEDURE m0.account_inactivity_purge_expired @Now datetimeoffset AS BEGIN SET NOCOUNT ON;
IF @@TRANCOUNT=0 OR APPLOCK_MODE('public','kohami.m0.security','Transaction')<>'Exclusive' THROW 51000,'SECURITY_TRANSACTION_REQUIRED',1;
UPDATE n SET State='Cancelled',LeaseOwner=NULL,LeaseUntil=NULL
FROM m0.AccountInactivityNotices n JOIN m0.Users u ON u.UserId=n.UserId
WHERE n.State='Sent' AND (u.Status<>'Active' OR u.LastActivityAt IS NULL OR u.LastActivityAt<>n.ActivityAt);
UPDATE m0.AccountInactivityNotices SET State='Cancelled',LeaseOwner=NULL,LeaseUntil=NULL
WHERE State='Sent' AND SentAt<=DATEADD(year,-1,@Now);
DELETE m0.AccountInactivityNotices
WHERE State IN('Cancelled','Completed') AND COALESCE(SentAt,CreatedAt)<=DATEADD(year,-1,@Now);
SELECT @@ROWCOUNT;
END;
GO
GRANT EXECUTE ON OBJECT::m0.account_inactivity_claim_notice TO m0_worker;
GRANT EXECUTE ON OBJECT::m0.account_inactivity_mark_notice_sent TO m0_worker;
GRANT EXECUTE ON OBJECT::m0.account_inactivity_suspend_due TO m0_worker;
GRANT EXECUTE ON OBJECT::m0.account_inactivity_purge_expired TO m0_worker;
GO
