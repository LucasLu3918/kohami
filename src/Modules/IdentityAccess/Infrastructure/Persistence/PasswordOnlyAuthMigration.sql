-- The existing database may contain password-stage sessions; revoke those instead of promoting them.
IF COL_LENGTH('m0.Sessions','AuthenticatedAt') IS NULL AND COL_LENGTH('m0.Sessions','MfaVerifiedAt') IS NOT NULL
 EXEC sys.sp_rename 'm0.Sessions.MfaVerifiedAt','AuthenticatedAt','COLUMN';
IF COL_LENGTH('m0.Sessions','AuthenticatedAt') IS NULL
 ALTER TABLE m0.Sessions ADD AuthenticatedAt datetimeoffset NULL;
GO
