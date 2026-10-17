using System.Text.Json;
using Microsoft.Data.SqlClient;
using Kohami.M0.Core;
using Kohami.M0.Native;

namespace Kohami.M0.NativeTests;

public static class SqlBaselineAdoptionAcceptance
{
    private const string DriftTable = "m0.__BaselineAdoptionDrift";
    private sealed record History(string Id, string Hash);

    public static async Task RunAsync(string configPath)
    {
        var root = Path.GetFullPath(Directory.GetCurrentDirectory());
        var expectedPath = Path.GetFullPath(Path.Combine(root, ".local", "m0", "fixture-private.json"));
        var fullPath = Path.GetFullPath(configPath);
        if (!StringComparer.Ordinal.Equals(expectedPath, fullPath) || File.ResolveLinkTarget(fullPath, false) is not null ||
            (File.GetUnixFileMode(fullPath) & (UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute)) != 0)
            throw new InvalidOperationException("SQL_BASELINE_ACCEPTANCE_CONFIG_INVALID");

        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(fullPath));
        var config = document.RootElement;
        if (!config.TryGetProperty("IdentitySqlTestDedicated", out var dedicated) || !dedicated.GetBoolean() ||
            !Guid.TryParse(config.GetProperty("FixtureId").GetString(), out var fixtureId))
            throw new InvalidOperationException("SQL_BASELINE_ACCEPTANCE_FIXTURE_INVALID");

        var settings = M0Settings.Load(fullPath);
        var adminBuilder = new SqlConnectionStringBuilder(config.GetProperty("MigratorConnectionString").GetString())
        {
            Encrypt = SqlConnectionEncryptOption.Strict,
            TrustServerCertificate = false,
            HostNameInCertificate = settings.SqlServerName,
            ApplicationName = "KOHAMI-SqlBaselineAdoptionCleanup",
        };
        adminBuilder["Server Certificate"] = settings.SqlCertificatePath;
        if (!SqlProvision.IsDedicatedFixtureEndpoint(adminBuilder.DataSource) ||
            adminBuilder.InitialCatalog != "KohamiM0" || adminBuilder.UserID != "sa")
            throw new InvalidOperationException("SQL_BASELINE_ACCEPTANCE_TARGET_INVALID");

        var secretsPath = Path.Combine(root, ".local", "m0", "provisioner-private.json");
        if (File.ResolveLinkTarget(secretsPath, false) is not null ||
            (File.GetUnixFileMode(secretsPath) & (UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute)) != 0)
            throw new InvalidOperationException("SQL_BASELINE_ACCEPTANCE_SECRETS_INVALID");
        using var secretDocument = JsonDocument.Parse(await File.ReadAllTextAsync(secretsPath));
        var migratorBuilder = new SqlConnectionStringBuilder
        {
            DataSource = adminBuilder.DataSource,
            InitialCatalog = "KohamiM0",
            UserID = "kohami_m0_migrator",
            Password = secretDocument.RootElement.GetProperty("migrator").GetString(),
            Encrypt = SqlConnectionEncryptOption.Strict,
            TrustServerCertificate = false,
            HostNameInCertificate = settings.SqlServerName,
            ApplicationName = "KOHAMI-SqlBaselineAdoptionAcceptance",
        };
        migratorBuilder["Server Certificate"] = settings.SqlCertificatePath;
        new M0Sql(settings).ValidateTls();

        await using var admin = new SqlConnection(adminBuilder.ConnectionString);
        await admin.OpenAsync();
        await using var migrator = new SqlConnection(migratorBuilder.ConnectionString);
        await migrator.OpenAsync();
        await VerifyMarkerAsync(admin, fixtureId);

        var plan = SqlMigrations.Load(root);
        var originalHistory = await ReadHistoryAsync(admin);
        if (originalHistory.Count != plan.Count || plan.Any(m => !originalHistory.Any(h => h.Id == m.Id && h.Hash == m.SourceHash)))
            throw new InvalidOperationException("SQL_BASELINE_ACCEPTANCE_ORIGINAL_HISTORY_INVALID");

        try
        {
            await ExpectHistoryRejectAsync(admin, migrator, plan, originalHistory);
            await DropHistoryAsync(admin);
            var captured = await SqlBaselineAdoption.ComputeFingerprintAsync(admin, plan);
            await SqlBaselineAdoption.AdoptAsync(admin, migrator, plan);
            await VerifyHistoryAsync(admin, plan);

            await DropHistoryAsync(admin);
            var sourceDrift = plan.Select((migration, index) => index == 0
                ? new SqlMigrationDefinition(migration.Id, migration.Batches.Append(("acceptance", "-- changed baseline source")).ToArray())
                : migration).ToArray();
            await ExpectFingerprintRejectAsync(admin, migrator, sourceDrift, "migration-source");

            await DropHistoryAsync(admin);
            await SqlProvision.Execute(admin, $"CREATE TABLE {DriftTable}(Id int NOT NULL PRIMARY KEY)");
            await ExpectFingerprintRejectAsync(admin, migrator, plan, "schema-object");
            await SqlProvision.Execute(admin, $"DROP TABLE {DriftTable}");

            await DropHistoryAsync(admin);
            await SqlProvision.Execute(admin, "REVOKE SELECT ON OBJECT::m0.SecurityState FROM m0_api");
            await ExpectFingerprintRejectAsync(admin, migrator, plan, "database-grant");
            await SqlProvision.Execute(admin, "GRANT SELECT ON OBJECT::m0.SecurityState TO m0_api");
            var restoredFingerprint = await SqlBaselineAdoption.ComputeFingerprintAsync(admin, plan);
            if (!StringComparer.Ordinal.Equals(restoredFingerprint, captured))
                throw new InvalidOperationException("SQL_BASELINE_ACCEPTANCE_RESTORE_INVALID");
        }
        finally
        {
            await RestoreAsync(admin, originalHistory);
            await VerifyMarkerAsync(admin, fixtureId);
        }

        await VerifyHistoryAsync(admin, plan);
        Console.WriteLine("PASS SQL_BASELINE_ADOPTION_EXACT_SCHEMA_AND_GRANTS_REJECT_DRIFT_RESTORE_FIXTURE");
    }

    private static async Task ExpectFingerprintRejectAsync(SqlConnection metadata, SqlConnection migrator,
        IReadOnlyList<SqlMigrationDefinition> plan, string label)
    {
        try
        {
            await SqlBaselineAdoption.AdoptAsync(metadata, migrator, plan);
            throw new InvalidOperationException("SQL_BASELINE_ACCEPTANCE_DRIFT_ACCEPTED:" + label);
        }
        catch (InvalidOperationException error) when (error.Message == "SQL_BASELINE_FINGERPRINT_MISMATCH")
        {
            if (await ScalarAsync(metadata, "SELECT CASE WHEN OBJECT_ID('ops.SchemaMigrations','U') IS NULL THEN 1 ELSE 0 END") != 1)
                throw new InvalidOperationException("SQL_BASELINE_ACCEPTANCE_DRIFT_CREATED_HISTORY:" + label);
        }
    }

    private static async Task ExpectHistoryRejectAsync(SqlConnection metadata, SqlConnection migrator,
        IReadOnlyList<SqlMigrationDefinition> plan, IReadOnlyList<History> originalHistory)
    {
        try
        {
            await SqlBaselineAdoption.AdoptAsync(metadata, migrator, plan);
            throw new InvalidOperationException("SQL_BASELINE_ACCEPTANCE_EXISTING_HISTORY_ACCEPTED");
        }
        catch (InvalidOperationException error) when (error.Message == "SQL_BASELINE_ADOPTION_HISTORY_INVALID")
        {
            var rows = await ReadHistoryAsync(metadata);
            if (!rows.SequenceEqual(originalHistory))
                throw new InvalidOperationException("SQL_BASELINE_ACCEPTANCE_EXISTING_HISTORY_CHANGED");
        }
    }

    private static async Task<List<History>> ReadHistoryAsync(SqlConnection connection)
    {
        var result = new List<History>();
        await using var command = new SqlCommand("SELECT MigrationId,SourceHash FROM ops.SchemaMigrations ORDER BY MigrationId", connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) result.Add(new History(reader.GetString(0), reader.GetString(1).Trim()));
        return result;
    }

    private static async Task VerifyHistoryAsync(SqlConnection connection, IReadOnlyList<SqlMigrationDefinition> plan)
    {
        var rows = await ReadHistoryAsync(connection);
        if (rows.Count != plan.Count || plan.Any(m => !rows.Any(row => row.Id == m.Id && row.Hash == m.SourceHash)))
            throw new InvalidOperationException("SQL_BASELINE_ACCEPTANCE_HISTORY_INVALID");
    }

    private static async Task DropHistoryAsync(SqlConnection connection) =>
        await SqlProvision.Execute(connection, "DROP TABLE IF EXISTS ops.SchemaMigrations");

    private static async Task RestoreAsync(SqlConnection connection, IReadOnlyList<History> history)
    {
        await SqlProvision.Execute(connection, $"DROP TABLE IF EXISTS {DriftTable}; " +
            "GRANT SELECT ON OBJECT::m0.SecurityState TO m0_api; DROP TABLE IF EXISTS ops.SchemaMigrations; " +
            "CREATE TABLE ops.SchemaMigrations(MigrationId varchar(100) NOT NULL PRIMARY KEY, SourceHash char(64) NOT NULL, " +
            "AppliedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_SchemaMigrations_AppliedAtUtc DEFAULT SYSUTCDATETIME()); " +
            "GRANT SELECT,INSERT ON OBJECT::ops.SchemaMigrations TO m0_migrator;");
        foreach (var row in history)
        {
            await using var insert = new SqlCommand("INSERT ops.SchemaMigrations(MigrationId,SourceHash) VALUES(@id,@hash)", connection);
            insert.Parameters.AddWithValue("@id", row.Id);
            insert.Parameters.AddWithValue("@hash", row.Hash);
            await insert.ExecuteNonQueryAsync();
        }
    }

    private static async Task VerifyMarkerAsync(SqlConnection connection, Guid fixtureId)
    {
        await using var command = new SqlCommand("SELECT COUNT(*) FROM ops.DevelopmentFixtureMarker WHERE FixtureId=@id AND DatasetId='demo_m0'", connection);
        command.Parameters.AddWithValue("@id", fixtureId);
        if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 1)
            throw new InvalidOperationException("SQL_BASELINE_ACCEPTANCE_FIXTURE_MARKER_INVALID");
    }

    private static async Task<int> ScalarAsync(SqlConnection connection, string sql)
    {
        await using var command = new SqlCommand(sql, connection);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

}
