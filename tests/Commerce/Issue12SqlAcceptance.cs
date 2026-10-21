using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Kohami.Core;

internal static class Issue12SqlAcceptance
{
    private sealed record InventoryRow(string Sku, long Price, int OriginalStock, bool Created);
    private sealed record FixtureState(InventoryRow[] Rows);
    private sealed record CheckoutRow(string CheckoutId, string OrderId, string SkuId);
    private sealed record Evidence(string[] CartIds, string[] QuoteIds, CheckoutRow[] Checkouts,
        string[] IdempotencyKeys, string RollbackCartId, string RollbackQuoteId, bool Completed);

    public static async Task PrepareAsync(string configPath, string statePath)
    {
        var (settings, connectionString) = DedicatedAdmin(configPath);
        _ = settings;
        RequirePrivatePath(configPath, statePath, mustNotExist: true);
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var tx = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var rows = new[] {
            new InventoryRow("sku_issue12_" + Guid.NewGuid().ToString("N"), 1275, 0, true),
            new InventoryRow("sku_issue12_" + Guid.NewGuid().ToString("N"), 2390, 0, true),
        };
        var state = new FixtureState(rows);
        await File.WriteAllTextAsync(statePath, JsonSerializer.Serialize(state) + "\n");
        SetPrivateFileMode(statePath);
        try
        {
            foreach (var row in rows)
            {
                await using var update = new SqlCommand("INSERT commerce.Inventory(Dataset,Sku,Price,Stock,Active) VALUES('kohami_local',@sku,@price,1,1)", connection, tx);
                update.Parameters.AddWithValue("@sku", row.Sku);
                update.Parameters.AddWithValue("@price", row.Price);
                if (await update.ExecuteNonQueryAsync() != 1) throw new InvalidOperationException("ISSUE12_INVENTORY_PREPARE_FAILED");
            }
            await tx.CommitAsync();
        }
        catch
        {
            try { await tx.RollbackAsync(); } catch { }
            File.Delete(statePath);
            throw;
        }
        Console.WriteLine("PASS Issue12 created two uniquely identified synthetic SKUs with one unit each");
    }

    public static Task SetSecondStockAsync(string configPath, string statePath) => SetStocksAsync(configPath, statePath, secondOnly: true);

    public static async Task SetStocksAsync(string configPath, string statePath)
    {
        await SetStocksAsync(configPath, statePath, secondOnly: false);
    }

    private static async Task SetStocksAsync(string configPath, string statePath, bool secondOnly)
    {
        var (_, connectionString) = DedicatedAdmin(configPath);
        var state = ReadState(configPath, statePath);
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var tx = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        foreach (var row in secondOnly ? state.Rows.Skip(1) : state.Rows)
        {
            await using var guard = new SqlCommand("SELECT COUNT(*) FROM commerce.Reservations WHERE Dataset='kohami_local' AND Sku=@sku", connection, tx);
            guard.Parameters.AddWithValue("@sku", row.Sku);
            if (Convert.ToInt32(await guard.ExecuteScalarAsync()) != 0) throw new InvalidOperationException("ISSUE12_STOCK_RESET_HAS_RESERVATIONS");
            await using var update = new SqlCommand("UPDATE commerce.Inventory SET Stock=@stock WHERE Dataset='kohami_local' AND Sku=@sku AND Price=@price AND Active=1 AND Stock IN (0,1)", connection, tx);
            update.Parameters.AddWithValue("@stock", secondOnly ? 0 : 1);
            update.Parameters.AddWithValue("@sku", row.Sku);
            update.Parameters.AddWithValue("@price", row.Price);
            if (await update.ExecuteNonQueryAsync() != 1) throw new InvalidOperationException("ISSUE12_INVENTORY_RESET_CONFLICT");
        }
        await tx.CommitAsync();
        Console.WriteLine(secondOnly ? "PASS Issue12 forced only the second SKU unavailable after quote creation" : "PASS Issue12 temporary SKUs restored to one available unit for remaining API cases");
    }

    public static async Task VerifyRollbackAsync(string configPath, string statePath, string evidencePath)
    {
        var (_, connectionString) = DedicatedAdmin(configPath);
        var state = ReadState(configPath, statePath);
        var evidence = ReadEvidence(configPath, evidencePath);
        if (!Regex.IsMatch(evidence.RollbackCartId, "^cart_[0-9a-f]{32}$") || !Regex.IsMatch(evidence.RollbackQuoteId, "^quote_[0-9a-f]{32}$"))
            throw new InvalidOperationException("ISSUE12_ROLLBACK_EVIDENCE_INVALID");
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var tx = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var first = await ReadStock(connection, tx, state.Rows[0]);
        var second = await ReadStock(connection, tx, state.Rows[1]);
        var reservations = await CountReservations(connection, tx, state.Rows.Select(x => x.Sku));
        using var cart = await ReadBody(connection, tx, "Cart", evidence.RollbackCartId);
        using var quote = await ReadBody(connection, tx, "Quote", evidence.RollbackQuoteId);
        if (first != 1 || second != 0 || reservations != 0 || quote.RootElement.GetProperty("used").GetBoolean()
            || quote.RootElement.GetProperty("snapshot").GetProperty("lines").GetArrayLength() != 2
            || cart.RootElement.GetProperty("items").GetArrayLength() != 2)
            throw new InvalidOperationException("ISSUE12_MULTISKU_FAILURE_DID_NOT_ROLL_BACK");
        await tx.CommitAsync();
        Console.WriteLine("PASS public multi-SKU checkout conflict rolled back the first stock decrement and left no reservation");
    }

    public static async Task CleanupAsync(string configPath, string statePath, string evidencePath)
    {
        var (_, connectionString) = DedicatedAdmin(configPath);
        var state = ReadState(configPath, statePath);
        var evidence = ReadEvidence(configPath, evidencePath);
        ValidateEvidence(evidence);
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var tx = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var checkouts = await RecoverRecordedCheckouts(connection, tx, evidence);
        string? verificationFailure = null;
        if (evidence.Completed)
        {
            try
            {
                if (checkouts.Length != 2 || checkouts.Select(x => x.SkuId).Distinct(StringComparer.Ordinal).Count() != 2)
                    throw new InvalidOperationException("ISSUE12_SUCCESSFUL_CHECKOUT_EVIDENCE_INCOMPLETE");
                foreach (var checkout in checkouts)
                {
                    var checkoutBody = await ReadBody(connection, tx, "Checkout", checkout.CheckoutId);
                    using (checkoutBody)
                    {
                        var orderBody = await ReadBody(connection, tx, "Order", checkout.OrderId);
                        using (orderBody)
                        {
                            if (checkoutBody.RootElement.GetProperty("paymentState").GetString() != "Created"
                                || checkoutBody.RootElement.GetProperty("orderState").GetString() != "AwaitingPayment"
                                || checkoutBody.RootElement.GetProperty("mode").GetString() != "mock"
                                || orderBody.RootElement.GetProperty("orderState").GetString() != "AwaitingPayment")
                                throw new InvalidOperationException("ISSUE12_CHECKOUT_SQL_STATE_INVALID");
                        }
                    }
                    var held = await CountHeldReservation(connection, tx, checkout.OrderId, checkout.SkuId);
                    var stock = await ReadStock(connection, tx, state.Rows.Single(x => x.Sku == checkout.SkuId));
                    if (held != 1 || stock != 0) throw new InvalidOperationException("ISSUE12_CHECKOUT_RESERVATION_SQL_INVALID");
                }
            }
            catch (Exception error)
            {
                verificationFailure = error.Message;
            }
        }

        foreach (var checkout in checkouts)
        {
            await using var removeReservation = new SqlCommand("DELETE commerce.Reservations WHERE Dataset='kohami_local' AND OrderId=@order", connection, tx);
            removeReservation.Parameters.AddWithValue("@order", checkout.OrderId);
            await removeReservation.ExecuteNonQueryAsync();
        }
        foreach (var checkout in checkouts)
        {
            await DeleteDocument(connection, tx, "Checkout", checkout.CheckoutId);
            await DeleteDocument(connection, tx, "Order", checkout.OrderId);
        }
        foreach (var quote in evidence.QuoteIds) await DeleteDocument(connection, tx, "Quote", quote);
        foreach (var cart in evidence.CartIds)
        {
            await DeleteDocument(connection, tx, "Cart", cart);
        }
        foreach (var key in evidence.IdempotencyKeys)
        {
            await using var removeKey = new SqlCommand("DELETE commerce.Idempotency WHERE Dataset='kohami_local' AND OperationKey=@key", connection, tx);
            removeKey.Parameters.AddWithValue("@key", key);
            await removeKey.ExecuteNonQueryAsync();
        }
        foreach (var row in state.Rows)
        {
            if (await CountReservations(connection, tx, [row.Sku]) != 0) throw new InvalidOperationException("ISSUE12_RESERVATION_CLEANUP_INCOMPLETE");
            if (row.Created)
            {
                await using var remove = new SqlCommand("DELETE commerce.Inventory WHERE Dataset='kohami_local' AND Sku=@sku AND Price=@price AND Active=1 AND Stock IN (0,1)", connection, tx);
                remove.Parameters.AddWithValue("@sku", row.Sku);
                remove.Parameters.AddWithValue("@price", row.Price);
                if (await remove.ExecuteNonQueryAsync() != 1) throw new InvalidOperationException("ISSUE12_SYNTHETIC_INVENTORY_CLEANUP_CONFLICT");
            }
            else
            {
                await using var restore = new SqlCommand("UPDATE commerce.Inventory SET Stock=@original WHERE Dataset='kohami_local' AND Sku=@sku AND Price=@price AND Active=1 AND Stock IN (0,1)", connection, tx);
                restore.Parameters.AddWithValue("@original", row.OriginalStock);
                restore.Parameters.AddWithValue("@sku", row.Sku);
                restore.Parameters.AddWithValue("@price", row.Price);
                if (await restore.ExecuteNonQueryAsync() != 1) throw new InvalidOperationException("ISSUE12_INVENTORY_RESTORE_CONFLICT");
            }
        }
        await tx.CommitAsync();
        File.Delete(evidencePath);
        File.Delete(statePath);
        if (verificationFailure is not null) throw new InvalidOperationException(verificationFailure);
        Console.WriteLine("PASS Issue12 removed only recorded synthetic carts, quotes, checkouts, orders, idempotency, reservations, and runner-created SKU rows");
    }

    private static async Task<long> ReadStock(SqlConnection connection, SqlTransaction tx, InventoryRow row)
    {
        await using var query = new SqlCommand("SELECT Stock FROM commerce.Inventory WHERE Dataset='kohami_local' AND Sku=@sku AND Price=@price AND Active=1", connection, tx);
        query.Parameters.AddWithValue("@sku", row.Sku);
        query.Parameters.AddWithValue("@price", row.Price);
        var result = await query.ExecuteScalarAsync();
        return result is null ? -1 : Convert.ToInt64(result);
    }

    private static async Task<int> CountReservations(SqlConnection connection, SqlTransaction tx, IEnumerable<string> skus)
    {
        var rows = skus.Distinct(StringComparer.Ordinal).ToArray();
        var count = 0;
        foreach (var sku in rows)
        {
            await using var query = new SqlCommand("SELECT COUNT(*) FROM commerce.Reservations WHERE Dataset='kohami_local' AND Sku=@sku", connection, tx);
            query.Parameters.AddWithValue("@sku", sku);
            count += Convert.ToInt32(await query.ExecuteScalarAsync());
        }
        return count;
    }

    private static async Task<int> CountHeldReservation(SqlConnection connection, SqlTransaction tx, string orderId, string sku)
    {
        await using var query = new SqlCommand("SELECT COUNT(*) FROM commerce.Reservations WHERE Dataset='kohami_local' AND OrderId=@order AND Sku=@sku AND Quantity=1 AND State='Held'", connection, tx);
        query.Parameters.AddWithValue("@order", orderId);
        query.Parameters.AddWithValue("@sku", sku);
        return Convert.ToInt32(await query.ExecuteScalarAsync());
    }

    private static async Task<JsonDocument> ReadBody(SqlConnection connection, SqlTransaction tx, string kind, string id)
    {
        await using var query = new SqlCommand("SELECT Body FROM commerce.Documents WHERE Dataset='kohami_local' AND Kind=@kind AND Id=@id", connection, tx);
        query.Parameters.AddWithValue("@kind", kind);
        query.Parameters.AddWithValue("@id", id);
        var body = await query.ExecuteScalarAsync() as string ?? throw new InvalidOperationException("ISSUE12_EXPECTED_SQL_DOCUMENT_MISSING");
        return JsonDocument.Parse(body);
    }

    private static async Task<CheckoutRow[]> RecoverRecordedCheckouts(SqlConnection connection, SqlTransaction tx, Evidence evidence)
    {
        var rows = evidence.Checkouts.ToDictionary(x => x.CheckoutId, StringComparer.Ordinal);
        foreach (var key in evidence.IdempotencyKeys)
        {
            await using var query = new SqlCommand("SELECT Response FROM commerce.Idempotency WHERE Dataset='kohami_local' AND OperationKey=@key", connection, tx);
            query.Parameters.AddWithValue("@key", key);
            var response = await query.ExecuteScalarAsync() as string;
            if (response is null) continue;
            using var body = JsonDocument.Parse(response);
            var root = body.RootElement;
            if (!root.TryGetProperty("checkoutId", out var checkoutIdValue) || checkoutIdValue.ValueKind != JsonValueKind.String
                || !root.TryGetProperty("orderId", out var orderIdValue) || orderIdValue.ValueKind != JsonValueKind.String
                || !root.TryGetProperty("snapshot", out var snapshot) || !snapshot.TryGetProperty("lines", out var lines)
                || lines.ValueKind != JsonValueKind.Array || lines.GetArrayLength() != 1
                || !lines[0].TryGetProperty("skuId", out var skuValue) || skuValue.ValueKind != JsonValueKind.String)
                continue;
            var row = new CheckoutRow(checkoutIdValue.GetString()!, orderIdValue.GetString()!, skuValue.GetString()!);
            rows[row.CheckoutId] = row;
        }
        var recovered = rows.Values.ToArray();
        if (recovered.Length > 2 || recovered.Any(x => !Regex.IsMatch(x.CheckoutId, "^checkout_[0-9a-f]{32}$")
            || !Regex.IsMatch(x.OrderId, "^order_[0-9a-f]{32}$") || !Regex.IsMatch(x.SkuId, "^[A-Za-z0-9_-]{1,100}$")))
            throw new InvalidOperationException("ISSUE12_API_EVIDENCE_INVALID");
        return recovered;
    }

    private static async Task DeleteDocument(SqlConnection connection, SqlTransaction tx, string kind, string id)
    {
        await using var delete = new SqlCommand("DELETE commerce.Documents WHERE Dataset='kohami_local' AND Kind=@kind AND Id=@id", connection, tx);
        delete.Parameters.AddWithValue("@kind", kind);
        delete.Parameters.AddWithValue("@id", id);
        await delete.ExecuteNonQueryAsync();
    }

    private static FixtureState ReadState(string configPath, string statePath)
    {
        RequirePrivatePath(configPath, statePath, mustNotExist: false);
        var state = JsonSerializer.Deserialize<FixtureState>(File.ReadAllText(statePath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new InvalidOperationException("ISSUE12_FIXTURE_STATE_INVALID");
        if (state.Rows.Length != 2 || state.Rows.Any(x => !Regex.IsMatch(x.Sku, "^[A-Za-z0-9_-]{1,100}$") || x.Price < 1 || x.OriginalStock is <0 or >100_000)
            || state.Rows.Select(x => x.Sku).Distinct(StringComparer.Ordinal).Count() != 2)
            throw new InvalidOperationException("ISSUE12_FIXTURE_STATE_INVALID");
        return state;
    }

    private static Evidence ReadEvidence(string configPath, string evidencePath)
    {
        RequirePrivatePath(configPath, evidencePath, mustNotExist: false);
        return JsonSerializer.Deserialize<Evidence>(File.ReadAllText(evidencePath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new InvalidOperationException("ISSUE12_API_EVIDENCE_INVALID");
    }

    private static void ValidateEvidence(Evidence evidence)
    {
        if (evidence.CartIds.Length >10 || evidence.QuoteIds.Length >10 || evidence.IdempotencyKeys.Length >100
            || evidence.CartIds.Any(x => !Regex.IsMatch(x, "^cart_[0-9a-f]{32}$"))
            || evidence.QuoteIds.Any(x => !Regex.IsMatch(x, "^quote_[0-9a-f]{32}$"))
            || evidence.IdempotencyKeys.Any(x => !Regex.IsMatch(x, "^issue12-[0-9a-f]{32}$"))
            || evidence.Checkouts.Length > 2
            || evidence.Checkouts.Any(x => !Regex.IsMatch(x.CheckoutId, "^checkout_[0-9a-f]{32}$")
                || !Regex.IsMatch(x.OrderId, "^order_[0-9a-f]{32}$") || !Regex.IsMatch(x.SkuId, "^[A-Za-z0-9_-]{1,100}$")))
            throw new InvalidOperationException("ISSUE12_API_EVIDENCE_INVALID");
    }

    private static void RequirePrivatePath(string configPath, string path, bool mustNotExist)
    {
        var file = new FileInfo(path);
        if (file.LinkTarget is not null || Path.GetDirectoryName(file.FullName) != Path.GetDirectoryName(Path.GetFullPath(configPath))
            || (mustNotExist && file.Exists)
            || (!OperatingSystem.IsWindows() && file.Exists && (file.UnixFileMode & (UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.OtherRead | UnixFileMode.OtherWrite)) != 0))
            throw new InvalidOperationException("ISSUE12_PRIVATE_PATH_INVALID");
    }

    private static void SetPrivateFileMode(string path)
    {
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    private static (RuntimeSettings Settings, string ConnectionString) DedicatedAdmin(string configPath)
    {
        using var config = JsonDocument.Parse(File.ReadAllText(configPath));
        var root = config.RootElement;
        if (!root.TryGetProperty("IdentitySqlTestDedicated", out var dedicated) || !dedicated.GetBoolean()
            || root.GetProperty("datasetId").GetString() != "kohami_local") throw new InvalidOperationException("DEDICATED_ISSUE12_FIXTURE_REQUIRED");
        var settings = RuntimeSettings.Load(configPath);
        var builder = new SqlConnectionStringBuilder(root.GetProperty("MigratorConnectionString").GetString())
        {
            Encrypt = SqlConnectionEncryptOption.Strict,
            TrustServerCertificate = false,
            HostNameInCertificate = settings.SqlServerName
        };
        builder["Server Certificate"] = settings.SqlCertificatePath;
        var port = Regex.Match(builder.DataSource, "^localhost,(\\d+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!port.Success || !int.TryParse(port.Groups[1].Value, out var number) || number is <14336 or >14399
            || builder.InitialCatalog != "Kohami") throw new InvalidOperationException("ISSUE12_SQL_SCOPE_INVALID");
        return (settings, builder.ConnectionString);
    }
}
