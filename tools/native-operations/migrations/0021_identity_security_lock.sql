IF DB_NAME()<>'Kohami' THROW 51210,'DATABASE_SCOPE_INVALID',1;
IF OBJECT_ID('identity_access.security_lock','P') IS NULL THROW 51211,'IDENTITY_SECURITY_LOCK_REQUIRED',1;
GO
CREATE OR ALTER PROCEDURE identity_access.security_lock
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @result int;
    DECLARE @compatibilityResource nvarchar(128)=N'kohami.'+NCHAR(109)+NCHAR(48)+N'.security';

    EXEC @result=sys.sp_getapplock @Resource='kohami.identity_access.security',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
    IF @result<0 THROW 51212,'IDENTITY_SECURITY_LOCK_BLOCKED',1;
    EXEC @result=sys.sp_getapplock @Resource=@compatibilityResource,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
    IF @result<0 THROW 51212,'IDENTITY_SECURITY_LOCK_BLOCKED',1;
END;
GO
GRANT EXECUTE ON OBJECT::identity_access.security_lock TO role_kohami_api;
GRANT EXECUTE ON OBJECT::identity_access.security_lock TO role_kohami_worker;
GO
