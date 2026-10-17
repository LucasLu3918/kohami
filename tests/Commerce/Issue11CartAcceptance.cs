using System.Text.Json;
using System.Text.RegularExpressions;
using Kohami.M0.Core;
using Microsoft.Data.SqlClient;

internal static class Issue11CartAcceptance
{
    private sealed record CartEvidence(string CartId);
    private sealed record InventoryState(string SkuId, long OriginalPrice, int OriginalStock, bool OriginalActive, long ChangedPrice, int ChangedStock);

    public static async Task ChangeInventoryAsync(string configPath, string skuId, string change, string statePath)
    {
        if (!Regex.IsMatch(skuId, "^demo-[A-Za-z0-9_-]{1,90}$") || change is not ("price" or "stock"))
            throw new InvalidOperationException("ISSUE11_INVENTORY_CHANGE_INVALID");
        var stateFile = new FileInfo(statePath);
        if (stateFile.Exists || stateFile.LinkTarget is not null || Path.GetDirectoryName(stateFile.FullName) != Path.GetDirectoryName(Path.GetFullPath(configPath)))
            throw new InvalidOperationException("ISSUE11_INVENTORY_STATE_PATH_INVALID");
        var (settings, connectionString) = DedicatedAdmin(configPath);
        _ = settings;
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        await using var read = new SqlCommand("""
            SELECT i.Price,i.Stock,i.Active
            FROM m0commerce.Inventory i JOIN m0.CatalogProductSkus s ON s.Dataset=i.Dataset AND s.SkuId=i.Sku
            WHERE i.Dataset='demo_m0' AND i.Sku=@sku AND i.Active=1 AND s.PublicIncluded=1 AND s.PublicActive=1
            """, connection, transaction);
        read.Parameters.AddWithValue("@sku", skuId);
        await using var reader = await read.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) throw new InvalidOperationException("ISSUE11_PUBLIC_SYNTHETIC_SKU_REQUIRED");
        var originalPrice = reader.GetInt64(0);
        var originalStock = reader.GetInt32(1);
        var originalActive = reader.GetBoolean(2);
        await reader.CloseAsync();
        if (change == "stock" && originalStock < 2) throw new InvalidOperationException("ISSUE11_STOCK_FIXTURE_TOO_LOW");
        var state = new InventoryState(skuId, originalPrice, originalStock, originalActive,
            change == "price" ? checked(originalPrice + 1) : originalPrice,
            change == "stock" ? 1 : originalStock);
        await File.WriteAllTextAsync(statePath, JsonSerializer.Serialize(state) + "\n");
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(statePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        try
        {
            var sql = change == "price"
                ? "UPDATE m0commerce.Inventory SET Price=@changed WHERE Dataset='demo_m0' AND Sku=@sku AND Price=@originalPrice AND Stock=@originalStock AND Active=1"
                : "UPDATE m0commerce.Inventory SET Stock=@changed WHERE Dataset='demo_m0' AND Sku=@sku AND Price=@originalPrice AND Stock=@originalStock AND Active=1";
            await using var update = new SqlCommand(sql, connection, transaction);
            update.Parameters.AddWithValue("@sku", skuId);
            update.Parameters.AddWithValue("@originalPrice", originalPrice);
            update.Parameters.AddWithValue("@originalStock", originalStock);
            update.Parameters.AddWithValue("@changed", change == "price" ? state.ChangedPrice : state.ChangedStock);
            if (await update.ExecuteNonQueryAsync() != 1) throw new InvalidOperationException("ISSUE11_INVENTORY_CHANGE_CONFLICT");
            await transaction.CommitAsync();
        }
        catch
        {
            try { await transaction.RollbackAsync(); } catch { }
            File.Delete(statePath);
            throw;
        }
        Console.WriteLine("PASS Issue11 one public synthetic SKU temporarily changed for stale quote acceptance");
    }

    public static async Task RestoreInventoryAsync(string configPath, string statePath)
    {
        var stateFile = new FileInfo(statePath);
        if (!stateFile.Exists) return;
        if (stateFile.LinkTarget is not null || Path.GetDirectoryName(stateFile.FullName) != Path.GetDirectoryName(Path.GetFullPath(configPath))
            || (!OperatingSystem.IsWindows() && (stateFile.UnixFileMode & (UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.OtherRead | UnixFileMode.OtherWrite)) != 0))
            throw new InvalidOperationException("ISSUE11_INVENTORY_STATE_PATH_UNSAFE");
        var state = JsonSerializer.Deserialize<InventoryState>(await File.ReadAllTextAsync(statePath))
            ?? throw new InvalidOperationException("ISSUE11_INVENTORY_STATE_INVALID");
        if (!Regex.IsMatch(state.SkuId, "^demo-[A-Za-z0-9_-]{1,90}$") || state.OriginalPrice < 1 || state.OriginalStock is < 2 or > 100_000
            || state.ChangedPrice != state.OriginalPrice + 1 && state.ChangedStock != 1)
            throw new InvalidOperationException("ISSUE11_INVENTORY_STATE_INVALID");
        var (_, connectionString) = DedicatedAdmin(configPath);
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var update = new SqlCommand("""
            UPDATE m0commerce.Inventory SET Price=@price,Stock=@stock,Active=@active
            WHERE Dataset='demo_m0' AND Sku=@sku AND Price=@changedPrice AND Stock=@changedStock AND Active=@active
            """, connection);
        update.Parameters.AddWithValue("@sku", state.SkuId);
        update.Parameters.AddWithValue("@price", state.OriginalPrice);
        update.Parameters.AddWithValue("@stock", state.OriginalStock);
        update.Parameters.AddWithValue("@active", state.OriginalActive);
        update.Parameters.AddWithValue("@changedPrice", state.ChangedPrice);
        update.Parameters.AddWithValue("@changedStock", state.ChangedStock);
        if (await update.ExecuteNonQueryAsync() != 1) throw new InvalidOperationException("ISSUE11_INVENTORY_RESTORE_CONFLICT");
        File.Delete(statePath);
        Console.WriteLine("PASS Issue11 temporary synthetic SKU price/stock change restored");
    }

    public static async Task CleanupAsync(string configPath, string evidencePath)
    {
        var evidence = new FileInfo(evidencePath);
        if (!evidence.Exists) return;
        if (evidence.LinkTarget is not null || (!OperatingSystem.IsWindows() && (evidence.UnixFileMode & (UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.OtherRead | UnixFileMode.OtherWrite)) != 0))
            throw new InvalidOperationException("ISSUE11_EVIDENCE_PATH_UNSAFE");
        var lines = (await File.ReadAllLinesAsync(evidencePath)).Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
        var cartIds = lines.Select(line =>
        {
            using var item = JsonDocument.Parse(line);
            var itemEvidence = item.RootElement.Deserialize<CartEvidence>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (itemEvidence is null || !Regex.IsMatch(itemEvidence.CartId, "^cart_[0-9a-f]{32}$"))
                throw new InvalidOperationException("ISSUE11_CART_ID_INVALID");
            return itemEvidence.CartId;
        }).Distinct(StringComparer.Ordinal).ToArray();
        if (cartIds.Length is < 1 or > 10) throw new InvalidOperationException("ISSUE11_CART_EVIDENCE_COUNT_INVALID");

        using var config = JsonDocument.Parse(await File.ReadAllTextAsync(configPath));
        var settings = M0Settings.Load(configPath);
        var configRoot = config.RootElement;
        if (!configRoot.TryGetProperty("IdentitySqlTestDedicated", out var dedicated) || !dedicated.GetBoolean()
            || configRoot.GetProperty("datasetId").GetString() != "demo_m0")
            throw new InvalidOperationException("DEDICATED_ISSUE11_FIXTURE_REQUIRED");
        var builder = new SqlConnectionStringBuilder(configRoot.GetProperty("MigratorConnectionString").GetString())
        {
            Encrypt = SqlConnectionEncryptOption.Strict,
            TrustServerCertificate = false,
            HostNameInCertificate = settings.SqlServerName
        };
        builder["Server Certificate"] = settings.SqlCertificatePath;
        var target = Regex.Match(builder.DataSource, "^localhost,(\\d+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!target.Success || !int.TryParse(target.Groups[1].Value, out var port) || port is < 14336 or > 14399
            || builder.InitialCatalog != "KohamiM0") throw new InvalidOperationException("ISSUE11_SQL_SCOPE_INVALID");

        await using var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        foreach (var cartId in cartIds)
        {
            await using var owner = new SqlCommand("SELECT OwnerId FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='Cart' AND Id=@cart", connection, transaction);
            owner.Parameters.AddWithValue("@cart", cartId);
            var actor = await owner.ExecuteScalarAsync() as string ?? throw new InvalidOperationException("ISSUE11_CART_CLEANUP_TARGET_NOT_FOUND");
            if (!actor.StartsWith("browser_", StringComparison.Ordinal)) throw new InvalidOperationException("ISSUE11_CART_OWNER_INVALID");

            await using var cleanup = new SqlCommand("""
                DELETE FROM m0commerce.Idempotency WHERE Dataset='demo_m0' AND Actor=@actor AND
                  ((Scope='createCart' AND JSON_VALUE(Response,'$.id')=@cart)
                   OR Scope IN ('createQuote/'+@cart)
                   OR Scope LIKE 'setCartItem/'+@cart+'/%'
                   OR Scope LIKE 'removeCartItem/'+@cart+'/%'
                   OR Scope='setCartPromotion/'+@cart);
                DELETE FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='Quote' AND OwnerId=@actor AND JSON_VALUE(Body,'$.cartId')=@cart;
                DELETE FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='Cart' AND Id=@cart AND OwnerId=@actor;
                """, connection, transaction);
            cleanup.Parameters.AddWithValue("@actor", actor);
            cleanup.Parameters.AddWithValue("@cart", cartId);
            if (await cleanup.ExecuteNonQueryAsync() < 1) throw new InvalidOperationException("ISSUE11_CART_CLEANUP_FAILED");
        }
        await transaction.CommitAsync();
        File.Delete(evidencePath);
        Console.WriteLine($"PASS removed only {cartIds.Length} runner-recorded Issue11 Cart rows, related Quote and idempotency rows");
    }

    private static (M0Settings Settings, string ConnectionString) DedicatedAdmin(string configPath)
    {
        using var config = JsonDocument.Parse(File.ReadAllText(configPath));
        var root = config.RootElement;
        if (!root.TryGetProperty("IdentitySqlTestDedicated", out var dedicated) || !dedicated.GetBoolean()
            || root.GetProperty("datasetId").GetString() != "demo_m0") throw new InvalidOperationException("DEDICATED_ISSUE11_FIXTURE_REQUIRED");
        var settings = M0Settings.Load(configPath);
        var builder = new SqlConnectionStringBuilder(root.GetProperty("MigratorConnectionString").GetString())
        {
            Encrypt = SqlConnectionEncryptOption.Strict,
            TrustServerCertificate = false,
            HostNameInCertificate = settings.SqlServerName
        };
        builder["Server Certificate"] = settings.SqlCertificatePath;
        var target = Regex.Match(builder.DataSource, "^localhost,(\\d+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!target.Success || !int.TryParse(target.Groups[1].Value, out var port) || port is < 14336 or > 14399
            || builder.InitialCatalog != "KohamiM0") throw new InvalidOperationException("ISSUE11_SQL_SCOPE_INVALID");
        return (settings, builder.ConnectionString);
    }
}
