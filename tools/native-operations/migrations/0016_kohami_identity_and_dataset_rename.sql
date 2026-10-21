/*
   New immutable migration source. Earlier source files stay byte-for-byte unchanged.
   Applied only to the parallel Kohami copy after the original KohamiM0 database
   has been backed up and restored under its new database name.
*/
IF DB_NAME() <> 'Kohami' THROW 51160,'DATABASE_SCOPE_INVALID',1;
IF SCHEMA_ID('m0') IS NULL OR SCHEMA_ID('m0commerce') IS NULL
    THROW 51161,'LEGACY_SCHEMA_REQUIRED',1;
IF EXISTS
(
    SELECT 1
    FROM sys.objects AS target_object
    JOIN sys.schemas AS target_schema ON target_schema.schema_id=target_object.schema_id
    LEFT JOIN sys.schemas AS source_schema ON source_schema.name=CASE target_schema.name WHEN 'identity_access' THEN 'm0' ELSE 'm0commerce' END
    LEFT JOIN sys.objects AS source_object ON source_object.schema_id=source_schema.schema_id AND source_object.name=target_object.name
    WHERE target_schema.name IN ('identity_access','commerce') AND target_object.is_ms_shipped=0
      AND (source_object.object_id IS NULL OR target_object.type NOT IN ('P','PC','V','FN','IF','TF','FS','FT') OR target_object.type<>source_object.type)
)
    THROW 51162,'RENAMED_SCHEMA_ALREADY_EXISTS',1;

IF SCHEMA_ID('identity_access') IS NULL EXEC sys.sp_executesql N'CREATE SCHEMA identity_access AUTHORIZATION dbo';
IF SCHEMA_ID('commerce') IS NULL EXEC sys.sp_executesql N'CREATE SCHEMA commerce AUTHORIZATION dbo';
GO

DECLARE @schema sysname, @object sysname, @statement nvarchar(max), @moduleType char(2);
IF EXISTS
(
    SELECT 1
    FROM sys.objects AS source_object
    JOIN sys.schemas AS source_schema ON source_schema.schema_id=source_object.schema_id
    JOIN sys.schemas AS target_schema ON target_schema.name=CASE source_schema.name WHEN 'm0' THEN 'identity_access' ELSE 'commerce' END
    JOIN sys.objects AS target_object ON target_object.schema_id=target_schema.schema_id AND target_object.name=source_object.name
    WHERE source_schema.name IN ('m0','m0commerce') AND target_object.type NOT IN ('P','PC','V','FN','IF','TF','FS','FT')
)
    THROW 51163,'RENAMED_DATA_OBJECT_COLLISION',1;

DECLARE module_collisions CURSOR LOCAL FAST_FORWARD FOR
    SELECT target_schema.name,target_object.name,target_object.type
    FROM sys.objects AS source_object
    JOIN sys.schemas AS source_schema ON source_schema.schema_id=source_object.schema_id
    JOIN sys.schemas AS target_schema ON target_schema.name=CASE source_schema.name WHEN 'm0' THEN 'identity_access' ELSE 'commerce' END
    JOIN sys.objects AS target_object ON target_object.schema_id=target_schema.schema_id AND target_object.name=source_object.name
    WHERE source_schema.name IN ('m0','m0commerce') AND target_object.type IN ('P','PC','V','FN','IF','TF','FS','FT');
OPEN module_collisions;
FETCH NEXT FROM module_collisions INTO @schema,@object,@moduleType;
WHILE @@FETCH_STATUS=0
BEGIN
    SET @statement=N'DROP '+CASE WHEN @moduleType IN ('P','PC') THEN N'PROCEDURE ' WHEN @moduleType='V' THEN N'VIEW ' ELSE N'FUNCTION ' END
        +QUOTENAME(@schema)+N'.'+QUOTENAME(@object)+N';';
    EXEC sys.sp_executesql @statement;
    FETCH NEXT FROM module_collisions INTO @schema,@object,@moduleType;
END;
CLOSE module_collisions;
DEALLOCATE module_collisions;

DECLARE schema_objects CURSOR LOCAL FAST_FORWARD FOR
    SELECT source_schema.name, object.name
    FROM sys.objects AS object
    JOIN sys.schemas AS source_schema ON source_schema.schema_id = object.schema_id
    WHERE source_schema.name IN ('m0','m0commerce')
      AND object.is_ms_shipped = 0
      AND object.type IN ('U','P','V','FN','IF','TF','FS','FT','SN','SO')
    ORDER BY source_schema.name, object.name;
OPEN schema_objects;
FETCH NEXT FROM schema_objects INTO @schema,@object;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @statement = N'ALTER SCHEMA ' + QUOTENAME(CASE @schema WHEN 'm0' THEN 'identity_access' ELSE 'commerce' END)
        + N' TRANSFER ' + QUOTENAME(@schema) + N'.' + QUOTENAME(@object) + N';';
    EXEC sys.sp_executesql @statement;
    FETCH NEXT FROM schema_objects INTO @schema,@object;
END;
CLOSE schema_objects;
DEALLOCATE schema_objects;

/* Recreate dataset-bound checks around the in-place synthetic data update. */
CREATE TABLE #DatasetChecks
(
    SchemaName sysname NOT NULL,
    TableName sysname NOT NULL,
    ConstraintName sysname NOT NULL,
    Definition nvarchar(max) NOT NULL
);
INSERT #DatasetChecks(SchemaName,TableName,ConstraintName,Definition)
SELECT s.name,t.name,c.name,c.definition
FROM sys.check_constraints AS c
JOIN sys.tables AS t ON t.object_id=c.parent_object_id
JOIN sys.schemas AS s ON s.schema_id=t.schema_id
WHERE c.definition LIKE N'%demo_m0%';

DECLARE @constraint sysname, @definition nvarchar(max);
DECLARE dataset_checks CURSOR LOCAL FAST_FORWARD FOR
    SELECT SchemaName,TableName,ConstraintName,Definition FROM #DatasetChecks;
OPEN dataset_checks;
FETCH NEXT FROM dataset_checks INTO @schema,@object,@constraint,@definition;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @statement = N'ALTER TABLE ' + QUOTENAME(@schema) + N'.' + QUOTENAME(@object)
        + N' DROP CONSTRAINT ' + QUOTENAME(@constraint) + N';';
    EXEC sys.sp_executesql @statement;
    FETCH NEXT FROM dataset_checks INTO @schema,@object,@constraint,@definition;
END;
CLOSE dataset_checks;
DEALLOCATE dataset_checks;

DECLARE disable_constraints CURSOR LOCAL FAST_FORWARD FOR
    SELECT s.name,t.name FROM sys.tables AS t JOIN sys.schemas AS s ON s.schema_id=t.schema_id
    WHERE t.is_ms_shipped=0;
OPEN disable_constraints;
FETCH NEXT FROM disable_constraints INTO @schema,@object;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @statement = N'ALTER TABLE ' + QUOTENAME(@schema) + N'.' + QUOTENAME(@object)
        + N' NOCHECK CONSTRAINT ALL;';
    EXEC sys.sp_executesql @statement;
    FETCH NEXT FROM disable_constraints INTO @schema,@object;
END;
CLOSE disable_constraints;
DEALLOCATE disable_constraints;

/* Update all persisted text, including dataset keys embedded in JSON documents. */
DECLARE @column sysname;
DECLARE dataset_values CURSOR LOCAL FAST_FORWARD FOR
    SELECT s.name,t.name,c.name
    FROM sys.columns AS c
    JOIN sys.tables AS t ON t.object_id=c.object_id
    JOIN sys.schemas AS s ON s.schema_id=t.schema_id
    JOIN sys.types AS ty ON ty.user_type_id=c.user_type_id
    WHERE t.is_ms_shipped=0 AND ty.name IN ('char','nchar','varchar','nvarchar','text','ntext');
OPEN dataset_values;
FETCH NEXT FROM dataset_values INTO @schema,@object,@column;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @statement = N'UPDATE ' + QUOTENAME(@schema) + N'.' + QUOTENAME(@object)
        + N' SET ' + QUOTENAME(@column) + N'=REPLACE(CONVERT(nvarchar(max),' + QUOTENAME(@column)
        + N'),N''demo_m0'',N''kohami_local'') WHERE CONVERT(nvarchar(max),' + QUOTENAME(@column)
        + N') COLLATE Latin1_General_100_BIN2 LIKE N''%demo_m0%'';';
    EXEC sys.sp_executesql @statement;
    FETCH NEXT FROM dataset_values INTO @schema,@object,@column;
END;
CLOSE dataset_values;
DEALLOCATE dataset_values;

/* Rebuild renamed dataset checks using their original names and predicates. */
DECLARE restore_checks CURSOR LOCAL FAST_FORWARD FOR
    SELECT SchemaName,TableName,ConstraintName,REPLACE(Definition,N'demo_m0',N'kohami_local')
    FROM #DatasetChecks;
OPEN restore_checks;
FETCH NEXT FROM restore_checks INTO @schema,@object,@constraint,@definition;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @statement = N'ALTER TABLE ' + QUOTENAME(@schema) + N'.' + QUOTENAME(@object)
        + N' WITH CHECK ADD CONSTRAINT ' + QUOTENAME(@constraint) + N' CHECK ' + @definition + N';';
    EXEC sys.sp_executesql @statement;
    FETCH NEXT FROM restore_checks INTO @schema,@object,@constraint,@definition;
END;
CLOSE restore_checks;
DEALLOCATE restore_checks;

DECLARE validate_constraints CURSOR LOCAL FAST_FORWARD FOR
    SELECT s.name,t.name FROM sys.tables AS t JOIN sys.schemas AS s ON s.schema_id=t.schema_id
    WHERE t.is_ms_shipped=0;
OPEN validate_constraints;
FETCH NEXT FROM validate_constraints INTO @schema,@object;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @statement = N'ALTER TABLE ' + QUOTENAME(@schema) + N'.' + QUOTENAME(@object)
        + N' WITH CHECK CHECK CONSTRAINT ALL;';
    EXEC sys.sp_executesql @statement;
    FETCH NEXT FROM validate_constraints INTO @schema,@object;
END;
CLOSE validate_constraints;
DEALLOCATE validate_constraints;

/* Rewrite stored procedure, view, and function bodies only after schema transfer. */
DECLARE @moduleId int, @moduleDefinition nvarchar(max), @ansiNulls bit, @quotedIdentifier bit;
DECLARE modules CURSOR LOCAL FAST_FORWARD FOR
    SELECT m.object_id,m.definition,m.uses_ansi_nulls,m.uses_quoted_identifier
    FROM sys.sql_modules AS m
    WHERE m.definition LIKE N'%m0.%' OR m.definition LIKE N'%m0commerce.%'
       OR m.definition LIKE N'%demo_m0%'
       OR m.definition LIKE N'%KohamiM0%';
OPEN modules;
FETCH NEXT FROM modules INTO @moduleId,@moduleDefinition,@ansiNulls,@quotedIdentifier;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @moduleDefinition = @moduleDefinition COLLATE Latin1_General_100_CI_AS;
    SET @moduleDefinition = REPLACE(@moduleDefinition,N'm0commerce.',N'commerce.');
    SET @moduleDefinition = REPLACE(@moduleDefinition,N'm0.',N'identity_access.');
    SET @moduleDefinition = REPLACE(@moduleDefinition,N'[m0commerce].',N'[commerce].');
    SET @moduleDefinition = REPLACE(@moduleDefinition,N'[m0].',N'[identity_access].');
    SET @moduleDefinition = REPLACE(@moduleDefinition,N'demo_m0',N'kohami_local');
    SET @moduleDefinition = REPLACE(@moduleDefinition,N'KohamiM0',N'Kohami');
    WHILE UNICODE(LEFT(@moduleDefinition,1)) IN (9,10,13,32)
        SET @moduleDefinition=SUBSTRING(@moduleDefinition,2,LEN(@moduleDefinition));
    IF UPPER(LEFT(@moduleDefinition,15))=N'CREATE OR ALTER'
        SET @moduleDefinition=STUFF(@moduleDefinition,1,15,N'ALTER');
    ELSE IF UPPER(LEFT(@moduleDefinition,6))=N'CREATE'
        SET @moduleDefinition=STUFF(@moduleDefinition,1,6,N'ALTER');
    SET @statement = N'SET ANSI_NULLS ' + CASE WHEN @ansiNulls=1 THEN N'ON' ELSE N'OFF' END
        + N'; SET QUOTED_IDENTIFIER ' + CASE WHEN @quotedIdentifier=1 THEN N'ON' ELSE N'OFF' END + N';';
    EXEC sys.sp_executesql @statement;
    EXEC sys.sp_executesql @moduleDefinition;
    FETCH NEXT FROM modules INTO @moduleId,@moduleDefinition,@ansiNulls,@quotedIdentifier;
END;
CLOSE modules;
DEALLOCATE modules;

/* Bind renamed runtime logins to the preserved least-privilege roles. */
DECLARE @suffix sysname, @oldRole sysname, @newRole sysname, @oldUser sysname, @newUser sysname;
DECLARE principals CURSOR LOCAL FAST_FORWARD FOR
    SELECT v.suffix,v.old_role,v.new_role,v.old_user,v.new_user
    FROM (VALUES
        (N'api',N'm0_api',N'role_kohami_api',N'kohami_m0_api',N'kohami_api'),
        (N'worker',N'm0_worker',N'role_kohami_worker',N'kohami_m0_worker',N'kohami_worker'),
        (N'native',N'm0_native',N'role_kohami_native',N'kohami_m0_native',N'kohami_native'),
        (N'migrator',N'm0_migrator',N'role_kohami_migrator',N'kohami_m0_migrator',N'kohami_migrator'),
        (N'backup',N'm0_backup',N'role_kohami_backup',N'kohami_m0_backup',N'kohami_backup')
    ) AS v(suffix,old_role,new_role,old_user,new_user);
OPEN principals;
FETCH NEXT FROM principals INTO @suffix,@oldRole,@newRole,@oldUser,@newUser;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF DATABASE_PRINCIPAL_ID(@newUser) IS NULL
        THROW 51163,'RENAMED_SQL_USER_REQUIRED',1;
    IF DATABASE_PRINCIPAL_ID(@oldRole) IS NOT NULL
    BEGIN
        SET @statement=N'ALTER ROLE '+QUOTENAME(@oldRole)+N' ADD MEMBER '+QUOTENAME(@newUser)+N';';
        EXEC sys.sp_executesql @statement;
        SET @statement=N'ALTER ROLE '+QUOTENAME(@oldRole)+N' WITH NAME = '+QUOTENAME(@newRole)+N';';
        EXEC sys.sp_executesql @statement;
    END;
    IF DATABASE_PRINCIPAL_ID(@oldUser) IS NOT NULL
    BEGIN
        SET @statement=N'DROP USER '+QUOTENAME(@oldUser)+N';';
        EXEC sys.sp_executesql @statement;
    END;
    SET @statement=N'ALTER USER '+QUOTENAME(@newUser)+N' WITH DEFAULT_SCHEMA = [identity_access];';
    EXEC sys.sp_executesql @statement;
    FETCH NEXT FROM principals INTO @suffix,@oldRole,@newRole,@oldUser,@newUser;
END;
CLOSE principals;
DEALLOCATE principals;

IF DATABASE_PRINCIPAL_ID(N'kohami_backup') IS NOT NULL
    ALTER ROLE [db_backupoperator] ADD MEMBER [kohami_backup];
GRANT ALTER ON SCHEMA::identity_access TO role_kohami_migrator;
GRANT ALTER ON SCHEMA::commerce TO role_kohami_migrator;
GRANT SELECT,INSERT,UPDATE,DELETE ON SCHEMA::identity_access TO role_kohami_migrator;
GRANT SELECT,INSERT,UPDATE,DELETE ON SCHEMA::commerce TO role_kohami_migrator;

IF OBJECT_ID('ops.DevelopmentFixtureMarker','U') IS NOT NULL
BEGIN
    UPDATE ops.DevelopmentFixtureMarker
    SET InstanceFingerprint=LOWER(CONVERT(varchar(64),HASHBYTES('SHA2_256',
        CONCAT(CONVERT(nvarchar(100),SERVERPROPERTY('ServerName')),'|Kohami|kohami_local')),2));
END;

DROP SCHEMA m0;
DROP SCHEMA m0commerce;
