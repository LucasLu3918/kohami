using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Globalization;
using Microsoft.Data.SqlClient;

namespace Kohami.M0.Native;

public static class SqlBaselineAdoption
{
    // Refreshed only by reviewing and accepting a new migration baseline fingerprint.
    public const string ExpectedFingerprint = "0495b8123f131fff266dc8af79d12034e5d99f9f6b19f0081eb39597c4b0b76a";

    private static readonly (string Name, string Sql)[] CatalogSnapshots =
    [
        ("principals", """
            SELECT name, type_desc, default_schema_name, authentication_type_desc, is_fixed_role
            FROM sys.database_principals
            WHERE principal_id > 4 AND name NOT IN ('m0_fixture_bootstrap','kohami_m0_fixture_bootstrap')
            ORDER BY name
            """),
        ("role-members", """
            SELECT role.name AS role_name, member.name AS member_name
            FROM sys.database_role_members rm
            JOIN sys.database_principals role ON role.principal_id=rm.role_principal_id
            JOIN sys.database_principals member ON member.principal_id=rm.member_principal_id
            WHERE role.name<>'m0_fixture_bootstrap' AND member.name<>'kohami_m0_fixture_bootstrap'
            ORDER BY role.name,member.name
            """),
        ("schemas", """
            SELECT s.name, owner.name AS owner_name
            FROM sys.schemas s JOIN sys.database_principals owner ON owner.principal_id=s.principal_id
            WHERE s.name NOT IN ('sys','INFORMATION_SCHEMA')
            ORDER BY s.name
            """),
        ("objects", """
            SELECT s.name AS schema_name,o.name,o.type,o.type_desc,o.is_ms_shipped,
                   CONVERT(nvarchar(128),USER_NAME(o.principal_id)) AS owner_name
            FROM sys.objects o JOIN sys.schemas s ON s.schema_id=o.schema_id
            WHERE o.is_ms_shipped=0 AND NOT (s.name='ops' AND
                  (o.name='SchemaMigrations' OR o.name IN ('DevelopmentFixtureMarker','DevelopmentFixtureBootstrapUse','InspectDevelopmentFixture','DevelopmentFixtureBootstrapOwner')))
            ORDER BY s.name,o.name,o.type
            """),
        ("columns", """
            SELECT s.name AS schema_name,o.name AS object_name,c.column_id,c.name AS column_name,
                   t.name AS type_name,c.max_length,c.precision,c.scale,c.is_nullable,c.is_identity,
                   c.is_computed,c.collation_name,c.is_rowguidcol,c.is_sparse,c.is_column_set,
                   cc.definition AS computed_definition,cc.is_persisted,
                   dc.definition AS default_definition,IDENT_SEED(QUOTENAME(s.name)+N'.'+QUOTENAME(o.name)) AS identity_seed,
                   IDENT_INCR(QUOTENAME(s.name)+N'.'+QUOTENAME(o.name)) AS identity_increment
            FROM sys.columns c JOIN sys.objects o ON o.object_id=c.object_id
            JOIN sys.schemas s ON s.schema_id=o.schema_id JOIN sys.types t ON t.user_type_id=c.user_type_id
            LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id
            LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id
            WHERE o.is_ms_shipped=0 AND NOT (s.name='ops' AND
                  (o.name='SchemaMigrations' OR o.name IN ('DevelopmentFixtureMarker','DevelopmentFixtureBootstrapUse','InspectDevelopmentFixture','DevelopmentFixtureBootstrapOwner')))
            ORDER BY s.name,o.name,c.column_id
            """),
        ("indexes", """
            SELECT s.name AS schema_name,o.name AS object_name,i.name,i.type_desc,i.is_unique,
                   i.is_primary_key,i.is_unique_constraint,i.ignore_dup_key,i.fill_factor,
                   i.has_filter,i.filter_definition,i.is_disabled,i.allow_row_locks,i.allow_page_locks
            FROM sys.indexes i JOIN sys.objects o ON o.object_id=i.object_id
            JOIN sys.schemas s ON s.schema_id=o.schema_id
            WHERE i.index_id>0 AND o.is_ms_shipped=0 AND NOT (s.name='ops' AND
                  (o.name='SchemaMigrations' OR o.name IN ('DevelopmentFixtureMarker','DevelopmentFixtureBootstrapUse','InspectDevelopmentFixture','DevelopmentFixtureBootstrapOwner')))
            ORDER BY s.name,o.name,i.name
            """),
        ("index-columns", """
            SELECT s.name AS schema_name,o.name AS object_name,i.name AS index_name,
                   ic.key_ordinal,ic.index_column_id,ic.is_descending_key,ic.is_included_column,c.name AS column_name
            FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id
            JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
            JOIN sys.objects o ON o.object_id=i.object_id JOIN sys.schemas s ON s.schema_id=o.schema_id
            WHERE i.index_id>0 AND o.is_ms_shipped=0 AND NOT (s.name='ops' AND
                  (o.name='SchemaMigrations' OR o.name IN ('DevelopmentFixtureMarker','DevelopmentFixtureBootstrapUse','InspectDevelopmentFixture','DevelopmentFixtureBootstrapOwner')))
            ORDER BY s.name,o.name,i.name,ic.index_column_id
            """),
        ("constraints", """
            SELECT s.name AS schema_name,o.name AS object_name,cc.name,cc.type_desc,
                   cc.definition,cc.is_disabled,cc.is_not_trusted,CONVERT(nvarchar(256),NULL) AS referenced_table,
                   CONVERT(nvarchar(20),NULL) AS delete_action,CONVERT(nvarchar(20),NULL) AS update_action
            FROM sys.check_constraints cc JOIN sys.objects o ON o.object_id=cc.parent_object_id
            JOIN sys.schemas s ON s.schema_id=o.schema_id
            WHERE NOT (s.name='ops' AND (o.name='SchemaMigrations' OR o.name IN ('DevelopmentFixtureMarker','DevelopmentFixtureBootstrapUse','InspectDevelopmentFixture','DevelopmentFixtureBootstrapOwner')))
            UNION ALL
            SELECT s.name,o.name,fk.name,N'FOREIGN_KEY',N'',fk.is_disabled,fk.is_not_trusted,
                   QUOTENAME(rs.name)+N'.'+QUOTENAME(ro.name),fk.delete_referential_action_desc,fk.update_referential_action_desc
            FROM sys.foreign_keys fk JOIN sys.objects o ON o.object_id=fk.parent_object_id
            JOIN sys.schemas s ON s.schema_id=o.schema_id
            JOIN sys.objects ro ON ro.object_id=fk.referenced_object_id
            JOIN sys.schemas rs ON rs.schema_id=ro.schema_id
            WHERE NOT (s.name='ops' AND (o.name='SchemaMigrations' OR o.name IN ('DevelopmentFixtureMarker','DevelopmentFixtureBootstrapUse','InspectDevelopmentFixture','DevelopmentFixtureBootstrapOwner')))
            ORDER BY schema_name,object_name,name
            """),
        ("modules", """
            SELECT s.name AS schema_name,o.name,m.uses_ansi_nulls,m.uses_quoted_identifier,
                   m.is_schema_bound,m.execute_as_principal_id,m.definition
            FROM sys.sql_modules m JOIN sys.objects o ON o.object_id=m.object_id
            JOIN sys.schemas s ON s.schema_id=o.schema_id
            WHERE o.is_ms_shipped=0 AND NOT (s.name='ops' AND
                  (o.name='SchemaMigrations' OR o.name IN ('DevelopmentFixtureMarker','DevelopmentFixtureBootstrapUse','InspectDevelopmentFixture','DevelopmentFixtureBootstrapOwner')))
            ORDER BY s.name,o.name
            """),
        ("permissions", """
            SELECT grantee.name AS grantee_name,p.class_desc,p.permission_name,p.state_desc,
                   COALESCE(QUOTENAME(os.name)+N'.'+QUOTENAME(o.name),QUOTENAME(ss.name),DB_NAME()) AS target_name,
                   c.name AS column_name
            FROM sys.database_permissions p
            JOIN sys.database_principals grantee ON grantee.principal_id=p.grantee_principal_id
            LEFT JOIN sys.objects o ON p.class=1 AND o.object_id=p.major_id
            LEFT JOIN sys.schemas os ON os.schema_id=o.schema_id
            LEFT JOIN sys.schemas ss ON p.class=3 AND ss.schema_id=p.major_id
            LEFT JOIN sys.columns c ON p.class=1 AND c.object_id=p.major_id AND c.column_id=p.minor_id AND p.minor_id>0
            WHERE grantee.name NOT IN ('m0_fixture_bootstrap','kohami_m0_fixture_bootstrap')
              AND NOT (p.class=1 AND os.name='ops' AND (o.name='SchemaMigrations' OR o.name IN ('DevelopmentFixtureMarker','DevelopmentFixtureBootstrapUse','InspectDevelopmentFixture','DevelopmentFixtureBootstrapOwner')))
            ORDER BY grantee.name,p.class_desc,p.permission_name,p.state_desc,target_name,column_name
            """)
    ];

    public static async Task<string> ComputeFingerprintAsync(SqlConnection connection,
        IReadOnlyList<SqlMigrationDefinition> plan,
        CancellationToken cancellationToken = default)
    {
        EnsureConnection(connection);
        SqlMigrations.ValidatePlan(plan);
        var canonical = new StringBuilder();
        foreach (var snapshot in CatalogSnapshots)
        {
            await using var command = new SqlCommand(snapshot.Sql, connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var values = new string?[reader.FieldCount];
                for (var index = 0; index < reader.FieldCount; index++)
                    values[index] = reader.IsDBNull(index) ? null : Convert.ToString(reader.GetValue(index), CultureInfo.InvariantCulture);
                canonical.Append(snapshot.Name).Append(':').Append(JsonSerializer.Serialize(values)).Append('\n');
            }
        }
        foreach (var migration in plan)
            canonical.Append("migration:").Append(JsonSerializer.Serialize(new[] { migration.Id, migration.SourceHash })).Append('\n');
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
    }

    public static async Task AdoptAsync(SqlConnection metadataConnection, SqlConnection writerConnection,
        IReadOnlyList<SqlMigrationDefinition> plan, CancellationToken cancellationToken = default)
    {
        EnsureConnection(metadataConnection);
        EnsureConnection(writerConnection);
        if (!StringComparer.Ordinal.Equals(metadataConnection.DataSource, writerConnection.DataSource) ||
            !StringComparer.Ordinal.Equals(metadataConnection.Database, writerConnection.Database))
            throw new InvalidOperationException("SQL_BASELINE_ADOPTION_TARGET_INVALID");
        SqlMigrations.ValidatePlan(plan);
        if (await LoginAsync(metadataConnection, cancellationToken) != "sa" ||
            await LoginAsync(writerConnection, cancellationToken) != "kohami_m0_migrator")
            throw new InvalidOperationException("SQL_BASELINE_ADOPTION_LOGIN_INVALID");
        await using (var lockCommand = new SqlCommand("DECLARE @result int; EXEC @result=sys.sp_getapplock " +
            "@Resource='kohami:m0:schema-migrations',@LockMode='Exclusive',@LockOwner='Session',@LockTimeout=30000; SELECT @result", writerConnection))
        {
            if (Convert.ToInt32(await lockCommand.ExecuteScalarAsync(cancellationToken)) < 0)
                throw new InvalidOperationException("SQL_BASELINE_ADOPTION_LOCK_FAILED");
        }

        try
        {
            if (await ScalarAsync(writerConnection, "SELECT CASE WHEN SCHEMA_ID('ops') IS NOT NULL AND OBJECT_ID('ops.SchemaMigrations','U') IS NULL THEN 1 ELSE 0 END", cancellationToken) != 1)
                throw new InvalidOperationException("SQL_BASELINE_ADOPTION_HISTORY_INVALID");
            if (await ScalarAsync(writerConnection, "SELECT CASE WHEN SCHEMA_ID('m0') IS NOT NULL AND SCHEMA_ID('m0commerce') IS NOT NULL THEN 1 ELSE 0 END", cancellationToken) != 1)
                throw new InvalidOperationException("SQL_BASELINE_ADOPTION_SCHEMA_MISSING");

            var actual = await ComputeFingerprintAsync(metadataConnection, plan, cancellationToken);
            if (!StringComparer.Ordinal.Equals(actual, ExpectedFingerprint))
                throw new InvalidOperationException("SQL_BASELINE_FINGERPRINT_MISMATCH");

            await using var transaction = (SqlTransaction)await writerConnection.BeginTransactionAsync(cancellationToken);
            try
            {
                await using (var create = new SqlCommand("CREATE TABLE ops.SchemaMigrations(" +
                    "MigrationId varchar(100) NOT NULL PRIMARY KEY, SourceHash char(64) NOT NULL, " +
                    "AppliedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_SchemaMigrations_AppliedAtUtc DEFAULT SYSUTCDATETIME());", writerConnection, transaction))
                    await create.ExecuteNonQueryAsync(cancellationToken);
                foreach (var migration in plan)
                {
                    await using var insert = new SqlCommand("INSERT ops.SchemaMigrations(MigrationId,SourceHash) VALUES(@id,@hash)", writerConnection, transaction);
                    insert.Parameters.AddWithValue("@id", migration.Id);
                    insert.Parameters.AddWithValue("@hash", migration.SourceHash);
                    await insert.ExecuteNonQueryAsync(cancellationToken);
                }
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw;
            }
        }
        finally
        {
            await using var release = new SqlCommand("EXEC sys.sp_releaseapplock @Resource='kohami:m0:schema-migrations',@LockOwner='Session'", writerConnection);
            await release.ExecuteNonQueryAsync(CancellationToken.None);
        }
    }

    private static void EnsureConnection(SqlConnection connection)
    {
        if (connection.State != System.Data.ConnectionState.Open || connection.Database != "KohamiM0")
            throw new InvalidOperationException("SQL_BASELINE_ADOPTION_TARGET_INVALID");
    }

    private static async Task<int> ScalarAsync(SqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(sql, connection);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task<string> LoginAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand("SELECT ORIGINAL_LOGIN()", connection);
        return Convert.ToString(await command.ExecuteScalarAsync(cancellationToken)) ?? "";
    }
}
