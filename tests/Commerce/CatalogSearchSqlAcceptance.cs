using System.Text.Json;
using Kohami.Core;
using Microsoft.Data.SqlClient;

internal static class CatalogSearchSqlAcceptance
{
    private sealed record StockState(string SkuId, int OriginalStock);

    public static async Task MakeOneShowcaseSkuUnavailableAsync(string configPath, string statePath)
    {
        var (settings, connectionString) = Admin(configPath);
        if (File.Exists(statePath)) throw new InvalidOperationException("ISSUE10_STOCK_STATE_ALREADY_EXISTS");
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var tx = (SqlTransaction)await connection.BeginTransactionAsync();
        await using var select = new SqlCommand("""
            SELECT TOP(1) i.Sku,i.Stock
            FROM commerce.Inventory i JOIN identity_access.CatalogProductSkus s ON s.Dataset=i.Dataset AND s.SkuId=i.Sku
            WHERE i.Dataset='kohami_local' AND i.Active=1 AND i.Stock>0 AND s.PublicIncluded=1 AND s.PublicActive=1
              AND s.SkuId LIKE 'demo-%'
            ORDER BY s.SkuId
            """, connection, tx);
        await using var reader = await select.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) throw new InvalidOperationException("ISSUE10_SHOWCASE_SKU_NOT_FOUND");
        var state = new StockState(reader.GetString(0), reader.GetInt32(1));
        await reader.CloseAsync();
        await using var update = new SqlCommand("UPDATE commerce.Inventory SET Stock=0 WHERE Dataset='kohami_local' AND Sku=@sku AND Stock=@stock AND Active=1", connection, tx);
        update.Parameters.AddWithValue("@sku", state.SkuId);
        update.Parameters.AddWithValue("@stock", state.OriginalStock);
        if (await update.ExecuteNonQueryAsync() != 1) throw new InvalidOperationException("ISSUE10_STOCK_UPDATE_FAILED");
        await tx.CommitAsync();
        await File.WriteAllTextAsync(statePath, JsonSerializer.Serialize(state));
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(statePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        Console.WriteLine("PASS one showcase SKU is temporarily out of stock for Issue10 acceptance");
    }

    public static async Task RestoreShowcaseStockAsync(string configPath, string statePath)
    {
        var (settings, connectionString) = Admin(configPath);
        _ = settings;
        var state = JsonSerializer.Deserialize<StockState>(await File.ReadAllTextAsync(statePath))
            ?? throw new InvalidOperationException("ISSUE10_STOCK_STATE_INVALID");
        if (!state.SkuId.StartsWith("demo-", StringComparison.Ordinal) || state.OriginalStock is < 1 or > 100_000)
            throw new InvalidOperationException("ISSUE10_STOCK_STATE_INVALID");
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var update = new SqlCommand("UPDATE commerce.Inventory SET Stock=@stock WHERE Dataset='kohami_local' AND Sku=@sku AND Stock=0 AND Active=1", connection);
        update.Parameters.AddWithValue("@sku", state.SkuId);
        update.Parameters.AddWithValue("@stock", state.OriginalStock);
        if (await update.ExecuteNonQueryAsync() != 1) throw new InvalidOperationException("ISSUE10_STOCK_RESTORE_FAILED");
        File.Delete(statePath);
        Console.WriteLine("PASS showcase SKU stock restored after Issue10 acceptance");
    }

    private static (RuntimeSettings Settings, string ConnectionString) Admin(string configPath)
    {
        using var raw = JsonDocument.Parse(File.ReadAllText(configPath));
        if (!raw.RootElement.TryGetProperty("IdentitySqlTestDedicated", out var dedicated) || !dedicated.GetBoolean())
            throw new InvalidOperationException("DEDICATED_SQL_FIXTURE_REQUIRED");
        var settings = RuntimeSettings.Load(configPath);
        var builder = new SqlConnectionStringBuilder(raw.RootElement.GetProperty("MigratorConnectionString").GetString())
        {
            Encrypt = SqlConnectionEncryptOption.Strict,
            TrustServerCertificate = false,
            HostNameInCertificate = settings.SqlServerName
        };
        builder["Server Certificate"] = settings.SqlCertificatePath;
        if (builder.InitialCatalog != "Kohami") throw new InvalidOperationException("ISSUE10_SQL_SCOPE_INVALID");
        return (settings, builder.ConnectionString);
    }
}
