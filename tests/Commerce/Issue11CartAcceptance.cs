using System.Text.Json;
using System.Text.RegularExpressions;
using Kohami.Core;
using Microsoft.Data.SqlClient;

internal static class Issue11CartAcceptance
{
    private sealed record CartEvidence(string CartId);
    private sealed record InventoryState(Guid OperationId, string SkuId, string Change, long OriginalPrice, int OriginalStock, bool OriginalActive, long ChangedPrice, int ChangedStock, bool ChangedActive);

    public static async Task VerifyFixtureAsync(string configPath)
    {
        var (_, connectionString) = DedicatedAdmin(configPath);
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await VerifyFixtureMarkerAsync(connection);
        Console.WriteLine("PASS Issue11 SQL target is marker-verified canonical kohami-sql-test");
        await using var maintenance = new SqlCommand("SELECT Maintenance FROM identity_access.SecurityState WHERE Id=1", connection);
        var maintenanceValue = await maintenance.ExecuteScalarAsync();
        if (maintenanceValue is null or DBNull) throw new InvalidOperationException("ISSUE11_MAINTENANCE_STATE_MISSING");
        var required = Convert.ToBoolean(maintenanceValue);
        Console.WriteLine(required ? "ISSUE11_MAINTENANCE_REQUIRED" : "ISSUE11_MAINTENANCE_DISABLED");
    }

    public static async Task ChangeInventoryAsync(string configPath, string skuId, string change, string statePath)
    {
        if (!Regex.IsMatch(skuId, "^demo-[A-Za-z0-9_-]{1,90}$") || change is not ("price" or "stock" or "unavailable"))
            throw new InvalidOperationException("ISSUE11_INVENTORY_CHANGE_INVALID");
        var stateFile = new FileInfo(statePath);
        if (stateFile.Exists || stateFile.LinkTarget is not null || Path.GetDirectoryName(stateFile.FullName) != Path.GetDirectoryName(Path.GetFullPath(configPath)))
            throw new InvalidOperationException("ISSUE11_INVENTORY_STATE_PATH_INVALID");
        var (settings, connectionString) = DedicatedAdmin(configPath);
        _ = settings;
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await VerifyFixtureMarkerAsync(connection);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        await using var read = new SqlCommand("""
            SELECT i.Price,i.Stock,i.Active
            FROM commerce.Inventory i JOIN identity_access.CatalogProductSkus s ON s.Dataset=i.Dataset AND s.SkuId=i.Sku
            WHERE i.Dataset='kohami_local' AND i.Sku=@sku AND i.Active=1 AND s.PublicIncluded=1 AND s.PublicActive=1
            """, connection, transaction);
        read.Parameters.AddWithValue("@sku", skuId);
        await using var reader = await read.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) throw new InvalidOperationException("ISSUE11_PUBLIC_SYNTHETIC_SKU_REQUIRED");
        var originalPrice = reader.GetInt64(0);
        var originalStock = reader.GetInt32(1);
        var originalActive = reader.GetBoolean(2);
        await reader.CloseAsync();
        if (change == "stock" && originalStock < 2) throw new InvalidOperationException("ISSUE11_STOCK_FIXTURE_TOO_LOW");
        var state = new InventoryState(Guid.NewGuid(), skuId, change, originalPrice, originalStock, originalActive,
            change == "price" ? checked(originalPrice + 1) : originalPrice,
            change == "stock" ? 1 : originalStock,
            change == "unavailable" ? false : originalActive);
        await File.WriteAllTextAsync(statePath, JsonSerializer.Serialize(state) + "\n");
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(statePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        try
        {
            await using var update = new SqlCommand("""
                UPDATE commerce.Inventory SET Price=@changedPrice,Stock=@changedStock,Active=@changedActive
                WHERE Dataset='kohami_local' AND Sku=@sku AND Price=@originalPrice AND Stock=@originalStock AND Active=@originalActive
                """, connection, transaction);
            update.Parameters.AddWithValue("@sku", skuId);
            update.Parameters.AddWithValue("@originalPrice", originalPrice);
            update.Parameters.AddWithValue("@originalStock", originalStock);
            update.Parameters.AddWithValue("@originalActive", originalActive);
            update.Parameters.AddWithValue("@changedPrice", state.ChangedPrice);
            update.Parameters.AddWithValue("@changedStock", state.ChangedStock);
            update.Parameters.AddWithValue("@changedActive", state.ChangedActive);
            if (await update.ExecuteNonQueryAsync() != 1) throw new InvalidOperationException("ISSUE11_INVENTORY_CHANGE_CONFLICT");
            await VerifyInventoryAsync(connection, transaction, state.SkuId, state.ChangedPrice, state.ChangedStock, state.ChangedActive, "ISSUE11_INVENTORY_CHANGE_READBACK_FAILED");
            await transaction.CommitAsync();
        }
        catch
        {
            try { await transaction.RollbackAsync(); } catch { }
            File.Delete(statePath);
            throw;
        }
        Console.WriteLine($"PASS Issue11 synthetic SKU snapshot/change recorded operation={state.OperationId:N}");
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
        var expected = state.Change switch
        {
            "price" => state.ChangedPrice == state.OriginalPrice + 1 && state.ChangedStock == state.OriginalStock && state.ChangedActive == state.OriginalActive,
            "stock" => state.ChangedPrice == state.OriginalPrice && state.ChangedStock == 1 && state.ChangedActive == state.OriginalActive,
            "unavailable" => state.ChangedPrice == state.OriginalPrice && state.ChangedStock == state.OriginalStock && !state.ChangedActive && state.OriginalActive,
            _ => false,
        };
        if (state.OperationId == Guid.Empty || !Regex.IsMatch(state.SkuId, "^demo-[A-Za-z0-9_-]{1,90}$") || state.OriginalPrice < 1 || state.OriginalStock is < 1 or > 100_000 || !state.OriginalActive || !expected)
            throw new InvalidOperationException("ISSUE11_INVENTORY_STATE_INVALID");
        var (_, connectionString) = DedicatedAdmin(configPath);
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await VerifyFixtureMarkerAsync(connection);
        await using var update = new SqlCommand("""
            UPDATE commerce.Inventory SET Price=@price,Stock=@stock,Active=@active
            WHERE Dataset='kohami_local' AND Sku=@sku AND Price=@changedPrice AND Stock=@changedStock AND Active=@changedActive
            """, connection);
        update.Parameters.AddWithValue("@sku", state.SkuId);
        update.Parameters.AddWithValue("@price", state.OriginalPrice);
        update.Parameters.AddWithValue("@stock", state.OriginalStock);
        update.Parameters.AddWithValue("@active", state.OriginalActive);
        update.Parameters.AddWithValue("@changedPrice", state.ChangedPrice);
        update.Parameters.AddWithValue("@changedStock", state.ChangedStock);
        update.Parameters.AddWithValue("@changedActive", state.ChangedActive);
        if (await update.ExecuteNonQueryAsync() != 1) throw new InvalidOperationException("ISSUE11_INVENTORY_RESTORE_CONFLICT");
        await VerifyInventoryAsync(connection, null, state.SkuId, state.OriginalPrice, state.OriginalStock, state.OriginalActive, "ISSUE11_INVENTORY_RESTORE_READBACK_FAILED");
        File.Delete(statePath);
        Console.WriteLine($"PASS Issue11 synthetic SKU operation={state.OperationId:N} restored and verified by direct SQL readback");
    }

    public static async Task CleanupAsync(string configPath, string evidencePath)
    {
        var evidence = new FileInfo(evidencePath);
        if (!evidence.Exists) return;
        if (evidence.LinkTarget is not null || Path.GetDirectoryName(evidence.FullName) != Path.GetDirectoryName(Path.GetFullPath(configPath))
            || (!OperatingSystem.IsWindows() && (evidence.UnixFileMode & (UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.OtherRead | UnixFileMode.OtherWrite)) != 0))
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

        var (_, connectionString) = DedicatedAdmin(configPath);

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await VerifyFixtureMarkerAsync(connection);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        foreach (var cartId in cartIds)
        {
            await using var owner = new SqlCommand("SELECT OwnerId FROM commerce.Documents WHERE Dataset='kohami_local' AND Kind='Cart' AND Id=@cart", connection, transaction);
            owner.Parameters.AddWithValue("@cart", cartId);
            var actor = await owner.ExecuteScalarAsync() as string ?? throw new InvalidOperationException("ISSUE11_CART_CLEANUP_TARGET_NOT_FOUND");
            if (!actor.StartsWith("browser_", StringComparison.Ordinal)) throw new InvalidOperationException("ISSUE11_CART_OWNER_INVALID");

            await using var cleanup = new SqlCommand("""
                DELETE FROM commerce.Idempotency WHERE Dataset='kohami_local' AND Actor=@actor AND
                  ((Scope='createCart' AND JSON_VALUE(Response,'$.id')=@cart)
                   OR Scope IN ('createQuote/'+@cart)
                   OR Scope LIKE 'setCartItem/'+@cart+'/%'
                   OR Scope LIKE 'removeCartItem/'+@cart+'/%'
                   OR Scope='setCartPromotion/'+@cart);
                DELETE FROM commerce.Documents WHERE Dataset='kohami_local' AND Kind='Quote' AND OwnerId=@actor AND JSON_VALUE(Body,'$.cartId')=@cart;
                DELETE FROM commerce.Documents WHERE Dataset='kohami_local' AND Kind='Cart' AND Id=@cart AND OwnerId=@actor;
                """, connection, transaction);
            cleanup.Parameters.AddWithValue("@actor", actor);
            cleanup.Parameters.AddWithValue("@cart", cartId);
            if (await cleanup.ExecuteNonQueryAsync() < 1) throw new InvalidOperationException("ISSUE11_CART_CLEANUP_FAILED");
            await using var verify = new SqlCommand("""
                SELECT
                  (SELECT COUNT(*) FROM commerce.Documents WHERE Dataset='kohami_local' AND Kind='Cart' AND Id=@cart AND OwnerId=@actor)
                  +(SELECT COUNT(*) FROM commerce.Documents WHERE Dataset='kohami_local' AND Kind='Quote' AND OwnerId=@actor AND JSON_VALUE(Body,'$.cartId')=@cart)
                  +(SELECT COUNT(*) FROM commerce.Idempotency WHERE Dataset='kohami_local' AND Actor=@actor AND
                    ((Scope='createCart' AND JSON_VALUE(Response,'$.id')=@cart)
                     OR Scope='createQuote/'+@cart OR Scope LIKE 'setCartItem/'+@cart+'/%'
                     OR Scope LIKE 'removeCartItem/'+@cart+'/%' OR Scope='setCartPromotion/'+@cart))
                """, connection, transaction);
            verify.Parameters.AddWithValue("@actor", actor);
            verify.Parameters.AddWithValue("@cart", cartId);
            if (Convert.ToInt32(await verify.ExecuteScalarAsync()) != 0) throw new InvalidOperationException("ISSUE11_CART_CLEANUP_READBACK_FAILED");
        }
        await transaction.CommitAsync();
        File.Delete(evidencePath);
        Console.WriteLine($"PASS removed only {cartIds.Length} runner-recorded Issue11 carts and verified related Quote/idempotency cleanup by SQL readback");
    }

    private static (RuntimeSettings Settings, string ConnectionString) DedicatedAdmin(string configPath)
    {
        using var config = JsonDocument.Parse(File.ReadAllText(configPath));
        var root = config.RootElement;
        RequireCanonicalProfilePath(configPath);
        if (!root.TryGetProperty("IdentitySqlTestDedicated", out var dedicated) || !dedicated.GetBoolean()
            || !root.TryGetProperty("KohamiSqlTestDedicated", out var canonical) || !canonical.GetBoolean()
            || !root.TryGetProperty("port", out var configuredPort) || configuredPort.GetInt32() != 14333
            || root.GetProperty("datasetId").GetString() != "kohami_local") throw new InvalidOperationException("CANONICAL_KOHAMI_SQL_TEST_REQUIRED");
        var settings = RuntimeSettings.Load(configPath);
        if (settings.SqlServerName != "localhost" || settings.Origin != "https://localhost:3000") throw new InvalidOperationException("ISSUE11_PROFILE_IDENTITY_INVALID");
        RequireCanonicalConnection(root.GetProperty("connectionString").GetString() ?? throw new InvalidOperationException("ISSUE11_API_CONNECTION_MISSING"), settings.SqlCertificatePath);
        RequireCanonicalConnection(root.GetProperty("workerConnectionString").GetString() ?? throw new InvalidOperationException("ISSUE11_WORKER_CONNECTION_MISSING"), settings.SqlCertificatePath);
        var builder = new SqlConnectionStringBuilder(root.GetProperty("MigratorConnectionString").GetString())
        {
            Encrypt = SqlConnectionEncryptOption.Strict,
            TrustServerCertificate = false,
            HostNameInCertificate = settings.SqlServerName
        };
        builder["Server Certificate"] = settings.SqlCertificatePath;
        RequireCanonicalConnection(builder.ConnectionString, settings.SqlCertificatePath);
        return (settings, builder.ConnectionString);
    }

    private static void RequireCanonicalProfilePath(string configPath)
    {
        var fullPath = Path.GetFullPath(configPath);
        var directory = new DirectoryInfo(Path.GetDirectoryName(fullPath)!);
        if (Path.GetFileName(fullPath) != "fixture-private.json" || directory.Name != "identity"
            || directory.Parent?.Name != "kohami-sql-test" || directory.Parent.Parent?.Name != ".local")
            throw new InvalidOperationException("ISSUE11_CANONICAL_PROFILE_PATH_REQUIRED");
        var current = new DirectoryInfo(Path.GetPathRoot(fullPath)!);
        foreach (var segment in fullPath[Path.GetPathRoot(fullPath)!.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = new DirectoryInfo(Path.Combine(current.FullName, segment));
            if (current.LinkTarget is not null) throw new InvalidOperationException("ISSUE11_PROFILE_SYMLINK_REFUSED");
        }
    }

    private static void RequireCanonicalConnection(string connectionString, string certificatePath)
    {
        var connection = new SqlConnectionStringBuilder(connectionString);
        if (!string.Equals(connection.DataSource, "localhost,14333", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(connection.InitialCatalog, "Kohami", StringComparison.OrdinalIgnoreCase)
            || connection.Encrypt != SqlConnectionEncryptOption.Strict || connection.TrustServerCertificate
            || !string.Equals(connection.HostNameInCertificate, "localhost", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(connection["Server Certificate"]?.ToString(), certificatePath, StringComparison.Ordinal))
            throw new InvalidOperationException("ISSUE11_SQL_SCOPE_INVALID");
    }

    private static async Task VerifyFixtureMarkerAsync(SqlConnection connection)
    {
        await using var marker = new SqlCommand("""
            DECLARE @fp char(64)=LOWER(CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CONVERT(nvarchar(100),SERVERPROPERTY('ServerName')),'|Kohami|kohami_local')),2));
            SELECT CASE WHEN DB_NAME()='Kohami' AND
              (SELECT COUNT(*) FROM ops.DevelopmentFixtureMarker WHERE DatasetId='kohami_local' AND InstanceFingerprint=@fp)=1
              THEN 1 ELSE 0 END
            """, connection);
        if (Convert.ToInt32(await marker.ExecuteScalarAsync()) != 1) throw new InvalidOperationException("ISSUE11_DEVELOPMENT_FIXTURE_MARKER_MISMATCH");
    }

    private static async Task VerifyInventoryAsync(SqlConnection connection, SqlTransaction? transaction, string skuId,
        long expectedPrice, int expectedStock, bool expectedActive, string error)
    {
        await using var readback = new SqlCommand("SELECT Price,Stock,Active FROM commerce.Inventory WHERE Dataset='kohami_local' AND Sku=@sku", connection, transaction);
        readback.Parameters.AddWithValue("@sku", skuId);
        await using var reader = await readback.ExecuteReaderAsync();
        if (!await reader.ReadAsync() || reader.GetInt64(0) != expectedPrice || reader.GetInt32(1) != expectedStock || reader.GetBoolean(2) != expectedActive)
            throw new InvalidOperationException(error);
    }
}
