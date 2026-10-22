using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace Kohami.Native;

public sealed record SqlMigrationDefinition(string Id, IReadOnlyList<(string Source, string Sql)> Batches)
{
    public string SourceHash => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
        Id + "\n" + string.Join("\n", Batches.Select(batch => batch.Source + "\n" + batch.Sql)))));
}

public static class SqlMigrations
{
    private static readonly (string Id, string[] Sources)[] Manifest =
    [
        ("0001_identity_commerce_baseline", [
            "src/Modules/IdentityAccess/Infrastructure/Persistence/SQLSchema.sql",
            "src/Modules/Commerce/Infrastructure/Persistence/CommerceSchema.sql",
            "src/Modules/Commerce/Infrastructure/Persistence/CommerceSeed.sql"]),
        ("0002_native_operations", ["tools/native-operations/Operations.sql"]),
        ("0003_database_grants", [
            "src/Modules/IdentityAccess/Infrastructure/Persistence/SQLGrants.sql",
            "src/Modules/Commerce/Infrastructure/Persistence/CommerceGrants.sql",
            "tools/native-operations/Grants.sql",
            "tools/native-operations/MigratorGrants.sql"]),
        ("0004_schema_readiness_grant", ["tools/native-operations/SchemaReadinessGrant.sql"]),
        ("0005_catalog", ["src/Modules/Catalog/Infrastructure/Persistence/CatalogSchema.sql"]),
        ("0006_catalog_authorization_recheck", ["src/Modules/IdentityAccess/Infrastructure/Persistence/CatalogAuthorization.sql"]),
        ("0007_catalog_demo_showcase", ["src/Modules/Catalog/Infrastructure/Persistence/CatalogDemoShowcaseSeed.sql"]),
        ("0008_password_only_admin_auth", [
            "src/Modules/IdentityAccess/Infrastructure/Persistence/PasswordOnlyAuthMigration.sql",
            "src/Modules/IdentityAccess/Infrastructure/Persistence/PasswordOnlyAuthSchema.sql",
            "tools/native-operations/PasswordOnlyOperations.sql",
            "src/Modules/IdentityAccess/Infrastructure/Persistence/PasswordOnlyAuthCleanup.sql",
            "src/Modules/IdentityAccess/Infrastructure/Persistence/PasswordOnlyCatalogAuthorization.sql",
            "src/Modules/IdentityAccess/Infrastructure/Persistence/PasswordOnlyAuthGrants.sql"]),
        ("0009_issue23_retention_audit", [
            "src/Modules/Commerce/Infrastructure/Persistence/Issue23RetentionMigration.sql"]),
        ("0010_issue23_account_activity", [
            "src/Modules/IdentityAccess/Infrastructure/Persistence/Issue23AccountActivityMigration.sql"]),
        ("0011_issue23_account_inactivity", [
            "src/Modules/IdentityAccess/Infrastructure/Persistence/Issue23AccountInactivityMigration.sql"]),
        ("0012_issue23_guest_proof_retention", [
            "src/Modules/Commerce/Infrastructure/Persistence/Issue23GuestProofRetentionMigration.sql"]),
        ("0013_member_wallet_ledger", [
            "src/Modules/Commerce/Infrastructure/Persistence/MemberWalletMigration.sql"]),
        ("0014_wallet_payment_api_writes", [
            "src/Modules/Commerce/Infrastructure/Persistence/MemberWalletApiWriteMigration.sql"]),
        ("0015_catalog_admin_read", [
            "src/Modules/IdentityAccess/Infrastructure/Persistence/CatalogAdminReadMigration.sql"]),
        ("0016_kohami_identity_and_dataset_rename", [
            "tools/native-operations/migrations/0016_kohami_identity_and_dataset_rename.sql"]),
        ("0017_catalog_public_read_permissions", [
            "tools/native-operations/migrations/0017_catalog_public_read_permissions.sql"]),
        ("0018_api_schema_readiness", [
            "tools/native-operations/migrations/0018_api_schema_readiness.sql"]),
        ("0019_runtime_service_permissions", [
            "tools/native-operations/migrations/0019_runtime_service_permissions.sql"]),
        ("0020_member_registration", [
            "tools/native-operations/migrations/0020_member_registration.sql"]),
        ("0021_identity_security_lock", [
            "tools/native-operations/migrations/0021_identity_security_lock.sql"]),
        ("0022_wallet_mock_credits", [
            "tools/native-operations/migrations/0022_wallet_mock_credits.sql"]),
        ("0023_report_access_audit_api_insert", [
            "tools/native-operations/migrations/0023_report_access_audit_api_insert.sql"]),
        ("0024_security_log_purge_worker_execute", [
            "tools/native-operations/migrations/0024_security_log_purge_worker_execute.sql"]),
        ("0025_retention_worker_execute", [
            "tools/native-operations/migrations/0025_retention_worker_execute.sql"]),
        ("0026_kohami_native_operation_scope", [
            "tools/native-operations/migrations/0026_kohami_native_operation_scope.sql"]),
        ("0027_native_security_lock_bridge", [
            "tools/native-operations/migrations/0027_native_security_lock_bridge.sql"]),
        ("0028_reconcile_security_lock_bridge", [
            "tools/native-operations/migrations/0028_reconcile_security_lock_bridge.sql"]),
        ("0029_wallet_checkout_api_permissions", [
            "tools/native-operations/migrations/0029_wallet_checkout_api_permissions.sql"]),
    ];

    public static IReadOnlyList<SqlMigrationDefinition> Load(string root)
    {
        var plan = Manifest.Select(migration => new SqlMigrationDefinition(migration.Id,
            migration.Sources.SelectMany(source => ReadBatches(root, source)).ToArray())).ToArray();
        ValidatePlan(plan);
        return plan;
    }

    public static void ValidatePlan(IReadOnlyList<SqlMigrationDefinition> plan)
    {
        if (plan.Count == 0) throw new InvalidOperationException("SQL_MIGRATION_PLAN_EMPTY");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        string? previous = null;
        foreach (var migration in plan)
        {
            if (!Regex.IsMatch(migration.Id, "^[0-9]{4}_[a-z0-9_]+$", RegexOptions.CultureInvariant) ||
                !ids.Add(migration.Id) || (previous is not null && StringComparer.Ordinal.Compare(previous, migration.Id) >= 0))
                throw new InvalidOperationException("SQL_MIGRATION_PLAN_ORDER_INVALID");
            if (migration.Batches.Count == 0 || migration.Batches.Any(batch => string.IsNullOrWhiteSpace(batch.Sql)) ||
                !Regex.IsMatch(migration.SourceHash, "^[0-9a-f]{64}$", RegexOptions.CultureInvariant))
                throw new InvalidOperationException("SQL_MIGRATION_SOURCE_INVALID");
            previous = migration.Id;
        }
    }

    public static async Task ApplyAsync(SqlConnection connection, IReadOnlyList<SqlMigrationDefinition> plan,
        CancellationToken cancellationToken = default, string targetDatabase = "Kohami")
    {
        ValidatePlan(plan);
        if (connection.State != System.Data.ConnectionState.Open || connection.Database != targetDatabase)
            throw new InvalidOperationException("SQL_MIGRATION_TARGET_INVALID");

        await using (var scope = new SqlCommand("SELECT CASE WHEN DB_NAME()=@database AND " +
            "CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')) IS NOT NULL THEN 1 ELSE 0 END", connection))
        {
            scope.Parameters.AddWithValue("@database", targetDatabase);
            if (Convert.ToInt32(await scope.ExecuteScalarAsync(cancellationToken)) != 1)
                throw new InvalidOperationException("SQL_MIGRATION_TARGET_INVALID");
        }

        await using (var acquire = new SqlCommand("DECLARE @result int; EXEC @result=sys.sp_getapplock " +
            "@Resource='kohami:foundation:schema-migrations',@LockMode='Exclusive',@LockOwner='Session',@LockTimeout=30000; " +
            "SELECT @result", connection))
        {
            if (Convert.ToInt32(await acquire.ExecuteScalarAsync(cancellationToken)) < 0)
                throw new InvalidOperationException("SQL_MIGRATION_LOCK_FAILED");
        }

        try
        {
            await EnsureHistoryAsync(connection, cancellationToken);
            var applied = await ReadAppliedAsync(connection, cancellationToken);
            var knownIds = plan.Select(migration => migration.Id).ToHashSet(StringComparer.Ordinal);
            if (applied.Keys.Any(id => !knownIds.Contains(id)))
                throw new InvalidOperationException("SQL_MIGRATION_HISTORY_UNKNOWN");
            var appliedCount = applied.Count;
            if (plan.Take(appliedCount).Any(migration => !applied.ContainsKey(migration.Id)) ||
                plan.Skip(appliedCount).Any(migration => applied.ContainsKey(migration.Id)))
                throw new InvalidOperationException("SQL_MIGRATION_HISTORY_GAP");
            if (appliedCount == 0 && await HasLegacySchemaAsync(connection, cancellationToken))
                throw new InvalidOperationException("SQL_MIGRATION_BASELINE_REQUIRED");
            foreach (var migration in plan)
            {
                if (applied.TryGetValue(migration.Id, out var recordedHash))
                {
                    if (!StringComparer.Ordinal.Equals(recordedHash, migration.SourceHash))
                        throw new InvalidOperationException("SQL_MIGRATION_CHECKSUM_MISMATCH:" + migration.Id);
                    continue;
                }

                await ApplyOneAsync(connection, migration, cancellationToken);
                Console.WriteLine("SQL_MIGRATION_APPLIED " + migration.Id);
            }
        }
        finally
        {
            await using var release = new SqlCommand("EXEC sys.sp_releaseapplock " +
                "@Resource='kohami:foundation:schema-migrations',@LockOwner='Session'", connection);
            await release.ExecuteNonQueryAsync(CancellationToken.None);
        }
    }

    public static async Task<bool> ApplyExpectedNextAsync(SqlConnection connection,
        IReadOnlyList<SqlMigrationDefinition> plan, string migrationId, CancellationToken cancellationToken = default,
        string targetDatabase = "Kohami")
    {
        ValidatePlan(plan);
        if (connection.State != System.Data.ConnectionState.Open || connection.Database != targetDatabase)
            throw new InvalidOperationException("SQL_MIGRATION_TARGET_INVALID");
        var targetIndex = -1;
        for (var i = 0; i < plan.Count; i++)
            if (plan[i].Id == migrationId) targetIndex = i;
        if (targetIndex < 0) throw new InvalidOperationException("SQL_MIGRATION_TARGET_UNKNOWN");

        await using (var acquire = new SqlCommand("DECLARE @result int; EXEC @result=sys.sp_getapplock " +
            "@Resource='kohami:foundation:schema-migrations',@LockMode='Exclusive',@LockOwner='Session',@LockTimeout=30000; " +
            "SELECT @result", connection))
        {
            if (Convert.ToInt32(await acquire.ExecuteScalarAsync(cancellationToken)) < 0)
                throw new InvalidOperationException("SQL_MIGRATION_LOCK_FAILED");
        }

        try
        {
            await using (var scope = new SqlCommand("SELECT CASE WHEN DB_NAME()=@database AND " +
                "CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')) IS NOT NULL AND OBJECT_ID('ops.SchemaMigrations','U') IS NOT NULL THEN 1 ELSE 0 END", connection))
            {
                scope.Parameters.AddWithValue("@database", targetDatabase);
                if (Convert.ToInt32(await scope.ExecuteScalarAsync(cancellationToken)) != 1)
                    throw new InvalidOperationException("SQL_MIGRATION_HISTORY_MISSING");
            }

            var applied = await ReadAppliedAsync(connection, cancellationToken);
            var alreadyAppliedCount = targetIndex + 1;
            if (applied.Count == alreadyAppliedCount && plan.Take(alreadyAppliedCount).All(migration =>
                    applied.TryGetValue(migration.Id, out var hash) && StringComparer.Ordinal.Equals(hash, migration.SourceHash)))
                return false;
            if (applied.Count != targetIndex || plan.Take(targetIndex).Any(migration =>
                    !applied.TryGetValue(migration.Id, out var hash) || !StringComparer.Ordinal.Equals(hash, migration.SourceHash)))
                throw new InvalidOperationException("SQL_MIGRATION_TARGET_NOT_NEXT");

            await ApplyOneAsync(connection, plan[targetIndex], cancellationToken);
            Console.WriteLine("SQL_MIGRATION_APPLIED " + migrationId);
            return true;
        }
        finally
        {
            await using var release = new SqlCommand("EXEC sys.sp_releaseapplock " +
                "@Resource='kohami:foundation:schema-migrations',@LockOwner='Session'", connection);
            await release.ExecuteNonQueryAsync(CancellationToken.None);
        }
    }

    private static async Task<bool> HasLegacySchemaAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand("SELECT CASE WHEN EXISTS (SELECT 1 FROM sys.schemas s " +
            "JOIN sys.objects o ON o.schema_id=s.schema_id WHERE s.name NOT IN " +
            "('dbo','guest','INFORMATION_SCHEMA','sys','ops') AND o.is_ms_shipped=0) THEN 1 ELSE 0 END", connection);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) != 0;
    }

    private static async Task EnsureHistoryAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand("IF OBJECT_ID('ops.SchemaMigrations','U') IS NULL " +
            "BEGIN " +
            "IF EXISTS (SELECT 1 FROM sys.schemas s JOIN sys.objects o ON o.schema_id=s.schema_id " +
            "WHERE s.name NOT IN ('dbo','guest','INFORMATION_SCHEMA','sys','ops') AND o.is_ms_shipped=0) " +
            "THROW 51120,'SQL_MIGRATION_BASELINE_REQUIRED',1; " +
            "IF SCHEMA_ID('ops') IS NULL EXEC('CREATE SCHEMA ops AUTHORIZATION dbo'); " +
            "CREATE TABLE ops.SchemaMigrations(MigrationId varchar(100) NOT NULL PRIMARY KEY, " +
            "SourceHash char(64) NOT NULL, AppliedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_SchemaMigrations_AppliedAtUtc DEFAULT SYSUTCDATETIME()); " +
            "END", connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<Dictionary<string, string>> ReadAppliedAsync(SqlConnection connection,
        CancellationToken cancellationToken)
    {
        var applied = new Dictionary<string, string>(StringComparer.Ordinal);
        await using var command = new SqlCommand("SELECT MigrationId,SourceHash FROM ops.SchemaMigrations ORDER BY MigrationId", connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) applied.Add(reader.GetString(0), reader.GetString(1).Trim());
        return applied;
    }

    private static async Task ApplyOneAsync(SqlConnection connection, SqlMigrationDefinition migration,
        CancellationToken cancellationToken)
    {
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var batch in migration.Batches)
            {
                await using var command = new SqlCommand(batch.Sql, connection, transaction) { CommandTimeout = 60 };
                try
                {
                    await command.ExecuteNonQueryAsync(cancellationToken);
                }
                catch (SqlException error)
                {
                    var line = error.Errors.Count > 0 ? error.Errors[0].LineNumber : 0;
                    Console.Error.WriteLine($"SQL_MIGRATION_BATCH_FAILED {migration.Id} {batch.Source} line={line}");
                    if (error.Number == 207 && error.Errors.Count > 0)
                    {
                        var missingColumn = Regex.Match(error.Errors[0].Message, "Invalid column name '([^']+)'", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                        if (missingColumn.Success && Regex.IsMatch(missingColumn.Groups[1].Value, "^[A-Za-z0-9_]+$", RegexOptions.CultureInvariant))
                            Console.Error.WriteLine("SQL_MIGRATION_COLUMN_MISSING " + missingColumn.Groups[1].Value);
                    }
                    if (error.Number == 2714 && error.Errors.Count > 0)
                    {
                        var existing = Regex.Match(error.Errors[0].Message, "already an object named '([^']+)'", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                        if (existing.Success && Regex.IsMatch(existing.Groups[1].Value, "^[A-Za-z0-9_.\\[\\]-]+$", RegexOptions.CultureInvariant))
                            Console.Error.WriteLine("SQL_MIGRATION_OBJECT_EXISTS " + existing.Groups[1].Value);
                    }
                    if (error.Number == 5074 && error.Errors.Count > 0)
                    {
                        var identifiers = Regex.Matches(error.Errors[0].Message, "'([^']+)'")
                            .Select(match => match.Groups[1].Value)
                            .Where(value => Regex.IsMatch(value, "^[A-Za-z0-9_.\\[\\]]+$", RegexOptions.CultureInvariant))
                            .Take(2);
                        Console.Error.WriteLine("SQL_MIGRATION_DEPENDENCY " + string.Join(" ", identifiers));
                    }
                    throw;
                }
            }
            await using (var record = new SqlCommand("INSERT ops.SchemaMigrations(MigrationId,SourceHash) VALUES(@id,@hash)", connection, transaction))
            {
                record.Parameters.AddWithValue("@id", migration.Id);
                record.Parameters.AddWithValue("@hash", migration.SourceHash);
                await record.ExecuteNonQueryAsync(cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static IEnumerable<(string Source, string Sql)> ReadBatches(string root, string source)
    {
        var path = Path.Combine(root, source);
        if (!File.Exists(path)) throw new InvalidOperationException("MIGRATION_SOURCE_MISSING:" + source);
        var text = File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        return Regex.Split(text, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)
            .Where(batch => !string.IsNullOrWhiteSpace(batch)).Select(batch => (source, batch));
    }
}
