using System.Text.Json;
using Microsoft.Data.SqlClient;
using Kohami.M0.Core;
using Kohami.M0.Native;

namespace Kohami.M0.NativeTests;

public static class SqlMigrationAcceptance
{
    private const string SuccessId = "9001_migration_acceptance_success";
    private const string FailureId = "9002_migration_acceptance_failure";
    private const string SuccessTable = "ops.__MigrationAcceptanceSuccess";
    private const string FailureTable = "ops.__MigrationAcceptanceFailure";

    public static async Task RunAsync(string configPath)
    {
        var rootPath = Path.GetFullPath(Directory.GetCurrentDirectory());
        var expectedPath = Path.GetFullPath(Path.Combine(rootPath, ".local", "m0", "fixture-private.json"));
        var fullPath = Path.GetFullPath(configPath);
        if (!StringComparer.Ordinal.Equals(expectedPath, fullPath) || File.ResolveLinkTarget(fullPath, false) is not null ||
            (File.GetUnixFileMode(fullPath) & (UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute)) != 0)
            throw new InvalidOperationException("SQL_MIGRATION_ACCEPTANCE_CONFIG_INVALID");

        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(fullPath));
        var root = document.RootElement;
        if (!Guid.TryParse(root.GetProperty("DevelopmentFixtureId").GetString(), out var fixtureId))
            throw new InvalidOperationException("SQL_MIGRATION_ACCEPTANCE_FIXTURE_INVALID");
        var settings = M0Settings.Load(fullPath);
        var adminBuilder = new SqlConnectionStringBuilder(root.GetProperty("MigratorConnectionString").GetString())
        {
            Encrypt = SqlConnectionEncryptOption.Strict,
            TrustServerCertificate = false,
            HostNameInCertificate = settings.SqlServerName,
            ApplicationName = "KOHAMI-SqlMigrationAcceptanceCleanup",
        };
        adminBuilder["Server Certificate"] = settings.SqlCertificatePath;
        if (!DevelopmentFixtureBootstrapPolicy.IsDedicatedFixtureEndpoint(adminBuilder.DataSource) ||
            adminBuilder.InitialCatalog != "KohamiM0" || adminBuilder.UserID != "sa")
            throw new InvalidOperationException("SQL_MIGRATION_ACCEPTANCE_TARGET_INVALID");

        var secretsPath = Path.Combine(rootPath, ".local", "m0", "provisioner-private.json");
        if (File.ResolveLinkTarget(secretsPath, false) is not null ||
            (File.GetUnixFileMode(secretsPath) & (UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute)) != 0)
            throw new InvalidOperationException("SQL_MIGRATION_ACCEPTANCE_SECRETS_INVALID");
        using var secretsDocument = JsonDocument.Parse(await File.ReadAllTextAsync(secretsPath));
        var migratorBuilder = new SqlConnectionStringBuilder
        {
            DataSource = adminBuilder.DataSource,
            InitialCatalog = "KohamiM0",
            UserID = "kohami_m0_migrator",
            Password = secretsDocument.RootElement.GetProperty("migrator").GetString(),
            Encrypt = SqlConnectionEncryptOption.Strict,
            TrustServerCertificate = false,
            HostNameInCertificate = settings.SqlServerName,
            ApplicationName = "KOHAMI-SqlMigrationAcceptance",
        };
        migratorBuilder["Server Certificate"] = settings.SqlCertificatePath;

        new M0Sql(settings).ValidateTls();
        await using var connection = new SqlConnection(migratorBuilder.ConnectionString);
        await connection.OpenAsync();
        await using var adminConnection = new SqlConnection(adminBuilder.ConnectionString);
        await adminConnection.OpenAsync();
        await VerifyFixtureMarkerAsync(adminConnection, fixtureId);

        var plan = SqlMigrations.Load(rootPath);
        await SqlMigrations.ApplyAsync(connection, plan);
        await VerifyAppliedPlanAsync(connection, plan);

        var upgrade = new SqlMigrationDefinition(SuccessId,
        [
            ("acceptance", $"CREATE TABLE {SuccessTable}(Id int NOT NULL PRIMARY KEY, Value varchar(20) NOT NULL)"),
            ("acceptance", $"INSERT {SuccessTable}(Id,Value) VALUES(1,'applied')"),
        ]);
        var failedUpgrade = new SqlMigrationDefinition(FailureId,
        [
            ("acceptance", $"CREATE TABLE {FailureTable}(Id int NOT NULL PRIMARY KEY, Value varchar(20) NOT NULL)"),
            ("acceptance", $"INSERT {FailureTable}(Id,Value) VALUES(1,'must-rollback')"),
            ("acceptance", "THROW 51121,'EXPECTED_MIGRATION_FAILURE',1"),
        ]);
        var upgradePlan = plan.Concat([upgrade, failedUpgrade]).ToArray();
        try
        {
            try
            {
                await SqlMigrations.ApplyAsync(connection, upgradePlan);
                throw new InvalidOperationException("SQL_MIGRATION_ACCEPTANCE_FAILURE_NOT_RAISED");
            }
            catch (SqlException error) when (error.Number == 51121)
            {
                // The injected synthetic failure is the acceptance stimulus.
            }

            if (await ObjectExistsAsync(connection, SuccessTable) is false ||
                await ObjectExistsAsync(connection, FailureTable) ||
                await HistoryExistsAsync(connection, SuccessId) is false ||
                await HistoryExistsAsync(connection, FailureId))
                throw new InvalidOperationException("SQL_MIGRATION_ACCEPTANCE_ROLLBACK_INVALID");

            await SqlMigrations.ApplyAsync(connection, plan.Concat([upgrade]).ToArray());
            if (await ScalarAsync(connection, $"SELECT COUNT(*) FROM {SuccessTable}") != 1)
                throw new InvalidOperationException("SQL_MIGRATION_ACCEPTANCE_REPLAY_INVALID");
        }
        finally
        {
            await using var cleanup = new SqlCommand(
                $"DELETE ops.SchemaMigrations WHERE MigrationId IN (@success,@failure); DROP TABLE IF EXISTS {FailureTable}; DROP TABLE IF EXISTS {SuccessTable};", adminConnection);
            cleanup.Parameters.AddWithValue("@success", SuccessId);
            cleanup.Parameters.AddWithValue("@failure", FailureId);
            await cleanup.ExecuteNonQueryAsync();
            await VerifyFixtureMarkerAsync(adminConnection, fixtureId);
        }

        if (await HistoryExistsAsync(connection, SuccessId) || await ObjectExistsAsync(connection, SuccessTable) ||
            await ObjectExistsAsync(connection, FailureTable))
            throw new InvalidOperationException("SQL_MIGRATION_ACCEPTANCE_CLEANUP_FAILED");
        Console.WriteLine("PASS SQL_MIGRATION_UPGRADE_REPLAY_FAILURE_ROLLBACK_HISTORY_CLEANUP");
    }

    private static async Task VerifyFixtureMarkerAsync(SqlConnection connection, Guid fixtureId)
    {
        await using var command = new SqlCommand("SELECT COUNT(*) FROM ops.DevelopmentFixtureMarker WHERE FixtureId=@id AND DatasetId='demo_m0' AND Enabled=1", connection);
        command.Parameters.AddWithValue("@id", fixtureId);
        if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 1)
            throw new InvalidOperationException("SQL_MIGRATION_ACCEPTANCE_FIXTURE_MARKER_INVALID");
    }

    private static async Task VerifyAppliedPlanAsync(SqlConnection connection, IReadOnlyList<SqlMigrationDefinition> plan)
    {
        foreach (var migration in plan)
        {
            await using var command = new SqlCommand("SELECT SourceHash FROM ops.SchemaMigrations WHERE MigrationId=@id", connection);
            command.Parameters.AddWithValue("@id", migration.Id);
            var hash = Convert.ToString(await command.ExecuteScalarAsync());
            if (!StringComparer.Ordinal.Equals(hash?.Trim(), migration.SourceHash))
                throw new InvalidOperationException("SQL_MIGRATION_ACCEPTANCE_HISTORY_INVALID:" + migration.Id);
        }
    }

    private static async Task<bool> HistoryExistsAsync(SqlConnection connection, string id)
    {
        await using var command = new SqlCommand("SELECT CASE WHEN EXISTS(SELECT 1 FROM ops.SchemaMigrations WHERE MigrationId=@id) THEN 1 ELSE 0 END", connection);
        command.Parameters.AddWithValue("@id", id);
        return Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
    }

    private static async Task<bool> ObjectExistsAsync(SqlConnection connection, string name)
    {
        await using var command = new SqlCommand("SELECT CASE WHEN OBJECT_ID(@name,'U') IS NULL THEN 0 ELSE 1 END", connection);
        command.Parameters.AddWithValue("@name", name);
        return Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
    }

    private static async Task<int> ScalarAsync(SqlConnection connection, string sql)
    {
        await using var command = new SqlCommand(sql, connection);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }
}
