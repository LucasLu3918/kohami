using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Kohami.M0.Core;
using Kohami.M0.Security;

namespace Kohami.M0.Native;

public sealed record DevelopmentFixtureTarget(
    string EnvironmentName,
    bool OptIn,
    bool Interactive,
    bool ConfigIsPrivate,
    bool ConfigIsSymlink,
    bool Dedicated,
    bool FixtureEnabled,
    string Endpoint,
    string Database,
    string Dataset,
    string SqlServerName,
    bool TlsStrict,
    bool CertificatePinValid,
    string ClientAddress,
    string ApplicationName);

public static class DevelopmentFixtureBootstrapPolicy
{
    public static void Validate(DevelopmentFixtureTarget target)
    {
        if (target.EnvironmentName != "Development" || !target.OptIn || !target.Interactive)
            throw new InvalidOperationException("DEV_FIXTURE_BOOTSTRAP_REQUIRES_INTERACTIVE_OPT_IN");
        if (!target.ConfigIsPrivate || target.ConfigIsSymlink || !target.Dedicated || !target.FixtureEnabled)
            throw new InvalidOperationException("DEV_FIXTURE_AUTHORITY_INVALID");
        if (target.Endpoint is not ("localhost,14336" or "127.0.0.1,14336") ||
            target.ClientAddress is not ("localhost" or "127.0.0.1") ||
            target.Database != "KohamiM0" || target.Dataset != "demo_m0" || target.SqlServerName != "localhost")
            throw new InvalidOperationException("DEV_FIXTURE_TARGET_INVALID");
        if (!target.TlsStrict || !target.CertificatePinValid || target.ApplicationName != "KOHAMI-DevFixtureBootstrap")
            throw new InvalidOperationException("DEV_FIXTURE_CONNECTION_INVALID");
    }

    public static void ValidateActivation(string presented, string confirmation, DateTimeOffset issuedAt,
        DateTimeOffset expiresAt, DateTimeOffset now, string expectedFingerprint, string actualFingerprint)
    {
        var left = Encoding.UTF8.GetBytes(presented);
        var right = Encoding.UTF8.GetBytes(confirmation);
        if (left.Length < 32 || left.Length != right.Length || !CryptographicOperations.FixedTimeEquals(left, right))
            throw new InvalidOperationException("DEV_FIXTURE_ACTIVATION_CONFIRMATION_INVALID");
        if (expectedFingerprint.Length != 64 || !StringComparer.Ordinal.Equals(expectedFingerprint, actualFingerprint))
            throw new InvalidOperationException("DEV_FIXTURE_INSTANCE_INVALID");
        if (issuedAt > now || expiresAt <= now || expiresAt > issuedAt.AddMinutes(5))
            throw new InvalidOperationException("DEV_FIXTURE_ACTIVATION_EXPIRED");
    }

    public static string ActivationHash(string token, string operationId, string fingerprint)
    {
        var bytes = Encoding.UTF8.GetBytes($"{token}|{operationId}|{fingerprint}|demo_m0");
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }
}

public static class DevelopmentFixtureBootstrap
{
    const string ApplicationName = "KOHAMI-DevFixtureBootstrap";

    public static async Task RunSqlChecksAsync(string configPath)
    {
        var fullPath = Path.GetFullPath(configPath);
        var expected = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), ".local", "m0", "fixture-private.json"));
        if (!StringComparer.Ordinal.Equals(expected, fullPath) || File.ResolveLinkTarget(fullPath, false) is not null ||
            (File.GetUnixFileMode(fullPath) & (UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute)) != 0)
            throw new InvalidOperationException("DEV_FIXTURE_CONFIG_PATH_INVALID");

        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(fullPath));
        var root = document.RootElement;
        if (!root.TryGetProperty("IdentitySqlTestDedicated", out var dedicated) || !dedicated.GetBoolean() ||
            !root.TryGetProperty("DevelopmentFixtureBootstrapEnabled", out var enabled) || !enabled.GetBoolean() ||
            !Guid.TryParse(root.GetProperty("DevelopmentFixtureId").GetString(), out var fixtureId))
            throw new InvalidOperationException("DEV_FIXTURE_AUTHORITY_INVALID");
        var settings = M0Settings.Load(fullPath);
        var connection = new SqlConnectionStringBuilder(root.GetProperty("BootstrapConnectionString").GetString())
        {
            Encrypt = SqlConnectionEncryptOption.Strict,
            TrustServerCertificate = false,
            HostNameInCertificate = settings.SqlServerName,
            ApplicationName = ApplicationName,
        };
        connection["Server Certificate"] = settings.SqlCertificatePath;
        if (connection.DataSource != "localhost,14336" || connection.InitialCatalog != "KohamiM0" ||
            connection.UserID != "kohami_m0_fixture_bootstrap")
            throw new InvalidOperationException("DEV_FIXTURE_TARGET_INVALID");

        new M0Sql(settings).ValidateTls();
        await using var sql = new SqlConnection(connection.ConnectionString);
        await sql.OpenAsync();
        string fingerprint;
        await using (var inspect = new SqlCommand("EXEC ops.InspectDevelopmentFixture @FixtureId", sql))
        {
            inspect.Parameters.AddWithValue("@FixtureId", fixtureId);
            await using var reader = await inspect.ExecuteReaderAsync();
            if (!await reader.ReadAsync() || reader.GetGuid(reader.GetOrdinal("FixtureId")) != fixtureId ||
                reader.GetString(reader.GetOrdinal("DatasetId")) != "demo_m0" ||
                !reader.GetBoolean(reader.GetOrdinal("Enabled")) || reader.GetBoolean(reader.GetOrdinal("BootstrapConsumed")) ||
                !reader.GetBoolean(reader.GetOrdinal("Maintenance")) || reader.GetBoolean(reader.GetOrdinal("OwnerPrepared")) ||
                reader.GetBoolean(reader.GetOrdinal("BootstrapActivated")) || reader.GetBoolean(reader.GetOrdinal("OwnerExists")))
                throw new InvalidOperationException("DEV_FIXTURE_STATE_REJECTED");
            fingerprint = reader.GetString(reader.GetOrdinal("InstanceFingerprint"));
        }

        var denied = false;
        try
        {
            await using var apply = new SqlCommand("EXEC ops.DevelopmentFixtureBootstrapOwner @FixtureId,@InstanceFingerprint,@OperationId,@ActivationHash,@ExpiresAt,@UserId,@Email,@PasswordHash,@EncryptedTotpSeed", sql);
            apply.Parameters.AddWithValue("@FixtureId", fixtureId);
            apply.Parameters.AddWithValue("@InstanceFingerprint", new string('0', 64));
            apply.Parameters.AddWithValue("@OperationId", Guid.NewGuid());
            apply.Parameters.AddWithValue("@ActivationHash", new string('a', 64));
            apply.Parameters.AddWithValue("@ExpiresAt", DateTimeOffset.UtcNow.AddMinutes(5));
            apply.Parameters.AddWithValue("@UserId", "devfixture_owner_negative_test");
            apply.Parameters.AddWithValue("@Email", "negative-test@example.test");
            apply.Parameters.AddWithValue("@PasswordHash", new string('x', 100));
            apply.Parameters.AddWithValue("@EncryptedTotpSeed", "synthetic-only");
            await apply.ExecuteNonQueryAsync();
        }
        catch (SqlException error) when (error.Number == 51100) { denied = true; }
        if (!denied) throw new InvalidOperationException("DEV_FIXTURE_WRONG_INSTANCE_NOT_REJECTED");

        denied = false;
        try
        {
            await using var direct = new SqlCommand("UPDATE m0.SecurityState SET Maintenance=Maintenance WHERE Id=1", sql);
            await direct.ExecuteNonQueryAsync();
        }
        catch (SqlException error) when (error.Number == 229) { denied = true; }
        if (!denied) throw new InvalidOperationException("DEV_FIXTURE_DIRECT_DML_NOT_DENIED");

        await using (var verify = new SqlCommand("EXEC ops.InspectDevelopmentFixture @FixtureId", sql))
        {
            verify.Parameters.AddWithValue("@FixtureId", fixtureId);
            await using var reader = await verify.ExecuteReaderAsync();
            if (!await reader.ReadAsync() || reader.GetString(reader.GetOrdinal("InstanceFingerprint")) != fingerprint ||
                !reader.GetBoolean(reader.GetOrdinal("Enabled")) || reader.GetBoolean(reader.GetOrdinal("BootstrapConsumed")) ||
                !reader.GetBoolean(reader.GetOrdinal("Maintenance")) || reader.GetBoolean(reader.GetOrdinal("OwnerExists")))
                throw new InvalidOperationException("DEV_FIXTURE_NEGATIVE_TEST_MUTATED_STATE");
        }
        Console.WriteLine("PASS DEV_FIXTURE_SQL_WRONG_INSTANCE_AND_DIRECT_DML_DENIED_STATE_UNCHANGED");
    }

    public static async Task VerifySqlBootstrapAsync(string configPath)
    {
        var fullPath = Path.GetFullPath(configPath);
        var expected = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), ".local", "m0", "fixture-private.json"));
        if (!StringComparer.Ordinal.Equals(expected, fullPath) || File.ResolveLinkTarget(fullPath, false) is not null ||
            (File.GetUnixFileMode(fullPath) & (UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute)) != 0)
            throw new InvalidOperationException("DEV_FIXTURE_CONFIG_PATH_INVALID");
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(fullPath));
        var root = document.RootElement;
        if (!Guid.TryParse(root.GetProperty("DevelopmentFixtureId").GetString(), out var fixtureId))
            throw new InvalidOperationException("DEV_FIXTURE_ID_INVALID");
        var settings = M0Settings.Load(fullPath);
        var connection = new SqlConnectionStringBuilder(root.GetProperty("BootstrapConnectionString").GetString())
        {
            Encrypt = SqlConnectionEncryptOption.Strict,
            TrustServerCertificate = false,
            HostNameInCertificate = settings.SqlServerName,
            ApplicationName = ApplicationName,
        };
        connection["Server Certificate"] = settings.SqlCertificatePath;
        if (connection.DataSource != "localhost,14336" || connection.InitialCatalog != "KohamiM0" ||
            connection.UserID != "kohami_m0_fixture_bootstrap")
            throw new InvalidOperationException("DEV_FIXTURE_TARGET_INVALID");
        new M0Sql(settings).ValidateTls();
        await using var sql = new SqlConnection(connection.ConnectionString);
        await sql.OpenAsync();
        await using var inspect = new SqlCommand("EXEC ops.InspectDevelopmentFixture @FixtureId", sql);
        inspect.Parameters.AddWithValue("@FixtureId", fixtureId);
        await using var reader = await inspect.ExecuteReaderAsync();
        if (!await reader.ReadAsync() || reader.GetGuid(reader.GetOrdinal("FixtureId")) != fixtureId ||
            reader.GetString(reader.GetOrdinal("DatasetId")) != "demo_m0" ||
            reader.GetBoolean(reader.GetOrdinal("Enabled")) || !reader.GetBoolean(reader.GetOrdinal("BootstrapConsumed")) ||
            reader.GetBoolean(reader.GetOrdinal("Maintenance")) || reader.GetBoolean(reader.GetOrdinal("OwnerPrepared")) ||
            reader.GetBoolean(reader.GetOrdinal("BootstrapActivated")) || !reader.GetBoolean(reader.GetOrdinal("OwnerExists")) ||
            reader.GetInt32(reader.GetOrdinal("OwnerGrantCount")) != 7 ||
            !reader.GetBoolean(reader.GetOrdinal("BootstrapAuditExists")))
            throw new InvalidOperationException("DEV_FIXTURE_BOOTSTRAP_SQL_STATE_INVALID");
        Console.WriteLine("PASS DEV_FIXTURE_OWNER_ACTIVE_SEVEN_DATASET_GRANTS_ONE_TIME_MARKER_CONSUMED_AUDIT_PRESENT_MAINTENANCE_CLOSED");
    }

    public static async Task RunAsync(string configPath)
    {
        if (Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") != "Development" ||
            Environment.GetEnvironmentVariable("M0_DEV_FIXTURE_BOOTSTRAP") != "1")
            throw new InvalidOperationException("DEV_FIXTURE_BOOTSTRAP_OPT_IN_REQUIRED");
        if (Console.IsInputRedirected || Console.IsOutputRedirected)
            throw new InvalidOperationException("DEV_FIXTURE_BOOTSTRAP_REQUIRES_TTY");

        var expected = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), ".local", "m0", "fixture-private.json"));
        var fullPath = Path.GetFullPath(configPath);
        if (!StringComparer.Ordinal.Equals(expected, fullPath))
            throw new InvalidOperationException("DEV_FIXTURE_CONFIG_PATH_INVALID");
        if (File.ResolveLinkTarget(fullPath, false) is not null)
            throw new InvalidOperationException("DEV_FIXTURE_CONFIG_SYMLINK_REJECTED");
        var mode = File.GetUnixFileMode(fullPath);
        var privateConfig = (mode & (UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute)) == 0;

        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(fullPath));
        var root = document.RootElement;
        string Read(string name) => root.TryGetProperty(name, out var property) ? property.GetString() ?? "" : "";
        bool Flag(string name) => root.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.True;
        var fixtureIdText = Read("DevelopmentFixtureId");
        if (!Guid.TryParse(fixtureIdText, out var fixtureId))
            throw new InvalidOperationException("DEV_FIXTURE_ID_INVALID");
        var connectionText = Read("BootstrapConnectionString");
        var connection = new SqlConnectionStringBuilder(connectionText);
        var endpointHost = connection.DataSource.Split(',', 2)[0];
        var runtimeSettings = M0Settings.Load(fullPath);
        var tls = new M0Sql(runtimeSettings);
        tls.ValidateTls();
        connection.Encrypt = SqlConnectionEncryptOption.Strict;
        connection.TrustServerCertificate = false;
        connection.HostNameInCertificate = runtimeSettings.SqlServerName;
        connection["Server Certificate"] = runtimeSettings.SqlCertificatePath;
        connection.ApplicationName = ApplicationName;

        DevelopmentFixtureBootstrapPolicy.Validate(new(
            Read("DOTNET_ENVIRONMENT"),
            Environment.GetEnvironmentVariable("M0_DEV_FIXTURE_BOOTSTRAP") == "1",
            !Console.IsInputRedirected && !Console.IsOutputRedirected,
            privateConfig,
            File.ResolveLinkTarget(fullPath, false) is not null,
            Flag("IdentitySqlTestDedicated"),
            Flag("DevelopmentFixtureBootstrapEnabled"),
            connection.DataSource,
            connection.InitialCatalog,
            runtimeSettings.DatasetId,
            runtimeSettings.SqlServerName,
            connection.Encrypt == SqlConnectionEncryptOption.Strict && !connection.TrustServerCertificate,
            File.Exists(runtimeSettings.SqlCertificatePath),
            endpointHost,
            connection.ApplicationName));
        if (connection.UserID != "kohami_m0_fixture_bootstrap")
            throw new InvalidOperationException("DEV_FIXTURE_PRINCIPAL_INVALID");

        var crypto = M0Crypto.Load(runtimeSettings);
        var operationId = Guid.NewGuid();
        string fingerprint;
        await using (var sql = new SqlConnection(connection.ConnectionString))
        {
            await sql.OpenAsync();
            await using var inspect = new SqlCommand("EXEC ops.InspectDevelopmentFixture @FixtureId", sql);
            inspect.Parameters.AddWithValue("@FixtureId", fixtureId);
            await using var reader = await inspect.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) throw new InvalidOperationException("DEV_FIXTURE_MARKER_MISSING");
            if (reader.GetGuid(reader.GetOrdinal("FixtureId")) != fixtureId ||
                reader.GetString(reader.GetOrdinal("DatasetId")) != "demo_m0" ||
                !reader.GetBoolean(reader.GetOrdinal("Enabled")) ||
                reader.GetBoolean(reader.GetOrdinal("BootstrapConsumed")) ||
                !reader.GetBoolean(reader.GetOrdinal("Maintenance")) ||
                reader.GetBoolean(reader.GetOrdinal("OwnerPrepared")) ||
                reader.GetBoolean(reader.GetOrdinal("BootstrapActivated")) ||
                reader.GetBoolean(reader.GetOrdinal("OwnerExists")))
                throw new InvalidOperationException("DEV_FIXTURE_STATE_REJECTED");
            fingerprint = reader.GetString(reader.GetOrdinal("InstanceFingerprint"));
            if (fingerprint.Length != 64 || fingerprint.Any(c => !Uri.IsHexDigit(c)))
                throw new InvalidOperationException("DEV_FIXTURE_FINGERPRINT_INVALID");
        }

        var userId = "devfixture_owner_" + Guid.NewGuid().ToString("N");
        var email = "issue02-owner@example.test";
        Console.WriteLine("Development-only Issue 02 fixture bootstrap");
        Console.WriteLine("Target: https://localhost:3000 → localhost:14336/KohamiM0 → demo_m0");
        Console.WriteLine("Fixture fingerprint: sha256:" + fingerprint);
        Console.WriteLine("Synthetic Owner: " + email + " (dataset-scoped, 2 hour grants)");
        Console.WriteLine("No production/native Owner state, payment provider or tax provider will be used.");
        Console.WriteLine("本機 TOTP 註冊資料只顯示在此互動終端；不要貼到聊天或記錄檔。");

        var seed = RandomNumberGenerator.GetBytes(20);
        try
        {
            var key = Totp.Encode(seed);
            var label = Uri.EscapeDataString("Kohami:" + email);
            Console.Error.WriteLine($"Authenticator URI: otpauth://totp/{label}?secret={key}&issuer=Kohami&algorithm=SHA1&digits=6&period=30");
            Console.Error.Write("請先將此帳號加入真正的 TOTP 驗證器，再輸入目前 6 位數代碼：");
            var code = ReadSecret();
            var now = DateTimeOffset.UtcNow;
            if (Totp.Match(seed, code, now, -1) is null)
                throw new InvalidOperationException("DEV_FIXTURE_TOTP_PROOF_INVALID");

            var issuedAt = DateTimeOffset.UtcNow;
            var expiresAt = issuedAt.AddMinutes(5);
            var activation = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            Console.WriteLine("一次性啟用確認碼（有效 5 分鐘）：");
            Console.WriteLine(activation);
            Console.Error.Write("請在有效期限內重新輸入確認碼：");
            var confirmation = ReadSecret();
            DevelopmentFixtureBootstrapPolicy.ValidateActivation(activation, confirmation, issuedAt, expiresAt,
                DateTimeOffset.UtcNow, fingerprint, fingerprint);

            var password = ReadPassword();
            var passwordHash = new PasswordHasher<object>().HashPassword(new object(), password);
            var encryptedSeed = crypto.Totp.CreateProtector("kohami.m0.totp-seed", runtimeSettings.DatasetId, userId).Protect(key);
            var activationHash = DevelopmentFixtureBootstrapPolicy.ActivationHash(activation, operationId.ToString("D"), fingerprint);

            await using var target = new SqlConnection(connection.ConnectionString);
            await target.OpenAsync();
            await using var apply = new SqlCommand("EXEC ops.DevelopmentFixtureBootstrapOwner @FixtureId,@InstanceFingerprint,@OperationId,@ActivationHash,@ExpiresAt,@UserId,@Email,@PasswordHash,@EncryptedTotpSeed", target);
            apply.Parameters.AddWithValue("@FixtureId", fixtureId);
            apply.Parameters.AddWithValue("@InstanceFingerprint", fingerprint);
            apply.Parameters.AddWithValue("@OperationId", operationId);
            apply.Parameters.AddWithValue("@ActivationHash", activationHash);
            apply.Parameters.AddWithValue("@ExpiresAt", expiresAt);
            apply.Parameters.AddWithValue("@UserId", userId);
            apply.Parameters.AddWithValue("@Email", email);
            apply.Parameters.AddWithValue("@PasswordHash", passwordHash);
            apply.Parameters.AddWithValue("@EncryptedTotpSeed", encryptedSeed);
            await apply.ExecuteNonQueryAsync();
            Console.WriteLine("PASS DEVELOPMENT_FIXTURE_OWNER_CREATED_TOTP_VERIFIED_BOOTSTRAP_CLOSED");
            Console.WriteLine("Synthetic Owner email: " + email);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(seed);
        }
    }

    static string ReadSecret()
    {
        var value = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(true);
            if (key.Key == ConsoleKey.Enter) break;
            if (key.Key == ConsoleKey.Backspace) { if (value.Length > 0) value.Length--; continue; }
            if (!char.IsControl(key.KeyChar)) value.Append(key.KeyChar);
            if (value.Length > 256) throw new InvalidOperationException("DEV_FIXTURE_INPUT_INVALID");
        }
        Console.Error.WriteLine();
        return value.ToString();
    }

    static string ReadPassword()
    {
        Console.Error.Write("本機合成 Owner 密碼（隱藏輸入，至少 15 字元）：");
        var password = ReadSecret();
        if (password.Length is < 15 or > 256 || new[] { "passwordpassword", "123456789012345", "kohamikohamikohami" }.Contains(password.ToLowerInvariant()))
            throw new InvalidOperationException("DEV_FIXTURE_PASSWORD_POLICY");
        return password;
    }
}
