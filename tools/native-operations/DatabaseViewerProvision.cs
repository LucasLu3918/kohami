using System.Security.Cryptography;
using System.Text.Json;
using Kohami.Core;
using Microsoft.Data.SqlClient;

namespace Kohami.Native;

public static class DatabaseViewerProvision
{
    private const string Endpoint = "localhost,14349";
    private const string Database = "Kohami";
    private const string Login = "kohami_reader";
    private const string LoginPasswordOption = "PASSWORD=";

    public static string GenerateCredential() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(36));

    public static string CreateLoginSql() => string.Concat(
        "DECLARE @sql nvarchar(max)=N'CREATE LOGIN [kohami_reader] WITH ",
        LoginPasswordOption,
        "'+QUOTENAME(@credential,'''')+N', CHECK_POLICY=ON, CHECK_EXPIRATION=OFF'; EXEC sys.sp_executesql @sql;");

    public static async Task ProvisionAsync(string root)
    {
        if (OperatingSystem.IsWindows())
            throw new InvalidOperationException("VIEWER_PROVISIONING_REQUIRES_PRIVATE_UNIX_FILE_MODE");

        var repository = Path.GetFullPath(root);
        var localRoot = Path.Combine(repository, ".local");
        var profile = Path.Combine(localRoot, "kohami-sql", "identity");
        RequireDirectory(localRoot);
        RequireDirectory(Path.Combine(localRoot, "kohami-sql"));
        RequireDirectory(profile);

        var runtimePath = Path.Combine(profile, "runtime-private.json");
        var provisionerPath = Path.Combine(profile, "provisioner-private.json");
        var viewerEnvPath = Path.Combine(profile, "db-viewer.env");
        RequirePrivateFile(runtimePath);
        RequirePrivateFile(provisionerPath);
        if (File.Exists(viewerEnvPath) || new FileInfo(viewerEnvPath).LinkTarget is not null)
            throw new InvalidOperationException("VIEWER_CREDENTIAL_FILE_ALREADY_EXISTS");

        var settings = RuntimeSettings.Load(runtimePath);
        new SqlRuntime(settings).ValidateTls();
        var runtime = new SqlConnectionStringBuilder(settings.ConnectionString);
        if (!string.Equals(runtime.DataSource, Endpoint, StringComparison.OrdinalIgnoreCase)
            || runtime.InitialCatalog != Database || settings.DatasetId != "kohami_local")
            throw new InvalidOperationException("KOHAMI_SQL_TARGET_INVALID");

        var secrets = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(provisionerPath))
            ?? throw new InvalidOperationException("PROVISIONER_CONFIG_INVALID");
        if (!Guid.TryParse(secrets.GetValueOrDefault("fixtureId"), out var fixtureId)
            || string.IsNullOrWhiteSpace(secrets.GetValueOrDefault("sa")))
            throw new InvalidOperationException("DEDICATED_FIXTURE_MARKER_REQUIRED");

        var viewerCredential = GenerateCredential();
        var temporaryPath = viewerEnvPath + ".tmp-" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(8));
        var transactionStarted = false;
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(temporaryPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                using var writer = new StreamWriter(stream);
                writer.WriteLine($"KOHAMI_DB_VIEWER_SQL_USER={Login}");
                writer.WriteLine($"KOHAMI_DB_VIEWER_SQL_PASSWORD={viewerCredential}");
            }

            var admin = new SqlConnectionStringBuilder(settings.ConnectionString)
            {
                DataSource = Endpoint,
                InitialCatalog = "master",
                UserID = "sa",
                Password = secrets["sa"],
                Encrypt = SqlConnectionEncryptOption.Strict,
                TrustServerCertificate = false,
                HostNameInCertificate = settings.SqlServerName,
            };
            admin["Server Certificate"] = settings.SqlCertificatePath;
            await using var connection = new SqlConnection(admin.ConnectionString);
            await connection.OpenAsync();

            using (var marker = new SqlCommand($"""
                DECLARE @fp char(64)=LOWER(CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CONVERT(nvarchar(100),SERVERPROPERTY('ServerName')),'|{Database}|kohami_local')),2));
                SELECT COUNT(*) FROM [{Database}].ops.DevelopmentFixtureMarker WHERE FixtureId=@id AND DatasetId='kohami_local' AND InstanceFingerprint=@fp;
                """, connection))
            {
                marker.Parameters.AddWithValue("@id", fixtureId);
                if (Convert.ToInt32(await marker.ExecuteScalarAsync()) != 1)
                    throw new InvalidOperationException("DEDICATED_FIXTURE_MARKER_MISMATCH");
            }

            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
            transactionStarted = true;

            using (var exists = new SqlCommand("SELECT COUNT(*) FROM sys.server_principals WHERE name=@name", connection, transaction))
            {
                exists.Parameters.AddWithValue("@name", Login);
                if (Convert.ToInt32(await exists.ExecuteScalarAsync()) != 0)
                    throw new InvalidOperationException("VIEWER_LOGIN_ALREADY_EXISTS_WITHOUT_RECOVERABLE_CREDENTIAL_FILE");
            }

            using (var createLogin = new SqlCommand(CreateLoginSql(), connection, transaction))
            {
                createLogin.Parameters.AddWithValue("@credential", viewerCredential);
                await createLogin.ExecuteNonQueryAsync();
            }

            using (var createUser = new SqlCommand($"""
                USE [{Database}];
                CREATE USER [kohami_reader] FOR LOGIN [kohami_reader];
                ALTER ROLE [db_datareader] ADD MEMBER [kohami_reader];
                """, connection, transaction))
                await createUser.ExecuteNonQueryAsync();

            File.Move(temporaryPath, viewerEnvPath);
            File.SetUnixFileMode(viewerEnvPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            await transaction.CommitAsync();
            transactionStarted = false;
            Console.WriteLine("PASS LOCAL_DATABASE_VIEWER_READONLY_LOGIN_CREATED");
        }
        catch
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            if (transactionStarted && File.Exists(viewerEnvPath)) File.Delete(viewerEnvPath);
            throw;
        }
    }

    private static void RequireDirectory(string path)
    {
        var directory = new DirectoryInfo(path);
        if (!directory.Exists || directory.LinkTarget is not null)
            throw new InvalidOperationException("LOCAL_PROFILE_INVALID");
    }

    private static void RequirePrivateFile(string path)
    {
        var file = new FileInfo(path);
        if (!file.Exists || file.LinkTarget is not null
            || (File.GetUnixFileMode(path) & (UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute
                | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute)) != 0)
            throw new InvalidOperationException("PRIVATE_PROFILE_INVALID");
    }
}
