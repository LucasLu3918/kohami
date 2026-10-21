using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Kohami.M0.Core;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;

namespace Kohami.M0.Commerce.Tests;

internal static class LocalDemoWalletHttpAcceptance
{
    private const string Prefix = "local_demo_p5_";
    private sealed record IdempotencyEvidence(string Scope, string OperationKey, string Response);
    private sealed record RecoveredEvidence(string? CartId, string? QuoteId, string? CheckoutId, string? OrderId);
    internal sealed record FixtureSecurityState(bool Maintenance, string? MaintenanceTargetUserId);

    public static void VerifyRecoveryContract()
    {
        const string suffix = "regression";
        var cartKey = "local-demo-cart-" + suffix; var successKey = "local-demo-success-" + suffix;
        ExpectRecoveryFailure(
            [new("createCart", cartKey, "{\"id\":\"cart_regression\"}"), new("wrongScope", cartKey, "{\"id\":\"cart_other\"}")],
            suffix, "sku_regression", "LOCAL_DEMO_IDEMPOTENCY_KEY_AMBIGUOUS");
        ExpectRecoveryFailure(
            [new("createCart", cartKey, "{\"id\":\"cart_regression\"}"), new("createCheckout/cart_regression", successKey, "{\"checkoutId\":\"checkout_regression\"}")],
            suffix, "sku_regression", "LOCAL_DEMO_CHECKOUT_RECOVERY_INCOMPLETE");
        Console.WriteLine("PASS exact-key cross-scope and incomplete checkout recovery guards");
    }

    private static void ExpectRecoveryFailure(IReadOnlyCollection<IdempotencyEvidence> rows, string suffix, string sku, string expected)
    {
        try { _ = RecoverEvidence(rows, suffix, sku); }
        catch (InvalidOperationException error) when (error.Message == expected) { return; }
        throw new InvalidOperationException("LOCAL_DEMO_RECOVERY_GUARD_REGRESSION");
    }

    public static async Task RunAsync(string repoRoot, string localDirectory, int apiPort)
    {
        var root = Path.GetFullPath(repoRoot);
        var local = Path.GetFullPath(localDirectory);
        Require(local.StartsWith(Path.Combine(root, ".local") + Path.DirectorySeparatorChar, StringComparison.Ordinal), "LOCAL_DEMO_LOCAL_DIR_SCOPE_INVALID");
        var runtimePath = Path.Combine(local, "runtime-private.json");
        var provisionerPath = Path.Combine(local, "provisioner-private.json");
        var fixturePath = Path.Combine(local, "fixture-private.json");
        foreach (var path in new[] { runtimePath, provisionerPath, fixturePath })
            Require(File.Exists(path) && !new FileInfo(path).Attributes.HasFlag(FileAttributes.ReparsePoint), "LOCAL_DEMO_FIXTURE_CONFIG_INVALID");
        Require(apiPort is >= 5080 and <= 5099, "LOCAL_DEMO_API_PORT_INVALID");

        var settings = M0Settings.Load(runtimePath);
        new M0Sql(settings).ValidateTls();
        if (OperatingSystem.IsWindows()) throw new InvalidOperationException("WALLET03_PRIVATE_FILE_MODE_UNSUPPORTED");
        using var fixture = JsonDocument.Parse(await File.ReadAllTextAsync(fixturePath));
        using var provisioner = JsonDocument.Parse(await File.ReadAllTextAsync(provisionerPath));
        var fixtureRoot = fixture.RootElement;
        Require(fixtureRoot.TryGetProperty("KohamiDemoDedicated", out var dedicated) && dedicated.ValueKind == JsonValueKind.True,
            "LOCAL_DEMO_DEDICATED_FIXTURE_REQUIRED");
        var fixtureId = Guid.Parse(provisioner.RootElement.GetProperty("fixtureId").GetString()!);
        var runtimeBuilder = new SqlConnectionStringBuilder(settings.ConnectionString);
        Require(runtimeBuilder.DataSource.Equals($"localhost,{fixtureRoot.GetProperty("port").GetInt32()}", StringComparison.OrdinalIgnoreCase)
            && fixtureRoot.GetProperty("port").GetInt32() is not (14349 or 14350)
            && runtimeBuilder.InitialCatalog == "KohamiM0"
            && runtimeBuilder.Encrypt == SqlConnectionEncryptOption.Strict
            && !runtimeBuilder.TrustServerCertificate,
            "LOCAL_DEMO_SQL_TARGET_INVALID");
        var fixturePort = fixtureRoot.GetProperty("port").GetInt32();
        Require(fixturePort is >= 14336 and <= 14399 && fixturePort is not (14349 or 14350), "LOCAL_DEMO_SQL_PORT_INVALID");
        var migratorCredential = provisioner.RootElement.GetProperty("migrator").GetString();
        var admin = new SqlConnectionStringBuilder(settings.ConnectionString)
        {
            DataSource = $"localhost,{fixturePort}", InitialCatalog = "KohamiM0", UserID = "kohami_m0_migrator",
            Encrypt = SqlConnectionEncryptOption.Strict,
            TrustServerCertificate = false, HostNameInCertificate = settings.SqlServerName,
        };
        admin["Password"] = migratorCredential;
        admin["Server Certificate"] = settings.SqlCertificatePath;
        var securityBefore = await ReadFixtureSecurityState(admin.ConnectionString, fixtureId);
        Require(!PortInUse(apiPort), "LOCAL_DEMO_API_PORT_IN_USE");
        Require(File.Exists(Path.Combine(root, "apps/api/bin/Release/net10.0/Api.dll")), "LOCAL_DEMO_API_BUILD_REQUIRED");

        var suffix = Guid.NewGuid().ToString("N");
        var userId = Prefix + "member_" + suffix;
        var email = Prefix + suffix + "@example.test";
        var generatedPassword = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var actorCreated = false;
        string? cartId = null, quoteId = null, checkoutId = null, orderId = null, sku = null;
        int selectedStock = 0;
        Process? api = null;
        var cookies = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            await PrepareActor(admin.ConnectionString, fixtureId, userId, email, new PasswordHasher<string>().HashPassword(email, generatedPassword), securityBefore);
            actorCreated = true;
            (sku, selectedStock) = await SelectSkuAndStock(admin.ConnectionString);

            api = StartApi(root, runtimePath, apiPort);
            using var client = new HttpClient(new HttpClientHandler { UseCookies = false }) { Timeout = TimeSpan.FromSeconds(15) };
            var baseUri = new Uri($"http://127.0.0.1:{apiPort}/api/v1/");
            await WaitForApi(client, new Uri($"http://127.0.0.1:{apiPort}/health"), api);
            var csrfResponse = await Send(client, baseUri, "auth/csrf", HttpMethod.Get, cookies, null, null, null);
            Require(csrfResponse.Status == HttpStatusCode.OK && csrfResponse.Body["requestToken"] is JsonValue, "WALLET03_CSRF_SETUP_FAILED");
            var token = csrfResponse.Body["requestToken"]!.GetValue<string>();
            var login = await Send(client, baseUri, "auth/sessions", HttpMethod.Post, cookies,
                new JsonObject { ["email"] = email, ["password"] = generatedPassword }, token, settings.Origin);
            var loginCode = login.Body["code"]?.GetValue<string>() ?? "MISSING_CODE";
            Require(login.Status == HttpStatusCode.OK && login.Body["state"]?.GetValue<string>() == "MemberAuthenticated",
                $"WALLET03_REAL_LOGIN_FAILED_STATUS_{(int)login.Status}_{loginCode}");
            Require(cookies.ContainsKey("kohami.session"), "WALLET03_SESSION_COOKIE_MISSING");

            var cart = await Send(client, baseUri, "carts", HttpMethod.Post, cookies, new JsonObject(), token, settings.Origin,
                idempotency: "local-demo-cart-" + suffix);
            Require(cart.Status == HttpStatusCode.Created,
                $"WALLET03_CART_CREATE_FAILED:{(int)cart.Status}:{cart.Body["code"]?.GetValue<string>() ?? "UNKNOWN"}");
            cartId = cart.Body["id"]!.GetValue<string>();
            var cartVersion = cart.Body["version"]!.GetValue<string>();
            var add = await Send(client, baseUri, $"carts/current/items/{sku}", HttpMethod.Put, cookies,
                new JsonObject { ["quantity"] = 1 }, token, settings.Origin, cartVersion, "local-demo-item-" + suffix);
            Require(add.Status == HttpStatusCode.OK, "WALLET03_CART_ITEM_FAILED");
            cartVersion = add.Body["version"]!.GetValue<string>();
            var quote = await Send(client, baseUri, "checkout-quotes", HttpMethod.Post, cookies,
                new JsonObject { ["cartVersion"] = cartVersion, ["deliveryMethod"] = "mock_home", ["region"] = "demo_supported" }, token, settings.Origin,
                etag: cartVersion, idempotency: "local-demo-quote-" + suffix);
            Require(quote.Status == HttpStatusCode.Created, "WALLET03_QUOTE_FAILED");
            quoteId = quote.Body["id"]!.GetValue<string>();
            var total = quote.Body["snapshot"]!["totalTwd"]!.GetValue<long>();

            // The first request is intentionally insufficient and must leave all financial and stock state unchanged.
            await CreditWallet(admin.ConnectionString, userId, "synthetic-test-credit:local-demo-p5:" + suffix + ":short", total - 1);
            var beforeFailure = await ReadWalletAndStock(admin.ConnectionString, userId, sku);
            var checkoutPayload = CheckoutPayload(quoteId, cartVersion, email);
            var failed = await Send(client, baseUri, "checkouts", HttpMethod.Post, cookies, checkoutPayload, token, settings.Origin,
                cartVersion, "local-demo-failure-" + suffix);
            Require((int)failed.Status == 409 && failed.Body["code"]?.GetValue<string>() == "WALLET_INSUFFICIENT_BALANCE", "WALLET03_INSUFFICIENT_BALANCE_NOT_REJECTED");
            var afterFailure = await ReadWalletAndStock(admin.ConnectionString, userId, sku);
            Require(beforeFailure == afterFailure && await NoFinancialArtifacts(admin.ConnectionString, userId, cartId), "WALLET03_FAILED_CHECKOUT_NOT_ROLLED_BACK");

            await CreditWallet(admin.ConnectionString, userId, "synthetic-test-credit:local-demo-p5:" + suffix + ":topup", 1);
            var successful = await Send(client, baseUri, "checkouts", HttpMethod.Post, cookies, checkoutPayload, token, settings.Origin,
                cartVersion, "local-demo-success-" + suffix);
            Require(successful.Status == HttpStatusCode.Created, "WALLET03_HTTP_CHECKOUT_FAILED");
            checkoutId = successful.Body["checkoutId"]!.GetValue<string>();
            orderId = successful.Body["orderId"]!.GetValue<string>();
            var status = await Send(client, baseUri, $"checkouts/{checkoutId}/status", HttpMethod.Get, cookies, null, null, null);
            Require(status.Status == HttpStatusCode.OK && status.Body["orderState"]?.GetValue<string>() == "Confirmed"
                && status.Body["paymentState"]?.GetValue<string>() == "Succeeded", "WALLET03_CHECKOUT_STATUS_MISMATCH");
            var persisted = await ReadCheckoutState(admin.ConnectionString, userId, sku, checkoutId, orderId, total);
            Require(persisted.Balance == 0 && persisted.Debits == 1 && persisted.DebitAmount == total
                && persisted.DebitSource == checkoutId && persisted.WalletPaymentCount == 1 && persisted.PaymentAmount == total
                && persisted.CheckoutCount == 1 && persisted.CheckoutOrderId == orderId
                && persisted.OrderCount == 1 && persisted.CollectionCount == 1 && persisted.Collected == total
                && persisted.Stock == selectedStock - 1
                && persisted.OrderState == "Confirmed" && persisted.PaymentState == "Succeeded",
                "WALLET03_HTTP_CHECKOUT_SQL_STATE_INVALID");
            // Simulate losing the successful HTTP response after SQL commit. Cleanup must recover
            // the committed IDs through this run's exact idempotency operation key.
            checkoutId = null;
            orderId = null;
            await VerifyRecoveryFailureGuards(admin.ConnectionString, fixtureId, userId, email, cartId!, quoteId!, sku!, suffix, selectedStock, securityBefore);
            var logout = await Send(client, baseUri, "auth/session", HttpMethod.Delete, cookies, null, token, settings.Origin);
            Require(logout.Status == HttpStatusCode.NoContent && !cookies.ContainsKey("kohami.session"), "WALLET03_LOGOUT_COOKIE_NOT_CLEARED");
            Console.WriteLine("PASS local-demo real HTTP login/CSRF/wallet checkout and SQL assertions on marker-verified fixture");
        }
        finally
        {
            if (cookies.ContainsKey("kohami.session"))
            {
                try { using var logoutClient = new HttpClient(new HttpClientHandler { UseCookies = false }); await Send(logoutClient, new Uri($"http://127.0.0.1:{apiPort}/api/v1/"), "auth/session", HttpMethod.Delete, cookies, null, "cleanup", settings.Origin); } catch { }
            }
            if (api is { HasExited: false }) { api.Kill(true); await api.WaitForExitAsync(); }
            if (actorCreated)
            {
                try
                {
                    await Cleanup(admin.ConnectionString, fixtureId, userId, email, cartId, quoteId, checkoutId, orderId, sku, suffix, selectedStock, securityBefore);
                    await Cleanup(admin.ConnectionString, fixtureId, userId, email, cartId, quoteId, checkoutId, orderId, sku, suffix, selectedStock, securityBefore);
                    await VerifyCleanup(admin.ConnectionString, fixtureId, userId, sku ?? throw new InvalidOperationException("LOCAL_DEMO_CLEANUP_SKU_UNAVAILABLE"), selectedStock, suffix);
                    Console.WriteLine("PASS commit-then-response-lost recovery and idempotent scoped cleanup");
                }
                finally
                {
                    await RestoreFixtureSecurityTarget(admin.ConnectionString, fixtureId, userId, securityBefore);
                }
            }
            Require(await ReadFixtureSecurityState(admin.ConnectionString, fixtureId) == securityBefore, "WALLET03_MAINTENANCE_OR_TARGET_CHANGED");
        }
    }

    private static JsonObject CheckoutPayload(string quote, string version, string email) => new()
    {
        ["quoteId"] = quote, ["cartVersion"] = version, ["contact"] = new JsonObject { ["email"] = email },
        ["delivery"] = new JsonObject { ["method"] = "mock_home", ["region"] = "demo_supported" }, ["confirmed"] = true,
        ["paymentMethod"] = "wallet",
    };

    private static RecoveredEvidence RecoverEvidence(IReadOnlyCollection<IdempotencyEvidence> rows, string suffix, string? sku)
    {
        var grouped = rows.GroupBy(x => x.OperationKey, StringComparer.Ordinal).ToDictionary(x => x.Key, x => x.ToArray(), StringComparer.Ordinal);
        if (grouped.Values.Any(x => x.Length != 1)) throw new InvalidOperationException("LOCAL_DEMO_IDEMPOTENCY_KEY_AMBIGUOUS");
        var cartKey = "local-demo-cart-" + suffix; var itemKey = "local-demo-item-" + suffix;
        var quoteKey = "local-demo-quote-" + suffix; var failureKey = "local-demo-failure-" + suffix; var successKey = "local-demo-success-" + suffix;
        static JsonObject Body(IdempotencyEvidence row)
        {
            try { return JsonNode.Parse(row.Response) as JsonObject ?? throw new InvalidOperationException("LOCAL_DEMO_IDEMPOTENCY_RESPONSE_INVALID"); }
            catch (JsonException) { throw new InvalidOperationException("LOCAL_DEMO_IDEMPOTENCY_RESPONSE_INVALID"); }
        }
        static string? Text(JsonObject body, string name) => body[name] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
        var cartId = (string?)null; var quoteId = (string?)null; var checkoutId = (string?)null; var orderId = (string?)null;
        if (grouped.TryGetValue(cartKey, out var cartRows))
        {
            if (cartRows[0].Scope != "createCart") throw new InvalidOperationException("LOCAL_DEMO_IDEMPOTENCY_SCOPE_INVALID");
            cartId = Text(Body(cartRows[0]), "id");
            if (string.IsNullOrWhiteSpace(cartId)) throw new InvalidOperationException("LOCAL_DEMO_CART_ID_RECOVERY_INVALID");
        }
        if (cartId is null && (grouped.ContainsKey(itemKey) || grouped.ContainsKey(quoteKey) || grouped.ContainsKey(failureKey) || grouped.ContainsKey(successKey)))
            throw new InvalidOperationException("LOCAL_DEMO_CART_ID_RECOVERY_REQUIRED");
        if (cartId is not null)
        {
            if (grouped.TryGetValue(itemKey, out var itemRows) && (sku is null || itemRows[0].Scope != $"setCartItem/{cartId}/{sku}")) throw new InvalidOperationException("LOCAL_DEMO_IDEMPOTENCY_SCOPE_INVALID");
            if (grouped.TryGetValue(quoteKey, out var quoteRows))
            {
                if (quoteRows[0].Scope != $"createQuote/{cartId}") throw new InvalidOperationException("LOCAL_DEMO_IDEMPOTENCY_SCOPE_INVALID");
                quoteId = Text(Body(quoteRows[0]), "id");
                if (string.IsNullOrWhiteSpace(quoteId)) throw new InvalidOperationException("LOCAL_DEMO_QUOTE_ID_RECOVERY_INVALID");
            }
            foreach (var key in new[] { failureKey, successKey })
                if (grouped.TryGetValue(key, out var checkoutRows) && checkoutRows[0].Scope != $"createCheckout/{cartId}") throw new InvalidOperationException("LOCAL_DEMO_IDEMPOTENCY_SCOPE_INVALID");
            if (grouped.TryGetValue(successKey, out var successfulRows))
            {
                var body = Body(successfulRows[0]); checkoutId = Text(body, "checkoutId"); orderId = Text(body, "orderId");
                if (string.IsNullOrWhiteSpace(checkoutId) || string.IsNullOrWhiteSpace(orderId)) throw new InvalidOperationException("LOCAL_DEMO_CHECKOUT_RECOVERY_INCOMPLETE");
            }
        }
        if ((checkoutId is null) != (orderId is null)) throw new InvalidOperationException("LOCAL_DEMO_CHECKOUT_RECOVERY_INCOMPLETE");
        return new(cartId, quoteId, checkoutId, orderId);
    }

    private static async Task<(HttpStatusCode Status, JsonObject Body)> Send(HttpClient client, Uri baseUri, string path, HttpMethod method,
        IDictionary<string, string> cookies, JsonObject? body, string? csrf, string? origin, string? etag = null, string? idempotency = null)
    {
        using var request = new HttpRequestMessage(method, new Uri(baseUri, path));
        request.Headers.Host = baseUri.Authority;
        request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https");
        if (cookies.Count > 0) request.Headers.TryAddWithoutValidation("Cookie", string.Join("; ", cookies.Select(x => x.Key + "=" + x.Value)));
        if (origin is not null) request.Headers.TryAddWithoutValidation("Origin", origin);
        if (csrf is not null) request.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", csrf);
        if (etag is not null) request.Headers.IfMatch.Add(new EntityTagHeaderValue('"' + etag + '"'));
        if (idempotency is not null) request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotency);
        if (body is not null) request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        using var response = await client.SendAsync(request);
        foreach (var setCookie in response.Headers.TryGetValues("Set-Cookie", out var values) ? values : [])
        {
            var first = setCookie.Split(';', 2)[0]; var split = first.IndexOf('='); if (split <= 0) continue;
            var name = first[..split]; var value = first[(split + 1)..];
            if (setCookie.Contains("Max-Age=0", StringComparison.OrdinalIgnoreCase) || value.Length == 0) cookies.Remove(name);
            else cookies[name] = value;
        }
        JsonObject result; try { result = JsonNode.Parse(await response.Content.ReadAsStringAsync()) as JsonObject ?? new(); } catch { result = new(); }
        return (response.StatusCode, result);
    }

    internal static async Task PrepareActor(string cs, Guid fixtureId, string user, string email, string hash, FixtureSecurityState securityBefore)
    {
        await using var c = new SqlConnection(cs); await c.OpenAsync();
        await using var tx = (SqlTransaction)await c.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        await using (var marker = new SqlCommand("SELECT COUNT(*) FROM ops.DevelopmentFixtureMarker WHERE FixtureId=@id AND DatasetId='demo_m0' AND InstanceFingerprint=LOWER(CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CONVERT(nvarchar(100),SERVERPROPERTY('ServerName')),'|KohamiM0|demo_m0')),2))", c, tx))
        {
            marker.Parameters.AddWithValue("@id", fixtureId); Require(Convert.ToInt32(await marker.ExecuteScalarAsync()) == 1, "WALLET03_FIXTURE_MARKER_INVALID");
        }
        await using (var insert = new SqlCommand("INSERT m0.Users(UserId,Email,PasswordHash,EmailConfirmed,Status,SecurityVersion) VALUES(@u,@e,@p,1,'Active',1)", c, tx))
        {
            insert.Parameters.AddWithValue("@u", user); insert.Parameters.AddWithValue("@e", email); insert.Parameters.AddWithValue("@p", hash); await insert.ExecuteNonQueryAsync();
        }
        if (securityBefore.Maintenance)
        {
            await using var target = new SqlCommand("UPDATE m0.SecurityState SET MaintenanceTargetUserId=@user WHERE Id=1 AND Maintenance=1 AND (MaintenanceTargetUserId=@previous OR (MaintenanceTargetUserId IS NULL AND @previous IS NULL)) AND EXISTS(SELECT 1 FROM ops.DevelopmentFixtureMarker WHERE FixtureId=@fixture AND DatasetId='demo_m0' AND InstanceFingerprint=LOWER(CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CONVERT(nvarchar(100),SERVERPROPERTY('ServerName')),'|KohamiM0|demo_m0')),2)))", c, tx);
            target.Parameters.AddWithValue("@user", user); target.Parameters.AddWithValue("@previous", (object?)securityBefore.MaintenanceTargetUserId ?? DBNull.Value); target.Parameters.AddWithValue("@fixture", fixtureId);
            Require(await target.ExecuteNonQueryAsync() == 1, "WALLET03_MAINTENANCE_TARGET_SET_FAILED");
            await using var openWindow = new SqlCommand("UPDATE m0.SecurityState SET Maintenance=0 WHERE Id=1 AND Maintenance=1 AND MaintenanceTargetUserId=@user AND EXISTS(SELECT 1 FROM ops.DevelopmentFixtureMarker WHERE FixtureId=@fixture AND DatasetId='demo_m0' AND InstanceFingerprint=LOWER(CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CONVERT(nvarchar(100),SERVERPROPERTY('ServerName')),'|KohamiM0|demo_m0')),2)))", c, tx);
            openWindow.Parameters.AddWithValue("@user", user); openWindow.Parameters.AddWithValue("@fixture", fixtureId);
            Require(await openWindow.ExecuteNonQueryAsync() == 1, "WALLET03_MAINTENANCE_WINDOW_OPEN_FAILED");
        }
        await tx.CommitAsync();
    }

    internal static async Task<(string Sku, int Stock)> SelectSkuAndStock(string cs)
    {
        await using var c = new SqlConnection(cs); await c.OpenAsync();
        await using var cmd = new SqlCommand("SELECT TOP(1) Sku,Stock FROM m0commerce.Inventory WHERE Dataset='demo_m0' AND Active=1 AND Stock>=2 AND Price BETWEEN 1 AND 49900 ORDER BY Sku", c);
        await using var r = await cmd.ExecuteReaderAsync(); Require(await r.ReadAsync(), "WALLET03_STOCK_UNAVAILABLE"); return (r.GetString(0), r.GetInt32(1));
    }

    internal static async Task CreditWallet(string cs, string user, string source, long amount)
    {
        await using var c = new SqlConnection(cs); await c.OpenAsync(); await using var tx = await c.BeginTransactionAsync();
        await using var cmd = new SqlCommand("EXEC m0commerce.wallet_synthetic_credit @UserId,@SourceId,@AmountTwd", c, (SqlTransaction)tx);
        cmd.Parameters.AddWithValue("@UserId", user); cmd.Parameters.AddWithValue("@SourceId", source); cmd.Parameters.AddWithValue("@AmountTwd", amount); await cmd.ExecuteNonQueryAsync(); await tx.CommitAsync();
    }

    internal static async Task<(long Balance, int Debits, long DebitAmount, string DebitSource, int WalletPaymentCount, long PaymentAmount, int CheckoutCount, string CheckoutOrderId, int OrderCount, int CollectionCount, long Collected, int Stock, string OrderState, string PaymentState)> ReadCheckoutState(string cs, string user, string sku, string checkout, string order, long total)
    {
        await using var c = new SqlConnection(cs); await c.OpenAsync();
        await using var cmd = new SqlCommand("SELECT w.AvailableTwd,(SELECT COUNT(*) FROM m0commerce.WalletLedger WHERE Dataset='demo_m0' AND UserId=@u AND EntryType='CheckoutDebit'),ISNULL((SELECT AmountTwd FROM m0commerce.WalletLedger WHERE Dataset='demo_m0' AND UserId=@u AND EntryType='CheckoutDebit' AND SourceId=@c),0),ISNULL((SELECT SourceId FROM m0commerce.WalletLedger WHERE Dataset='demo_m0' AND UserId=@u AND EntryType='CheckoutDebit' AND SourceId=@c),''),(SELECT COUNT(*) FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='WalletPayment' AND OwnerId=@u AND JSON_VALUE(Body,'$.checkoutId')=@c),ISNULL((SELECT TRY_CONVERT(bigint,JSON_VALUE(Body,'$.amountTwd')) FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='WalletPayment' AND OwnerId=@u AND JSON_VALUE(Body,'$.checkoutId')=@c),0),(SELECT COUNT(*) FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='Checkout' AND Id=@c),ISNULL((SELECT JSON_VALUE(Body,'$.orderId') FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='Checkout' AND Id=@c),''),(SELECT COUNT(*) FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='Order' AND Id=@o),(SELECT COUNT(*) FROM m0commerce.Collections WHERE Dataset='demo_m0' AND OrderId=@o),ISNULL((SELECT SUM(Collected) FROM m0commerce.Collections WHERE Dataset='demo_m0' AND OrderId=@o),0),(SELECT Stock FROM m0commerce.Inventory WHERE Dataset='demo_m0' AND Sku=@s),JSON_VALUE(o.Body,'$.orderState'),JSON_VALUE(o.Body,'$.paymentState') FROM m0commerce.WalletAccounts w JOIN m0commerce.Documents o ON o.Dataset=w.Dataset AND o.Kind='Order' AND o.Id=@o WHERE w.Dataset='demo_m0' AND w.UserId=@u", c);
        cmd.Parameters.AddWithValue("@u", user); cmd.Parameters.AddWithValue("@s", sku); cmd.Parameters.AddWithValue("@o", order); cmd.Parameters.AddWithValue("@c", checkout);
        await using var r = await cmd.ExecuteReaderAsync(); Require(await r.ReadAsync(), "WALLET03_SQL_ORDER_MISSING");
        Require(r.GetInt64(2) == total && r.GetInt64(5) == total, "WALLET03_SQL_PAYMENT_AMOUNT_INVALID");
        return (r.GetInt64(0), r.GetInt32(1), r.GetInt64(2), r.GetString(3), r.GetInt32(4), r.GetInt64(5), r.GetInt32(6), r.GetString(7), r.GetInt32(8), r.GetInt32(9), r.GetInt64(10), r.GetInt32(11), r.GetString(12), r.GetString(13));
    }

    private static async Task<(long Balance, int Debits, int Stock)> ReadWalletAndStock(string cs, string user, string sku)
    {
        await using var c = new SqlConnection(cs); await c.OpenAsync();
        await using var cmd = new SqlCommand("SELECT ISNULL((SELECT AvailableTwd FROM m0commerce.WalletAccounts WHERE Dataset='demo_m0' AND UserId=@u),0),(SELECT COUNT(*) FROM m0commerce.WalletLedger WHERE Dataset='demo_m0' AND UserId=@u AND EntryType='CheckoutDebit'),(SELECT Stock FROM m0commerce.Inventory WHERE Dataset='demo_m0' AND Sku=@s)", c);
        cmd.Parameters.AddWithValue("@u", user); cmd.Parameters.AddWithValue("@s", sku); await using var r = await cmd.ExecuteReaderAsync(); await r.ReadAsync(); return (r.GetInt64(0), r.GetInt32(1), r.GetInt32(2));
    }

    private static async Task<bool> NoFinancialArtifacts(string cs, string user, string cart)
    {
        await using var c = new SqlConnection(cs); await c.OpenAsync();
        await using var cmd = new SqlCommand("SELECT (SELECT COUNT(*) FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind IN('Order','Checkout','WalletPayment') AND OwnerId=@u)+(SELECT COUNT(*) FROM m0commerce.Collections WHERE Dataset='demo_m0' AND OrderId IN(SELECT Id FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='Order' AND OwnerId=@u))+(SELECT COUNT(*) FROM m0commerce.Idempotency WHERE Dataset='demo_m0' AND Actor=@u AND Scope='createCheckout/'+@c)", c);
        cmd.Parameters.AddWithValue("@u", user); cmd.Parameters.AddWithValue("@c", cart); return Convert.ToInt32(await cmd.ExecuteScalarAsync()) == 0;
    }

    private static async Task Cleanup(string cs, Guid fixtureId, string user, string email, string? cart, string? quote, string? checkout, string? order, string? sku, string suffix, int selectedStock, FixtureSecurityState securityBefore)
    {
        await using var c = new SqlConnection(cs); await c.OpenAsync(); await using var tx = await c.BeginTransactionAsync();
        await using var guard = new SqlCommand("IF NOT EXISTS(SELECT 1 FROM ops.DevelopmentFixtureMarker WHERE FixtureId=@f AND DatasetId='demo_m0' AND InstanceFingerprint=LOWER(CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CONVERT(nvarchar(100),SERVERPROPERTY('ServerName')),'|KohamiM0|demo_m0')),2))) THROW 51000,'FIXTURE_MARKER_INVALID',1", c, (SqlTransaction)tx);
        guard.Parameters.AddWithValue("@f", fixtureId); await guard.ExecuteNonQueryAsync();
        var evidenceRows = new List<IdempotencyEvidence>();
        await using (var recover = new SqlCommand(@"SELECT Scope,OperationKey,Response
 FROM m0commerce.Idempotency WITH (UPDLOCK,HOLDLOCK)
 WHERE Dataset='demo_m0' AND Actor=@u AND OperationKey IN(@cartKey,@itemKey,@quoteKey,@failureKey,@successKey)", c, (SqlTransaction)tx))
        {
            recover.Parameters.AddWithValue("@u", user); recover.Parameters.AddWithValue("@cartKey", "local-demo-cart-" + suffix); recover.Parameters.AddWithValue("@itemKey", "local-demo-item-" + suffix); recover.Parameters.AddWithValue("@quoteKey", "local-demo-quote-" + suffix); recover.Parameters.AddWithValue("@failureKey", "local-demo-failure-" + suffix); recover.Parameters.AddWithValue("@successKey", "local-demo-success-" + suffix);
            await using var r = await recover.ExecuteReaderAsync();
            while (await r.ReadAsync()) evidenceRows.Add(new(r.GetString(0), r.GetString(1), r.GetString(2)));
        }
        var recovered = RecoverEvidence(evidenceRows, suffix, sku);
        cart ??= recovered.CartId; quote ??= recovered.QuoteId; checkout ??= recovered.CheckoutId; order ??= recovered.OrderId;
        if (order is not null)
        {
            if (sku is null || checkout is null) throw new InvalidOperationException("LOCAL_DEMO_CLEANUP_IDENTITY_UNAVAILABLE");
            await using var reconcile = new SqlCommand(@"DECLARE @reservationCount int=(SELECT COUNT(*) FROM m0commerce.Reservations WITH (UPDLOCK,HOLDLOCK) WHERE Dataset='demo_m0' AND OrderId=@o AND Sku=@s AND Quantity=1 AND State='Consumed');
IF @reservationCount<>1 THROW 51000,'LOCAL_DEMO_STOCK_DELTA_UNVERIFIED',1;
IF (SELECT COUNT(*) FROM m0commerce.Documents WITH (UPDLOCK,HOLDLOCK) WHERE Dataset='demo_m0' AND Kind='Checkout' AND Id=@c AND OwnerId=@u AND JSON_VALUE(Body,'$.orderId')=@o)<>1 THROW 51000,'LOCAL_DEMO_CHECKOUT_ORDER_LINK_INVALID',1;
IF (SELECT COUNT(*) FROM m0commerce.Documents WITH (UPDLOCK,HOLDLOCK) WHERE Dataset='demo_m0' AND Kind='Order' AND Id=@o AND OwnerId=@u)<>1 THROW 51000,'LOCAL_DEMO_ORDER_LINK_INVALID',1;
DECLARE @stock int=(SELECT Stock FROM m0commerce.Inventory WITH (UPDLOCK,HOLDLOCK) WHERE Dataset='demo_m0' AND Sku=@s);
IF @stock IS NULL OR @stock<>@expected THROW 51000,'LOCAL_DEMO_STOCK_DELTA_UNVERIFIED',1;
UPDATE m0commerce.Inventory SET Stock=Stock+1 WHERE Dataset='demo_m0' AND Sku=@s AND Stock=@expected;
IF @@ROWCOUNT<>1 THROW 51000,'LOCAL_DEMO_STOCK_ROW_COUNT_INVALID',1;", c, (SqlTransaction)tx);
            reconcile.Parameters.AddWithValue("@o", order); reconcile.Parameters.AddWithValue("@c", checkout); reconcile.Parameters.AddWithValue("@u", user); reconcile.Parameters.AddWithValue("@s", sku); reconcile.Parameters.AddWithValue("@expected", selectedStock - 1);
            await reconcile.ExecuteNonQueryAsync();
        }
        if (order is not null) { await using var cmd = new SqlCommand("DELETE m0commerce.RefundUnits WHERE Dataset='demo_m0' AND OrderId=@o; DELETE m0commerce.Collections WHERE Dataset='demo_m0' AND OrderId=@o; DELETE m0commerce.Reservations WHERE Dataset='demo_m0' AND OrderId=@o; DELETE m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='WalletPayment' AND OwnerId=@u AND JSON_VALUE(Body,'$.checkoutId')=@c; DELETE m0commerce.Documents WHERE Dataset='demo_m0' AND Id IN(@o,@c)", c, (SqlTransaction)tx); cmd.Parameters.AddWithValue("@o", order); cmd.Parameters.AddWithValue("@c", (object?)checkout ?? DBNull.Value); cmd.Parameters.AddWithValue("@u", user); await cmd.ExecuteNonQueryAsync(); }
        await using (var cmd = new SqlCommand(@"DELETE m0commerce.Idempotency WHERE Dataset='demo_m0' AND Actor=@u AND
 ((Scope='createCart' AND OperationKey=@cartKey) OR
  (Scope='setCartItem/'+@cart+'/'+@s AND OperationKey=@itemKey) OR
  (Scope='createQuote/'+@cart AND OperationKey=@quoteKey) OR
  (Scope='createCheckout/'+@cart AND OperationKey IN(@failureKey,@successKey)));
DELETE m0commerce.Documents WHERE Dataset='demo_m0' AND Id IN(@cart,@quote)", c, (SqlTransaction)tx)) { cmd.Parameters.AddWithValue("@u", user); cmd.Parameters.AddWithValue("@cart", (object?)cart ?? DBNull.Value); cmd.Parameters.AddWithValue("@quote", (object?)quote ?? DBNull.Value); cmd.Parameters.AddWithValue("@s", (object?)sku ?? DBNull.Value); cmd.Parameters.AddWithValue("@cartKey", "local-demo-cart-" + suffix); cmd.Parameters.AddWithValue("@itemKey", "local-demo-item-" + suffix); cmd.Parameters.AddWithValue("@quoteKey", "local-demo-quote-" + suffix); cmd.Parameters.AddWithValue("@failureKey", "local-demo-failure-" + suffix); cmd.Parameters.AddWithValue("@successKey", "local-demo-success-" + suffix); await cmd.ExecuteNonQueryAsync(); }
        await using (var cmd = new SqlCommand("DELETE m0commerce.WalletLedger WHERE Dataset='demo_m0' AND UserId=@u; DELETE m0commerce.WalletAccounts WHERE Dataset='demo_m0' AND UserId=@u; DELETE m0.SecurityAudit WHERE UserId=@u; DELETE m0.Sessions WHERE UserId=@u; DELETE m0.AuthFailures WHERE AccountKey=LOWER(CONVERT(varchar(64),HASHBYTES('SHA2_256',LOWER(@e)),2)); DELETE m0.Users WHERE UserId=@u AND Email=@e", c, (SqlTransaction)tx)) { cmd.Parameters.AddWithValue("@u", user); cmd.Parameters.AddWithValue("@e", email); await cmd.ExecuteNonQueryAsync(); }
        await RestoreMaintenanceTarget(c, (SqlTransaction)tx, fixtureId, user, securityBefore);
        await tx.CommitAsync();
    }

    internal static async Task RestoreMaintenanceTarget(SqlConnection connection, SqlTransaction transaction, Guid fixtureId, string user, FixtureSecurityState securityBefore)
    {
        await using var restore = new SqlCommand(@"IF NOT EXISTS(SELECT 1 FROM ops.DevelopmentFixtureMarker WHERE FixtureId=@fixture AND DatasetId='demo_m0' AND InstanceFingerprint=LOWER(CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CONVERT(nvarchar(100),SERVERPROPERTY('ServerName')),'|KohamiM0|demo_m0')),2))) THROW 51000,'FIXTURE_MARKER_INVALID',1;
DECLARE @maintenance bit,@target nvarchar(100); SELECT @maintenance=Maintenance,@target=MaintenanceTargetUserId FROM m0.SecurityState WITH(UPDLOCK,HOLDLOCK) WHERE Id=1;
IF @wasMaintenance=1 BEGIN
 IF @maintenance=0 AND @target=@user UPDATE m0.SecurityState SET Maintenance=1,MaintenanceTargetUserId=@previousTarget WHERE Id=1 AND Maintenance=0 AND MaintenanceTargetUserId=@user;
 ELSE IF NOT(@maintenance=1 AND (@target=@previousTarget OR (@target IS NULL AND @previousTarget IS NULL))) THROW 51000,'WALLET03_MAINTENANCE_OR_TARGET_CHANGED',1;
END
ELSE IF @maintenance<>0 OR NOT(@target=@previousTarget OR (@target IS NULL AND @previousTarget IS NULL)) THROW 51000,'WALLET03_MAINTENANCE_OR_TARGET_CHANGED',1;", connection, transaction);
        restore.Parameters.AddWithValue("@fixture", fixtureId);
        restore.Parameters.AddWithValue("@wasMaintenance", securityBefore.Maintenance);
        restore.Parameters.AddWithValue("@previousTarget", (object?)securityBefore.MaintenanceTargetUserId ?? DBNull.Value);
        restore.Parameters.AddWithValue("@user", user);
        await restore.ExecuteNonQueryAsync();
    }

    internal static async Task RestoreFixtureSecurityTarget(string cs, Guid fixtureId, string user, FixtureSecurityState securityBefore)
    {
        await using var connection = new SqlConnection(cs); await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        await RestoreMaintenanceTarget(connection, transaction, fixtureId, user, securityBefore);
        await transaction.CommitAsync();
    }

    private static async Task VerifyCleanup(string cs, Guid fixtureId, string user, string sku, int expectedStock, string suffix)
    {
        await using var c = new SqlConnection(cs); await c.OpenAsync();
        await using var cmd = new SqlCommand(@"SELECT
(SELECT COUNT(*) FROM ops.DevelopmentFixtureMarker WHERE FixtureId=@f AND DatasetId='demo_m0' AND InstanceFingerprint=LOWER(CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CONVERT(nvarchar(100),SERVERPROPERTY('ServerName')),'|KohamiM0|demo_m0')),2))),
(SELECT COUNT(*) FROM m0.Users WHERE UserId=@u),
(SELECT COUNT(*) FROM m0commerce.WalletLedger WHERE Dataset='demo_m0' AND UserId=@u),
(SELECT COUNT(*) FROM m0commerce.Idempotency WHERE Dataset='demo_m0' AND Actor=@u AND OperationKey IN(@cartKey,@itemKey,@quoteKey,@failureKey,@successKey)),
(SELECT Stock FROM m0commerce.Inventory WHERE Dataset='demo_m0' AND Sku=@s)", c);
        cmd.Parameters.AddWithValue("@f", fixtureId); cmd.Parameters.AddWithValue("@u", user); cmd.Parameters.AddWithValue("@s", sku);
        cmd.Parameters.AddWithValue("@cartKey", "local-demo-cart-" + suffix); cmd.Parameters.AddWithValue("@itemKey", "local-demo-item-" + suffix); cmd.Parameters.AddWithValue("@quoteKey", "local-demo-quote-" + suffix); cmd.Parameters.AddWithValue("@failureKey", "local-demo-failure-" + suffix); cmd.Parameters.AddWithValue("@successKey", "local-demo-success-" + suffix);
        await using var r = await cmd.ExecuteReaderAsync(); await r.ReadAsync();
        Require(r.GetInt32(0) == 1 && r.GetInt32(1) == 0 && r.GetInt32(2) == 0 && r.GetInt32(3) == 0 && r.GetInt32(4) == expectedStock, "LOCAL_DEMO_CLEANUP_VERIFICATION_FAILED");
    }

    private static async Task VerifyRecoveryFailureGuards(string cs, Guid fixtureId, string user, string email, string cart, string quote, string sku, string suffix, int selectedStock, FixtureSecurityState securityBefore)
    {
        await using var c = new SqlConnection(cs); await c.OpenAsync();
        var cartKey = "local-demo-cart-" + suffix; var successKey = "local-demo-success-" + suffix; var successScope = "createCheckout/" + cart;
        await using var originalQuery = new SqlCommand("SELECT Response FROM m0commerce.Idempotency WHERE Dataset='demo_m0' AND Actor=@u AND Scope=@s AND OperationKey=@key", c);
        originalQuery.Parameters.AddWithValue("@u", user); originalQuery.Parameters.AddWithValue("@s", successScope); originalQuery.Parameters.AddWithValue("@key", successKey);
        var originalResponse = Convert.ToString(await originalQuery.ExecuteScalarAsync()) ?? throw new InvalidOperationException("LOCAL_DEMO_SUCCESS_REPLAY_MISSING");
        var wrongScope = "p5-scope-regression-probe";
        await using (var insert = new SqlCommand("INSERT m0commerce.Idempotency(Dataset,Actor,Scope,OperationKey,RequestHash,Response,StatusCode,Version) VALUES('demo_m0',@u,@scope,@key,REPLICATE('a',64),'{}',201,1)", c))
        {
            insert.Parameters.AddWithValue("@u", user); insert.Parameters.AddWithValue("@scope", wrongScope); insert.Parameters.AddWithValue("@key", cartKey); await insert.ExecuteNonQueryAsync();
        }
        try { await ExpectCleanupFailure(cs, fixtureId, user, email, cart, quote, null, null, sku, suffix, selectedStock, "LOCAL_DEMO_IDEMPOTENCY_KEY_AMBIGUOUS", securityBefore); Console.WriteLine("PASS SQL cleanup rejects same actor/key across scopes without deleting checkout state"); }
        finally { await using var remove = new SqlCommand("DELETE m0commerce.Idempotency WHERE Dataset='demo_m0' AND Actor=@u AND Scope=@scope AND OperationKey=@key", c); remove.Parameters.AddWithValue("@u", user); remove.Parameters.AddWithValue("@scope", wrongScope); remove.Parameters.AddWithValue("@key", cartKey); await remove.ExecuteNonQueryAsync(); }

        await using (var corrupt = new SqlCommand("UPDATE m0commerce.Idempotency SET Response=JSON_MODIFY(Response,'$.orderId',NULL) WHERE Dataset='demo_m0' AND Actor=@u AND Scope=@s AND OperationKey=@key", c))
        {
            corrupt.Parameters.AddWithValue("@u", user); corrupt.Parameters.AddWithValue("@s", successScope); corrupt.Parameters.AddWithValue("@key", successKey); Require(await corrupt.ExecuteNonQueryAsync() == 1, "LOCAL_DEMO_SUCCESS_REPLAY_MISSING");
        }
        try { await ExpectCleanupFailure(cs, fixtureId, user, email, cart, quote, null, null, sku, suffix, selectedStock, "LOCAL_DEMO_CHECKOUT_RECOVERY_INCOMPLETE", securityBefore); Console.WriteLine("PASS SQL cleanup rejects incomplete checkout/order replay and preserves stock and rows"); }
        finally { await using var restore = new SqlCommand("UPDATE m0commerce.Idempotency SET Response=@response WHERE Dataset='demo_m0' AND Actor=@u AND Scope=@s AND OperationKey=@key", c); restore.Parameters.AddWithValue("@response", originalResponse); restore.Parameters.AddWithValue("@u", user); restore.Parameters.AddWithValue("@s", successScope); restore.Parameters.AddWithValue("@key", successKey); await restore.ExecuteNonQueryAsync(); }

        await using var preserved = new SqlCommand(@"SELECT
(SELECT COUNT(*) FROM m0.Users WHERE UserId=@u AND Email=@e),
(SELECT COUNT(*) FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='Order' AND OwnerId=@u),
(SELECT COUNT(*) FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='Checkout' AND OwnerId=@u),
(SELECT COUNT(*) FROM m0commerce.Reservations WHERE Dataset='demo_m0' AND State='Consumed' AND OrderId IN(SELECT Id FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='Order' AND OwnerId=@u)),
(SELECT Stock FROM m0commerce.Inventory WHERE Dataset='demo_m0' AND Sku=@sku)", c);
        preserved.Parameters.AddWithValue("@u", user); preserved.Parameters.AddWithValue("@e", email); preserved.Parameters.AddWithValue("@sku", sku);
        await using var r = await preserved.ExecuteReaderAsync(); await r.ReadAsync();
        Require(r.GetInt32(0) == 1 && r.GetInt32(1) == 1 && r.GetInt32(2) == 1 && r.GetInt32(3) == 1 && r.GetInt32(4) == selectedStock - 1, "LOCAL_DEMO_RECOVERY_FAILURE_MUTATED_CHECKOUT");
    }

    private static async Task ExpectCleanupFailure(string cs, Guid fixtureId, string user, string email, string cart, string quote, string? checkout, string? order, string sku, string suffix, int selectedStock, string expected, FixtureSecurityState securityBefore)
    {
        try { await Cleanup(cs, fixtureId, user, email, cart, quote, checkout, order, sku, suffix, selectedStock, securityBefore); }
        catch (InvalidOperationException error) when (error.Message == expected) { return; }
        throw new InvalidOperationException("LOCAL_DEMO_EXPECTED_CLEANUP_GUARD_DID_NOT_FIRE");
    }

    internal static async Task<FixtureSecurityState> ReadFixtureSecurityState(string cs, Guid fixtureId)
    {
        await using var c = new SqlConnection(cs); await c.OpenAsync();
        await using var cmd = new SqlCommand("SELECT Maintenance,MaintenanceTargetUserId FROM m0.SecurityState WHERE Id=1 AND EXISTS(SELECT 1 FROM ops.DevelopmentFixtureMarker WHERE FixtureId=@f AND DatasetId='demo_m0' AND InstanceFingerprint=LOWER(CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CONVERT(nvarchar(100),SERVERPROPERTY('ServerName')),'|KohamiM0|demo_m0')),2)))", c);
        cmd.Parameters.AddWithValue("@f", fixtureId);
        await using var reader = await cmd.ExecuteReaderAsync();
        Require(await reader.ReadAsync(), "WALLET03_FIXTURE_MARKER_INVALID");
        return new FixtureSecurityState(reader.GetBoolean(0), reader.IsDBNull(1) ? null : reader.GetString(1));
    }
    private static Process StartApi(string root, string config, int port)
    {
        var dll = Path.Combine(root, "apps/api/bin/Release/net10.0/Api.dll"); Require(File.Exists(dll), "WALLET03_API_BUILD_REQUIRED");
        var start = new ProcessStartInfo("/usr/local/share/dotnet/dotnet", dll) { WorkingDirectory = root, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        start.Environment["M0_CONFIG"] = config; start.Environment["KOHAMI_API_PORT"] = port.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var process = Process.Start(start) ?? throw new InvalidOperationException("WALLET03_API_START_FAILED"); process.OutputDataReceived += (_, _) => { }; process.ErrorDataReceived += (_, _) => { }; process.BeginOutputReadLine(); process.BeginErrorReadLine(); return process;
    }
    private static async Task WaitForApi(HttpClient client, Uri health, Process process)
    {
        for (var attempt = 0; attempt < 60; attempt++) { if (process.HasExited) throw new InvalidOperationException("WALLET03_API_EXITED"); try { using var response = await client.GetAsync(health); if (response.IsSuccessStatusCode) return; } catch (HttpRequestException) { } await Task.Delay(250); }
        throw new TimeoutException("WALLET03_API_START_TIMEOUT");
    }
    private static bool PortInUse(int port) { try { using var socket = new System.Net.Sockets.TcpClient(); var pending = socket.ConnectAsync("127.0.0.1", port); return pending.Wait(250) && socket.Connected; } catch { return false; } }
    private static void Require(bool condition, string code) { if (!condition) throw new InvalidOperationException(code); }
}

internal static class LocalDemoWalletBrowserAcceptance
{
    private const long InitialCreditTwd = 50_000;
    private static readonly string Dotnet = "/usr/local/share/dotnet/dotnet";

    public static async Task RunAsync(string repoRoot, string localDirectory, int apiPort, int sitePort)
    {
        var root = Path.GetFullPath(repoRoot);
        var local = Path.GetFullPath(localDirectory);
        LocalDemoWalletBrowserRequire(local.StartsWith(Path.Combine(root, ".local") + Path.DirectorySeparatorChar, StringComparison.Ordinal), "LOCAL_DEMO_LOCAL_DIR_SCOPE_INVALID");
        LocalDemoWalletBrowserRequire(apiPort is >= 5080 and <= 5099 && sitePort is >= 3002 and <= 3999, "LOCAL_DEMO_BROWSER_PORT_INVALID");
        LocalDemoWalletBrowserRequire(!LocalDemoWalletHttpAcceptancePortInUse(apiPort) && !LocalDemoWalletHttpAcceptancePortInUse(sitePort), "LOCAL_DEMO_BROWSER_PORT_IN_USE");

        var runtimePath = Path.Combine(local, "runtime-private.json");
        var fixturePath = Path.Combine(local, "fixture-private.json");
        var provisionerPath = Path.Combine(local, "provisioner-private.json");
        var certPath = Path.Combine(local, "browser/public.pem");
        var keyPath = Path.Combine(local, "browser/private.key");
        foreach (var path in new[] { runtimePath, fixturePath, provisionerPath, certPath, keyPath })
            LocalDemoWalletBrowserRequire(File.Exists(path) && !new FileInfo(path).Attributes.HasFlag(FileAttributes.ReparsePoint), "LOCAL_DEMO_BROWSER_PROFILE_INVALID");
        if (!OperatingSystem.IsWindows())
            foreach (var path in new[] { runtimePath, fixturePath, provisionerPath, keyPath })
                LocalDemoWalletBrowserRequire((File.GetUnixFileMode(path) & (UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute)) == 0, "LOCAL_DEMO_BROWSER_PRIVATE_MODE_INVALID");
        using var fixture = JsonDocument.Parse(await File.ReadAllTextAsync(fixturePath));
        using var provisioner = JsonDocument.Parse(await File.ReadAllTextAsync(provisionerPath));
        LocalDemoWalletBrowserRequire(fixture.RootElement.GetProperty("KohamiDemoDedicated").GetBoolean(), "LOCAL_DEMO_DEDICATED_FIXTURE_REQUIRED");
        var fixturePort = fixture.RootElement.GetProperty("port").GetInt32();
        LocalDemoWalletBrowserRequire(fixturePort is >= 14336 and <= 14399 && fixturePort is not (14349 or 14350), "LOCAL_DEMO_FIXTURE_PORT_INVALID");
        var fixtureId = Guid.Parse(provisioner.RootElement.GetProperty("fixtureId").GetString()!);
        var settings = M0Settings.Load(runtimePath);
        new M0Sql(settings).ValidateTls();
        var runtimeBuilder = new SqlConnectionStringBuilder(settings.ConnectionString);
        LocalDemoWalletBrowserRequire(runtimeBuilder.DataSource.Equals($"localhost,{fixturePort}", StringComparison.OrdinalIgnoreCase)
            && runtimeBuilder.InitialCatalog == "KohamiM0" && runtimeBuilder.Encrypt == SqlConnectionEncryptOption.Strict
            && !runtimeBuilder.TrustServerCertificate, "LOCAL_DEMO_SQL_TARGET_INVALID");
        var migrator = provisioner.RootElement.GetProperty("migrator").GetString()!;
        var admin = new SqlConnectionStringBuilder(settings.ConnectionString)
        {
            DataSource = $"localhost,{fixturePort}", InitialCatalog = "KohamiM0", UserID = "kohami_m0_migrator",
            Encrypt = SqlConnectionEncryptOption.Strict, TrustServerCertificate = false, HostNameInCertificate = settings.SqlServerName,
        };
        admin["Password"] = migrator;
        admin["Server Certificate"] = settings.SqlCertificatePath;
        var securityBefore = await LocalDemoWalletHttpAcceptance.ReadFixtureSecurityState(admin.ConnectionString, fixtureId);

        var suffix = Guid.NewGuid().ToString("N");
        var userId = "local_demo_p5_browser_member_" + suffix;
        var email = "local_demo_p5_browser_" + suffix + "@example.test";
        var syntheticLoginMaterial = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var (sku, stock) = await LocalDemoWalletHttpAcceptance.SelectSkuAndStock(admin.ConnectionString);
        var evidencePath = Path.Combine(local, "p5-wallet-browser-evidence.json");
        var browserArtifacts = Path.Combine(root, ".local/m0/p5-wallet-browser-artifacts-" + suffix);
        LocalDemoWalletBrowserRequire(!Directory.Exists(browserArtifacts) && !File.Exists(browserArtifacts), "LOCAL_DEMO_BROWSER_ARTIFACT_PATH_IN_USE");
        var actorCreated = false;
        Process? api = null;
        Process? storefront = null;
        string? checkoutId = null;
        string? orderId = null;
        string? cartId = null;
        string? quoteId = null;
        long total = 0;
        try
        {
            await LocalDemoWalletHttpAcceptance.PrepareActor(admin.ConnectionString, fixtureId, userId, email, new PasswordHasher<string>().HashPassword(email, syntheticLoginMaterial), securityBefore);
            actorCreated = true;
            await LocalDemoWalletHttpAcceptance.CreditWallet(admin.ConnectionString, userId, "synthetic-test-credit:p5-browser:" + suffix, InitialCreditTwd);

            var configPath = await WritePreviewConfig(runtimePath, $"https://localhost:{sitePort}");
            try
            {
                api = StartLocalDemoApi(root, local, configPath, apiPort);
                using var apiClient = new HttpClient();
                await WaitForLocalDemoApi(apiClient, apiPort, api);
                storefront = StartStorefront(root, local, apiPort, sitePort);
                await WaitForLocalDemoStorefront(sitePort, storefront);
                await RunPlaywright(root, local, sitePort, userId, email, syntheticLoginMaterial, sku, browserArtifacts);

                using var evidence = JsonDocument.Parse(await File.ReadAllTextAsync(evidencePath));
                var result = evidence.RootElement;
                LocalDemoWalletBrowserRequire(result.GetProperty("userId").GetString() == userId
                    && result.GetProperty("email").GetString() == email && result.GetProperty("skuId").GetString() == sku,
                    "LOCAL_DEMO_BROWSER_EVIDENCE_IDENTITY_INVALID");
                cartId = result.GetProperty("cartId").GetString(); quoteId = result.GetProperty("quoteId").GetString();
                checkoutId = result.GetProperty("checkoutId").GetString(); orderId = result.GetProperty("orderId").GetString();
                total = result.GetProperty("totalTwd").GetInt64();
                LocalDemoWalletBrowserRequire(total is > 0 and <= InitialCreditTwd, "LOCAL_DEMO_BROWSER_EVIDENCE_TOTAL_INVALID");
                LocalDemoWalletBrowserRequire(!string.IsNullOrWhiteSpace(cartId) && !string.IsNullOrWhiteSpace(quoteId)
                    && !string.IsNullOrWhiteSpace(checkoutId) && !string.IsNullOrWhiteSpace(orderId), "LOCAL_DEMO_BROWSER_EVIDENCE_IDS_INVALID");
                var persisted = await LocalDemoWalletHttpAcceptance.ReadCheckoutState(admin.ConnectionString, userId, sku, checkoutId!, orderId!, total);
                LocalDemoWalletBrowserRequire(persisted.Balance == InitialCreditTwd - total && persisted.Debits == 1
                    && persisted.DebitAmount == total && persisted.DebitSource == checkoutId
                    && persisted.WalletPaymentCount == 1 && persisted.PaymentAmount == total
                    && persisted.CheckoutCount == 1 && persisted.CheckoutOrderId == orderId
                    && persisted.OrderCount == 1 && persisted.CollectionCount == 1 && persisted.Collected == total
                    && persisted.Stock == stock - 1 && persisted.OrderState == "Confirmed" && persisted.PaymentState == "Succeeded",
                    "LOCAL_DEMO_BROWSER_SQL_STATE_INVALID");
                Console.WriteLine("PASS real browser member login, same-origin API wallet checkout, and exact SQL order/payment/ledger readback");
            }
            finally
            {
                File.Delete(configPath);
            }
        }
        finally
        {
            if (storefront is { HasExited: false }) { storefront.Kill(true); await storefront.WaitForExitAsync(); }
            if (api is { HasExited: false }) { api.Kill(true); await api.WaitForExitAsync(); }
            if (actorCreated)
            {
                try
                {
                    await CleanupBrowserActor(admin.ConnectionString, fixtureId, userId, email, sku, stock, cartId, quoteId, checkoutId, orderId, securityBefore);
                    await VerifyBrowserCleanup(admin.ConnectionString, fixtureId, userId, sku, stock);
                    Console.WriteLine("PASS exact synthetic-member cleanup restored the original inventory and preserved maintenance");
                }
                finally
                {
                    await LocalDemoWalletHttpAcceptance.RestoreFixtureSecurityTarget(admin.ConnectionString, fixtureId, userId, securityBefore);
                }
            }
            File.Delete(evidencePath);
            if (Directory.Exists(browserArtifacts)) Directory.Delete(browserArtifacts, recursive: true);
            LocalDemoWalletBrowserRequire(await LocalDemoWalletHttpAcceptance.ReadFixtureSecurityState(admin.ConnectionString, fixtureId) == securityBefore, "LOCAL_DEMO_BROWSER_MAINTENANCE_OR_TARGET_CHANGED");
        }
    }

    private static Process StartLocalDemoApi(string root, string local, string config, int port)
    {
        var dll = Path.Combine(root, "apps/api/bin/Release/net10.0/Api.dll");
        LocalDemoWalletBrowserRequire(File.Exists(dll), "LOCAL_DEMO_API_BUILD_REQUIRED");
        var start = new ProcessStartInfo(Dotnet, dll) { WorkingDirectory = root, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        start.Environment["M0_CONFIG"] = config; start.Environment["KOHAMI_API_PORT"] = port.ToString(System.Globalization.CultureInfo.InvariantCulture);
        start.Environment["KOHAMI_M0_LOCAL_DIR"] = local; start.Environment["KOHAMI_M0_FIXTURE_PURPOSE"] = "local-demo";
        start.Environment["KOHAMI_M0_TEST_CONFIG"] = Path.Combine(local, "fixture-private.json");
        return StartQuiet(start, "LOCAL_DEMO_API_START_FAILED");
    }

    private static Process StartStorefront(string root, string local, int apiPort, int sitePort)
    {
        var start = new ProcessStartInfo("npm") { WorkingDirectory = Path.Combine(root, "apps/storefront"), UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        start.ArgumentList.Add("run"); start.ArgumentList.Add("dev"); start.ArgumentList.Add("--"); start.ArgumentList.Add("--port"); start.ArgumentList.Add(sitePort.ToString(System.Globalization.CultureInfo.InvariantCulture));
        start.Environment["M0_HTTPS_CERT"] = Path.Combine(local, "browser/public.pem"); start.Environment["M0_HTTPS_KEY"] = Path.Combine(local, "browser/private.key");
        start.Environment["KOHAMI_M0_LOCAL_DIR"] = local; start.Environment["KOHAMI_API_PORT"] = apiPort.ToString(System.Globalization.CultureInfo.InvariantCulture);
        start.Environment["NUXT_API_ORIGIN"] = $"http://127.0.0.1:{apiPort}"; start.Environment["NUXT_PORT"] = sitePort.ToString(System.Globalization.CultureInfo.InvariantCulture);
        start.Environment["NUXT_SITE_ORIGIN"] = $"https://localhost:{sitePort}"; start.Environment["NUXT_BUILD_DIR"] = Path.Combine(local, "p5-wallet-browser-nuxt");
        return StartQuiet(start, "LOCAL_DEMO_STOREFRONT_START_FAILED");
    }

    private static Process StartQuiet(ProcessStartInfo start, string failure)
    {
        var process = Process.Start(start) ?? throw new InvalidOperationException(failure);
        process.OutputDataReceived += (_, _) => { }; process.ErrorDataReceived += (_, _) => { };
        process.BeginOutputReadLine(); process.BeginErrorReadLine(); return process;
    }

    private static async Task RunPlaywright(string root, string local, int sitePort, string userId, string email, string password, string sku, string browserArtifacts)
    {
        var cli = Path.Combine(root, "apps/storefront/node_modules/@playwright/test/cli.js");
        LocalDemoWalletBrowserRequire(File.Exists(cli), "LOCAL_DEMO_PLAYWRIGHT_DEPENDENCY_REQUIRED");
        var start = new ProcessStartInfo("node") { WorkingDirectory = Path.Combine(root, "apps/storefront"), UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        start.ArgumentList.Add(cli); start.ArgumentList.Add("test"); start.ArgumentList.Add("--config"); start.ArgumentList.Add("playwright.p5-live.config.ts");
        start.ArgumentList.Add("tests/issue-p5-live-wallet.spec.ts"); start.ArgumentList.Add("--reporter=line");
        start.Environment["KOHAMI_STOREFRONT_ORIGIN"] = $"https://localhost:{sitePort}";
        start.Environment["KOHAMI_P5_MEMBER_ID"] = userId; start.Environment["KOHAMI_P5_MEMBER_EMAIL"] = email;
        start.Environment["KOHAMI_P5_MEMBER_PASSWORD"] = password; start.Environment["KOHAMI_P5_SKU"] = sku;
        start.Environment["KOHAMI_P5_EVIDENCE_PATH"] = Path.Combine(local, "p5-wallet-browser-evidence.json");
        start.Environment["KOHAMI_P5_ARTIFACT_DIR"] = browserArtifacts;
        using var process = Process.Start(start) ?? throw new InvalidOperationException("LOCAL_DEMO_PLAYWRIGHT_START_FAILED");
        var capturedOutput = new StringBuilder();
        var capturedError = new StringBuilder();
        var outputLock = new object();
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) lock (outputLock) capturedOutput.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) lock (outputLock) capturedError.AppendLine(e.Data); };
        process.BeginOutputReadLine(); process.BeginErrorReadLine();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(4));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) { process.Kill(true); throw new TimeoutException("LOCAL_DEMO_PLAYWRIGHT_TIMEOUT"); }
        if (process.ExitCode != 0)
        {
            string diagnostics;
            lock (outputLock) diagnostics = capturedOutput.ToString() + capturedError.ToString();
            diagnostics = diagnostics.Replace(email, "[redacted-email]", StringComparison.Ordinal)
                .Replace(password, "[redacted-secret]", StringComparison.Ordinal)
                .Replace(userId, "[redacted-user]", StringComparison.Ordinal)
                .Replace(sku, "[redacted-sku]", StringComparison.Ordinal);
            if (diagnostics.Length > 12000) diagnostics = diagnostics[^12000..];
            Console.Error.WriteLine("PLAYWRIGHT_DIAGNOSTICS_BEGIN");
            Console.Error.WriteLine(diagnostics);
            Console.Error.WriteLine("PLAYWRIGHT_DIAGNOSTICS_END");
        }
        LocalDemoWalletBrowserRequire(process.ExitCode == 0, "LOCAL_DEMO_PLAYWRIGHT_FAILED");
    }

    private static async Task<string> WritePreviewConfig(string source, string origin)
    {
        var payload = JsonNode.Parse(await File.ReadAllTextAsync(source)) as JsonObject ?? throw new InvalidOperationException("LOCAL_DEMO_RUNTIME_CONFIG_INVALID");
        payload["origin"] = origin;
        var path = Path.Combine(Path.GetDirectoryName(source)!, "runtime-p5-browser-" + Guid.NewGuid().ToString("N") + ".json");
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None };
        if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        try
        {
            await using (var stream = new FileStream(path, options))
                await JsonSerializer.SerializeAsync(stream, payload);
            return path;
        }
        catch
        {
            File.Delete(path);
            throw;
        }
    }

    private static async Task WaitForLocalDemoApi(HttpClient client, int port, Process process)
    {
        var uri = new Uri($"http://127.0.0.1:{port}/health");
        for (var attempt = 0; attempt < 90; attempt++)
        {
            if (process.HasExited) throw new InvalidOperationException("LOCAL_DEMO_API_EXITED");
            try { using var response = await client.GetAsync(uri); if (response.IsSuccessStatusCode) return; } catch (HttpRequestException) { }
            await Task.Delay(250);
        }
        throw new TimeoutException("LOCAL_DEMO_API_START_TIMEOUT");
    }

    private static async Task WaitForLocalDemoStorefront(int port, Process process)
    {
        using var handler = new HttpClientHandler { ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator };
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(3) };
        var uri = new Uri($"https://localhost:{port}/en");
        for (var attempt = 0; attempt < 120; attempt++)
        {
            if (process.HasExited) throw new InvalidOperationException("LOCAL_DEMO_STOREFRONT_EXITED");
            try { using var response = await client.GetAsync(uri); if (response.IsSuccessStatusCode) return; } catch (HttpRequestException) { } catch (TaskCanceledException) { }
            await Task.Delay(250);
        }
        throw new TimeoutException("LOCAL_DEMO_STOREFRONT_START_TIMEOUT");
    }

    private static async Task CleanupBrowserActor(string cs, Guid fixtureId, string user, string email, string sku, int originalStock,
        string? cart, string? quote, string? checkout, string? order, LocalDemoWalletHttpAcceptance.FixtureSecurityState securityBefore)
    {
        await using var connection = new SqlConnection(cs); await connection.OpenAsync(); await using var transaction = await connection.BeginTransactionAsync();
        await using var marker = new SqlCommand("IF NOT EXISTS(SELECT 1 FROM ops.DevelopmentFixtureMarker WHERE FixtureId=@f AND DatasetId='demo_m0' AND InstanceFingerprint=LOWER(CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CONVERT(nvarchar(100),SERVERPROPERTY('ServerName')),'|KohamiM0|demo_m0')),2))) THROW 51000,'FIXTURE_MARKER_INVALID',1", connection, (SqlTransaction)transaction);
        marker.Parameters.AddWithValue("@f", fixtureId); await marker.ExecuteNonQueryAsync();
        await using var owned = new SqlCommand(@"DECLARE @orderCount int=(SELECT COUNT(*) FROM m0commerce.Documents WITH(UPDLOCK,HOLDLOCK) WHERE Dataset='demo_m0' AND Kind='Order' AND OwnerId=@u);
IF @orderCount>1 THROW 51000,'LOCAL_DEMO_BROWSER_ORDER_COUNT_INVALID',1;
IF @orderCount=1 BEGIN
 DECLARE @orderId nvarchar(200)=(SELECT Id FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='Order' AND OwnerId=@u);
 DECLARE @checkoutId nvarchar(200)=(SELECT TOP(1) Id FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='Checkout' AND OwnerId=@u AND JSON_VALUE(Body,'$.orderId')=@orderId);
 IF @checkoutId IS NULL OR (SELECT COUNT(*) FROM m0commerce.Reservations WITH(UPDLOCK,HOLDLOCK) WHERE Dataset='demo_m0' AND OrderId=@orderId AND Sku=@sku AND Quantity=1 AND State='Consumed')<>1 THROW 51000,'LOCAL_DEMO_BROWSER_ORDER_LINK_INVALID',1;
 IF (SELECT Stock FROM m0commerce.Inventory WITH(UPDLOCK,HOLDLOCK) WHERE Dataset='demo_m0' AND Sku=@sku)<>@expected-1 THROW 51000,'LOCAL_DEMO_BROWSER_STOCK_DELTA_INVALID',1;
 UPDATE m0commerce.Inventory SET Stock=Stock+1 WHERE Dataset='demo_m0' AND Sku=@sku AND Stock=@expected-1;
 IF @@ROWCOUNT<>1 THROW 51000,'LOCAL_DEMO_BROWSER_STOCK_RESTORE_FAILED',1;
 DELETE m0commerce.RefundUnits WHERE Dataset='demo_m0' AND OrderId=@orderId;
 DELETE m0commerce.Collections WHERE Dataset='demo_m0' AND OrderId=@orderId;
 DELETE m0commerce.Reservations WHERE Dataset='demo_m0' AND OrderId=@orderId;
 DELETE m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='WalletPayment' AND OwnerId=@u AND JSON_VALUE(Body,'$.checkoutId')=@checkoutId;
 DELETE m0commerce.Documents WHERE Dataset='demo_m0' AND Id IN(@orderId,@checkoutId);
END
IF (SELECT Stock FROM m0commerce.Inventory WHERE Dataset='demo_m0' AND Sku=@sku) NOT IN(@expected,@expected-1) THROW 51000,'LOCAL_DEMO_BROWSER_STOCK_DELTA_INVALID',1;
DELETE m0commerce.Idempotency WHERE Dataset='demo_m0' AND Actor=@u;
DELETE m0commerce.Documents WHERE Dataset='demo_m0' AND OwnerId=@u;
DELETE m0commerce.WalletLedger WHERE Dataset='demo_m0' AND UserId=@u;
DELETE m0commerce.WalletAccounts WHERE Dataset='demo_m0' AND UserId=@u;
DELETE m0.SecurityAudit WHERE UserId=@u;
DELETE m0.Sessions WHERE UserId=@u;
DELETE m0.Users WHERE UserId=@u AND Email=@email;", connection, (SqlTransaction)transaction);
        owned.Parameters.AddWithValue("@u", user); owned.Parameters.AddWithValue("@email", email); owned.Parameters.AddWithValue("@sku", sku);
        owned.Parameters.AddWithValue("@expected", originalStock); owned.Parameters.AddWithValue("@cart", (object?)cart ?? DBNull.Value); owned.Parameters.AddWithValue("@quote", (object?)quote ?? DBNull.Value);
        await owned.ExecuteNonQueryAsync();
        await LocalDemoWalletHttpAcceptance.RestoreMaintenanceTarget(connection, (SqlTransaction)transaction, fixtureId, user, securityBefore);
        await transaction.CommitAsync();
    }

    private static async Task VerifyBrowserCleanup(string cs, Guid fixtureId, string user, string sku, int stock)
    {
        await using var connection = new SqlConnection(cs); await connection.OpenAsync();
        await using var command = new SqlCommand("SELECT (SELECT COUNT(*) FROM ops.DevelopmentFixtureMarker WHERE FixtureId=@f AND DatasetId='demo_m0' AND InstanceFingerprint=LOWER(CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CONVERT(nvarchar(100),SERVERPROPERTY('ServerName')),'|KohamiM0|demo_m0')),2))),(SELECT COUNT(*) FROM m0.Users WHERE UserId=@u),(SELECT COUNT(*) FROM m0commerce.WalletLedger WHERE Dataset='demo_m0' AND UserId=@u),(SELECT Stock FROM m0commerce.Inventory WHERE Dataset='demo_m0' AND Sku=@sku)", connection);
        command.Parameters.AddWithValue("@f", fixtureId); command.Parameters.AddWithValue("@u", user); command.Parameters.AddWithValue("@sku", sku);
        await using var reader = await command.ExecuteReaderAsync(); await reader.ReadAsync();
        LocalDemoWalletBrowserRequire(reader.GetInt32(0) == 1 && reader.GetInt32(1) == 0 && reader.GetInt32(2) == 0 && reader.GetInt32(3) == stock, "LOCAL_DEMO_BROWSER_CLEANUP_FAILED");
    }

    private static bool LocalDemoWalletHttpAcceptancePortInUse(int port)
    {
        try { using var socket = new System.Net.Sockets.TcpClient(); var pending = socket.ConnectAsync("127.0.0.1", port); return pending.Wait(250) && socket.Connected; }
        catch { return false; }
    }

    private static void LocalDemoWalletBrowserRequire(bool condition, string code)
    {
        if (!condition) throw new InvalidOperationException(code);
    }
}
