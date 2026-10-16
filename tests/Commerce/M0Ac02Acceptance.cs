using System.Text.Json;
using Microsoft.Data.SqlClient;
using Kohami.M0.Core;

internal static class M0Ac02Acceptance
{
    sealed record FixtureWindow(string Key, bool PreviousMaintenance);
    static SqlConnectionStringBuilder Admin(string configPath)
    {
        using var json = JsonDocument.Parse(File.ReadAllText(configPath));
        var root = json.RootElement;
        if (!root.TryGetProperty("IdentitySqlTestDedicated", out var dedicated) || !dedicated.GetBoolean()
            || root.GetProperty("datasetId").GetString() != "demo_m0")
            throw new InvalidOperationException("DEDICATED_ISSUE06_FIXTURE_REQUIRED");
        var settings = M0Settings.Load(configPath);
        var admin = new SqlConnectionStringBuilder(root.GetProperty("MigratorConnectionString").GetString())
        {
            Encrypt = SqlConnectionEncryptOption.Strict,
            TrustServerCertificate = false,
            HostNameInCertificate = "localhost"
        };
        admin["Server Certificate"] = settings.SqlCertificatePath;
        if (!string.Equals(admin.DataSource, "localhost,14336", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(admin.InitialCatalog, "KohamiM0", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("ISSUE06_FIXTURE_TARGET_INVALID");
        return admin;
    }

    static void ValidateKey(string key)
    {
        if (!key.StartsWith("m0ac02-", StringComparison.Ordinal) || key.Length > 160)
            throw new InvalidOperationException("M0_AC02_SYNTHETIC_KEY_INVALID");
    }

    public static async Task OpenAsync(string configPath, string key, string statePath)
    {
        ValidateKey(key);
        if (File.Exists(statePath) || Directory.Exists(statePath) || File.Exists(statePath + ".tmp"))
            throw new InvalidOperationException("M0_AC02_FIXTURE_STATE_EXISTS");
        var admin = Admin(configPath);
        await using var connection = new SqlConnection(admin.ConnectionString);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        bool previousMaintenance;
        await using (var read = new SqlCommand("SELECT Maintenance FROM m0.SecurityState WITH (UPDLOCK,HOLDLOCK) WHERE Id=1;", connection, transaction))
            previousMaintenance = (bool)(await read.ExecuteScalarAsync() ?? throw new InvalidOperationException("M0_AC02_FIXTURE_STATE_INVALID"));
        var temporary = statePath + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(new FixtureWindow(key, previousMaintenance)) + "\n");
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(temporary, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            File.Move(temporary, statePath);
            await using var update = new SqlCommand("UPDATE m0.SecurityState SET Maintenance=0 WHERE Id=1;", connection, transaction);
            await update.ExecuteNonQueryAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            try { await transaction.RollbackAsync(); } catch { }
            File.Delete(temporary);
            File.Delete(statePath);
            throw;
        }
        Console.WriteLine("PASS M0-AC02 opened only the dedicated fixture Maintenance window and saved its prior value");
    }

    public static async Task VerifyAsync(string configPath, string key, string cartId, string wrongTokenKey, string wrongOriginKey)
    {
        ValidateKey(key);
        ValidateKey(wrongTokenKey);
        ValidateKey(wrongOriginKey);
        var admin = Admin(configPath);
        await using var connection = new SqlConnection(admin.ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(@"
SELECT COUNT(*),MIN(Actor),MIN(JSON_VALUE(Response,'$.id'))
FROM m0commerce.Idempotency
WHERE Dataset='demo_m0' AND Scope='createCart' AND OperationKey=@key;", connection);
        command.Parameters.AddWithValue("@key", key);
        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();
        var count = reader.GetInt32(0);
        var actor = reader.IsDBNull(1) ? null : reader.GetString(1);
        var recordedCartId = reader.IsDBNull(2) ? null : reader.GetString(2);
        await reader.CloseAsync();
        if (count != 1 || actor is null || recordedCartId != cartId)
            throw new InvalidOperationException("M0_AC02_IDEMPOTENCY_ROW_MISMATCH");

        await using var cart = new SqlCommand(@"
SELECT COUNT(*) FROM m0commerce.Documents
WHERE Dataset='demo_m0' AND Kind='Cart' AND Id=@id AND OwnerId=@actor;
SELECT (SELECT COUNT(*) FROM m0commerce.Idempotency WHERE Dataset='demo_m0' AND Scope='createCart' AND OperationKey=@wrongTokenKey)
     + (SELECT COUNT(*) FROM m0commerce.Idempotency WHERE Dataset='demo_m0' AND Scope='createCart' AND OperationKey=@wrongOriginKey);", connection);
        cart.Parameters.AddWithValue("@id", cartId);
        cart.Parameters.AddWithValue("@actor", actor);
        cart.Parameters.AddWithValue("@wrongTokenKey", wrongTokenKey);
        cart.Parameters.AddWithValue("@wrongOriginKey", wrongOriginKey);
        await using var cartReader = await cart.ExecuteReaderAsync();
        await cartReader.ReadAsync();
        var cartCount = cartReader.GetInt32(0);
        await cartReader.NextResultAsync();
        await cartReader.ReadAsync();
        var rejectedWrites = cartReader.GetInt32(0);
        if (cartCount != 1 || rejectedWrites != 0)
            throw new InvalidOperationException("M0_AC02_SQL_ASSERTION_FAILED");
        Console.WriteLine("PASS M0-AC02 one idempotency record/cart; rejected Origin/CSRF requests left no idempotency rows");
    }

    public static async Task CleanupAsync(string configPath, string key, string statePath)
    {
        ValidateKey(key);
        bool? previousMaintenance = null;
        if (File.Exists(statePath))
        {
            var window = JsonSerializer.Deserialize<FixtureWindow>(await File.ReadAllTextAsync(statePath))
                ?? throw new InvalidOperationException("M0_AC02_FIXTURE_STATE_INVALID");
            if (window.Key != key) throw new InvalidOperationException("M0_AC02_FIXTURE_STATE_KEY_MISMATCH");
            previousMaintenance = window.PreviousMaintenance;
        }
        var admin = Admin(configPath);
        await using var connection = new SqlConnection(admin.ConnectionString);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        int removedCarts;
        int removedKeys;
        await using (var cleanup = new SqlCommand(@"
DECLARE @cart TABLE(Actor varchar(150) NOT NULL,CartId varchar(100) NOT NULL);
INSERT @cart SELECT Actor,JSON_VALUE(Response,'$.id') FROM m0commerce.Idempotency
WHERE Dataset='demo_m0' AND Scope='createCart' AND OperationKey=@key;
DELETE d FROM m0commerce.Documents d JOIN @cart c ON d.Dataset='demo_m0' AND d.Kind='Cart' AND d.Id=c.CartId AND d.OwnerId=c.Actor;
DECLARE @removedCarts int=@@ROWCOUNT;
DELETE FROM m0commerce.Idempotency WHERE Dataset='demo_m0' AND Scope='createCart' AND OperationKey=@key;
DECLARE @removedKeys int=@@ROWCOUNT;
IF @restoreMaintenance IS NOT NULL UPDATE m0.SecurityState SET Maintenance=@restoreMaintenance WHERE Id=1;
SELECT @removedCarts,@removedKeys;", connection, transaction))
        {
            cleanup.Parameters.AddWithValue("@key", key);
            cleanup.Parameters.AddWithValue("@restoreMaintenance", (object?)previousMaintenance ?? DBNull.Value);
            await using var reader = await cleanup.ExecuteReaderAsync();
            await reader.ReadAsync();
            removedCarts = reader.GetInt32(0);
            removedKeys = reader.GetInt32(1);
        }
        await transaction.CommitAsync();
        if (removedCarts != removedKeys) throw new InvalidOperationException("M0_AC02_SYNTHETIC_CLEANUP_FAILED");
        await using var verify = new SqlCommand("SELECT (SELECT COUNT(*) FROM m0commerce.Idempotency WHERE Dataset='demo_m0' AND Scope='createCart' AND OperationKey=@key),(SELECT Maintenance FROM m0.SecurityState WHERE Id=1);", connection);
        verify.Parameters.AddWithValue("@key", key);
        await using var verification = await verify.ExecuteReaderAsync();
        await verification.ReadAsync();
        if (verification.GetInt32(0) != 0 || (previousMaintenance is not null && verification.GetBoolean(1) != previousMaintenance.Value))
            throw new InvalidOperationException("M0_AC02_SYNTHETIC_CLEANUP_FAILED");
        if (File.Exists(statePath)) File.Delete(statePath);
        Console.WriteLine("PASS M0-AC02 removed only the runner-owned synthetic cart/idempotency row");
    }
}
