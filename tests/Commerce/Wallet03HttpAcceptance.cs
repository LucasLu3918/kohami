using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Kohami.Core;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;

namespace Kohami.Commerce.Tests;

internal static class Wallet03HttpAcceptance
{
    private const string Prefix = "wallet03_http_";

    public static async Task RunAsync(string repoRoot, string localDirectory, int apiPort)
    {
        var root = Path.GetFullPath(repoRoot);
        var local = Path.GetFullPath(localDirectory);
        Require(string.Equals(local, Path.Combine(root, ".local", "kohami-sql-test", "identity"), StringComparison.Ordinal), "WALLET03_CANONICAL_PROFILE_REQUIRED");
        var runtimePath = Path.Combine(local, "runtime-private.json");
        var provisionerPath = Path.Combine(local, "provisioner-private.json");
        var fixturePath = Path.Combine(local, "fixture-private.json");
        foreach (var path in new[] { runtimePath, provisionerPath, fixturePath })
            Require(File.Exists(path) && !new FileInfo(path).Attributes.HasFlag(FileAttributes.ReparsePoint), "WALLET03_FIXTURE_CONFIG_INVALID");
        Require(apiPort is >= 5080 and <= 5099, "WALLET03_API_PORT_INVALID");

        var (settings, adminConnectionString) = Issue11CartAcceptance.DedicatedAdmin(fixturePath);
        new SqlRuntime(settings).ValidateTls();
        if (OperatingSystem.IsWindows()) throw new InvalidOperationException("WALLET03_PRIVATE_FILE_MODE_UNSUPPORTED");
        using var fixture = JsonDocument.Parse(await File.ReadAllTextAsync(fixturePath));
        using var provisioner = JsonDocument.Parse(await File.ReadAllTextAsync(provisionerPath));
        var fixtureRoot = fixture.RootElement;
        Require(fixtureRoot.TryGetProperty("IdentitySqlTestDedicated", out var identityDedicated) && identityDedicated.ValueKind == JsonValueKind.True
            && fixtureRoot.TryGetProperty("KohamiSqlTestDedicated", out var commerceDedicated) && commerceDedicated.ValueKind == JsonValueKind.True,
            "WALLET03_DEDICATED_FIXTURE_REQUIRED");
        var fixtureId = Guid.Parse(provisioner.RootElement.GetProperty("fixtureId").GetString()!);
        var runtimeBuilder = new SqlConnectionStringBuilder(settings.ConnectionString);
        Require(runtimeBuilder.DataSource.Equals("localhost,14333", StringComparison.OrdinalIgnoreCase)
            && runtimeBuilder.InitialCatalog == "Kohami"
            && runtimeBuilder.Encrypt == SqlConnectionEncryptOption.Strict
            && !runtimeBuilder.TrustServerCertificate
            && fixtureRoot.GetProperty("port").GetInt32() == 14333
            && fixtureRoot.GetProperty("datasetId").GetString() == "kohami_local",
            "WALLET03_SQL_TARGET_INVALID");
        var maintenanceBefore = await ReadinessAndMaintenance(adminConnectionString, fixtureId);
        Require(!maintenanceBefore, "WALLET03_MAINTENANCE_ENABLED");
        Require(!PortInUse(apiPort), "WALLET03_API_PORT_IN_USE");
        Require(File.Exists(Path.Combine(root, "apps/api/bin/Release/net10.0/Api.dll")), "WALLET03_API_BUILD_REQUIRED");

        var suffix = Guid.NewGuid().ToString("N");
        var userId = Prefix + "member_" + suffix;
        var email = Prefix + suffix + "@example.test";
        var supportUserId = Prefix + "support_" + suffix;
        var supportEmail = Prefix + "support_" + suffix + "@example.test";
        var unknownOrderId = Prefix + "unknown_order_" + suffix;
        var unknownCheckoutId = Prefix + "unknown_checkout_" + suffix;
        var unknownAttemptId = Prefix + "unknown_attempt_" + suffix;
        var generatedPassword = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var supportPassword = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var actorCreated = false;
        string? cartId = null, quoteId = null, checkoutId = null, orderId = null, sku = null;
        int startStock = 0;
        Process? api = null;
        var cookies = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            await PrepareActor(adminConnectionString, fixtureId, userId, email, new PasswordHasher<string>().HashPassword(email, generatedPassword));
            actorCreated = true;
            await PrepareActor(adminConnectionString, fixtureId, supportUserId, supportEmail, new PasswordHasher<string>().HashPassword(supportEmail, supportPassword));
            (sku, startStock) = await SelectSkuAndStock(adminConnectionString);

            api = StartApi(root, runtimePath, apiPort);
            using var client = new HttpClient(new HttpClientHandler { UseCookies = false }) { Timeout = TimeSpan.FromSeconds(15) };
            var baseUri = new Uri($"http://127.0.0.1:{apiPort}/api/v1/");
            await WaitForApi(client, new Uri($"http://127.0.0.1:{apiPort}/health"), api);
            var csrfResponse = await Send(client, baseUri, "auth/csrf", HttpMethod.Get, cookies, null, null, null);
            Require(csrfResponse.Status == HttpStatusCode.OK && csrfResponse.Body["requestToken"] is JsonValue, "WALLET03_CSRF_SETUP_FAILED");
            var token = csrfResponse.Body["requestToken"]!.GetValue<string>();

            var anonymousCookies = new Dictionary<string, string>(StringComparer.Ordinal);
            var anonymousCsrf = await Send(client, baseUri, "auth/csrf", HttpMethod.Get, anonymousCookies, null, null, null);
            Require(anonymousCsrf.Status == HttpStatusCode.OK && anonymousCsrf.Body["requestToken"] is JsonValue, "WALLET03_ANONYMOUS_CSRF_SETUP_FAILED");
            var anonymousBefore = await ReadWalletAndStock(adminConnectionString, userId, sku);
            var anonymousCheckout = await Send(client, baseUri, "checkouts", HttpMethod.Post, anonymousCookies,
                CheckoutPayload("quote_anonymous", "cart_anonymous", email), anonymousCsrf.Body["requestToken"]!.GetValue<string>(), settings.Origin,
                idempotency: "wallet03-http-anonymous-" + suffix);
            Require(anonymousCheckout.Status == HttpStatusCode.Unauthorized
                && await ReadWalletAndStock(adminConnectionString, userId, sku) == anonymousBefore
                && await FinancialArtifactCounts(adminConnectionString, userId) == (0, 0, 0, 0, 0),
                "WALLET03_ANONYMOUS_CHECKOUT_NOT_REJECTED_CLEANLY");

            var login = await Send(client, baseUri, "auth/sessions", HttpMethod.Post, cookies,
                new JsonObject { ["email"] = email, ["password"] = generatedPassword }, token, settings.Origin);
            Require(login.Status == HttpStatusCode.OK && login.Body["state"]?.GetValue<string>() == "MemberAuthenticated", "WALLET03_REAL_LOGIN_FAILED");
            Require(cookies.ContainsKey("kohami.session"), "WALLET03_SESSION_COOKIE_MISSING");

            await SeedUnknownExternalPayment(adminConnectionString, userId, unknownOrderId, unknownCheckoutId, unknownAttemptId, email);
            await GrantShipment(adminConnectionString, supportUserId, unknownOrderId, "wallet03-unknown-" + suffix);
            var supportCookies = new Dictionary<string, string>(StringComparer.Ordinal);
            var supportCsrf = await Send(client, baseUri, "auth/csrf", HttpMethod.Get, supportCookies, null, null, null);
            Require(supportCsrf.Status == HttpStatusCode.OK && supportCsrf.Body["requestToken"] is JsonValue, "WALLET03_SUPPORT_CSRF_SETUP_FAILED");
            var supportToken = supportCsrf.Body["requestToken"]!.GetValue<string>();
            var supportLogin = await Send(client, baseUri, "auth/sessions", HttpMethod.Post, supportCookies,
                new JsonObject { ["email"] = supportEmail, ["password"] = supportPassword }, supportToken, settings.Origin);
            Require(supportLogin.Status == HttpStatusCode.OK && supportLogin.Body["state"]?.GetValue<string>() == "AdminAuthenticated", "WALLET03_SUPPORT_LOGIN_FAILED");
            var unknownVersion = CommerceWire.Version("Order", unknownOrderId, 1);
            var unknownShipment = await Send(client, baseUri, $"admin/orders/{unknownOrderId}/shipments", HttpMethod.Post, supportCookies,
                new JsonObject { ["expectedVersion"] = unknownVersion }, supportToken, settings.Origin, unknownVersion,
                "wallet03-unknown-shipment-" + suffix);
            Require((int)unknownShipment.Status == 409 && unknownShipment.Body["code"]?.GetValue<string>() == "INVALID_TRANSITION"
                && await ShipmentCount(adminConnectionString, unknownOrderId) == 0,
                "WALLET03_UNKNOWN_EXTERNAL_PAYMENT_AUTHORIZED_SHIPMENT");

            var cart = await Send(client, baseUri, "carts", HttpMethod.Post, cookies, new JsonObject(), token, settings.Origin,
                idempotency: "wallet03-http-cart-" + suffix);
            Require(cart.Status == HttpStatusCode.Created, "WALLET03_CART_CREATE_FAILED");
            cartId = cart.Body["id"]!.GetValue<string>();
            var cartVersion = cart.Body["version"]!.GetValue<string>();
            var add = await Send(client, baseUri, $"carts/current/items/{sku}", HttpMethod.Put, cookies,
                new JsonObject { ["quantity"] = 1 }, token, settings.Origin, cartVersion, "wallet03-http-item-" + suffix);
            Require(add.Status == HttpStatusCode.OK, "WALLET03_CART_ITEM_FAILED");
            cartVersion = add.Body["version"]!.GetValue<string>();
            var quote = await Send(client, baseUri, "checkout-quotes", HttpMethod.Post, cookies,
                new JsonObject { ["cartVersion"] = cartVersion, ["deliveryMethod"] = "mock_home", ["region"] = "demo_supported" }, token, settings.Origin,
                etag: cartVersion, idempotency: "wallet03-http-quote-" + suffix);
            Require(quote.Status == HttpStatusCode.Created, "WALLET03_QUOTE_FAILED");
            quoteId = quote.Body["id"]!.GetValue<string>();
            var total = quote.Body["snapshot"]!["totalTwd"]!.GetValue<long>();

            // The first request is intentionally insufficient and must leave all financial and stock state unchanged.
            await CreditWallet(adminConnectionString, userId, "synthetic-test-credit:wallet03-http:" + suffix + ":short", total - 1);
            var beforeFailure = await ReadWalletAndStock(adminConnectionString, userId, sku);
            var artifactsBeforeFailure = await FinancialArtifactCounts(adminConnectionString, userId);
            var checkoutPayload = CheckoutPayload(quoteId, cartVersion, email);
            var failed = await Send(client, baseUri, "checkouts", HttpMethod.Post, cookies, checkoutPayload, token, settings.Origin,
                cartVersion, "wallet03-http-failure-" + suffix);
            Require((int)failed.Status == 409 && failed.Body["code"]?.GetValue<string>() == "WALLET_INSUFFICIENT_BALANCE", "WALLET03_INSUFFICIENT_BALANCE_NOT_REJECTED");
            var afterFailure = await ReadWalletAndStock(adminConnectionString, userId, sku);
            var failedArtifacts = await FinancialArtifactCounts(adminConnectionString, userId);
            if (beforeFailure != afterFailure || failedArtifacts != artifactsBeforeFailure)
                Console.Error.WriteLine($"WALLET03_DIAGNOSTIC response={(int)failed.Status}/{failed.Body["code"]?.GetValue<string>() ?? "missing"} walletStock={beforeFailure}->{afterFailure} artifactsBefore=documents:{artifactsBeforeFailure.Documents},collections:{artifactsBeforeFailure.Collections},reservations:{artifactsBeforeFailure.Reservations},refundUnits:{artifactsBeforeFailure.RefundUnits},checkoutDebits:{artifactsBeforeFailure.CheckoutDebits} artifactsAfter=documents:{failedArtifacts.Documents},collections:{failedArtifacts.Collections},reservations:{failedArtifacts.Reservations},refundUnits:{failedArtifacts.RefundUnits},checkoutDebits:{failedArtifacts.CheckoutDebits}");
            Require(beforeFailure == afterFailure, "WALLET03_FAILED_CHECKOUT_BALANCE_OR_STOCK_CHANGED");
            Require(failedArtifacts == artifactsBeforeFailure, "WALLET03_FAILED_CHECKOUT_FINANCIAL_ARTIFACTS_CHANGED");

            await CreditWallet(adminConnectionString, userId, "synthetic-test-credit:wallet03-http:" + suffix + ":topup", 1);
            var successful = await Send(client, baseUri, "checkouts", HttpMethod.Post, cookies, checkoutPayload, token, settings.Origin,
                cartVersion, "wallet03-http-success-" + suffix);
            Require(successful.Status == HttpStatusCode.Created, "WALLET03_HTTP_CHECKOUT_FAILED");
            checkoutId = successful.Body["checkoutId"]!.GetValue<string>();
            orderId = successful.Body["orderId"]!.GetValue<string>();
            var status = await Send(client, baseUri, $"checkouts/{checkoutId}/status", HttpMethod.Get, cookies, null, null, null);
            Require(status.Status == HttpStatusCode.OK && status.Body["orderState"]?.GetValue<string>() == "Confirmed"
                && status.Body["paymentState"]?.GetValue<string>() == "Succeeded", "WALLET03_CHECKOUT_STATUS_MISMATCH");
            var persisted = await ReadCheckoutState(adminConnectionString, userId, sku, checkoutId, orderId, total);
            Require(persisted.Balance == 0 && persisted.Debits == 1 && persisted.DebitAmount == total
                && persisted.DebitSource == checkoutId && persisted.WalletPaymentCount == 1 && persisted.PaymentAmount == total
                && persisted.CheckoutCount == 1 && persisted.CheckoutOrderId == orderId
                && persisted.OrderCount == 1 && persisted.CollectionCount == 1 && persisted.Collected == total
                && persisted.Stock == startStock - 1 && persisted.OrderState == "Confirmed" && persisted.PaymentState == "Succeeded",
                "WALLET03_HTTP_CHECKOUT_SQL_STATE_INVALID");
            await GrantShipment(adminConnectionString, supportUserId, orderId, "wallet03-wallet-" + suffix);
            var walletOrderVersion = CommerceWire.Version("Order", orderId, 1);
            var walletShipment = await Send(client, baseUri, $"admin/orders/{orderId}/shipments", HttpMethod.Post, supportCookies,
                new JsonObject { ["expectedVersion"] = walletOrderVersion }, supportToken, settings.Origin, walletOrderVersion,
                "wallet03-wallet-shipment-" + suffix);
            Require(walletShipment.Status == HttpStatusCode.Created && walletShipment.Body["state"]?.GetValue<string>() == "Pending"
                && await ShipmentCount(adminConnectionString, orderId) == 1
                && await WalletShipmentState(adminConnectionString, userId, checkoutId, orderId) == ("Confirmed", "Succeeded", 1, total, 1),
                "WALLET03_WALLET_ORDER_FULFILLMENT_NOT_AUTHORIZED_OR_INCONSISTENT");
            var logout = await Send(client, baseUri, "auth/session", HttpMethod.Delete, cookies, null, token, settings.Origin);
            Require(logout.Status == HttpStatusCode.NoContent && !cookies.ContainsKey("kohami.session"), "WALLET03_LOGOUT_COOKIE_NOT_CLEARED");
            var supportLogout = await Send(client, baseUri, "auth/session", HttpMethod.Delete, supportCookies, null, supportToken, settings.Origin);
            Require(supportLogout.Status == HttpStatusCode.NoContent && !supportCookies.ContainsKey("kohami.session"), "WALLET03_SUPPORT_LOGOUT_COOKIE_NOT_CLEARED");
            Console.WriteLine("PASS wallet03 real HTTP anonymous denial, wallet checkout, Unknown payment shipment rejection, wallet fulfillment, and SQL assertions on dedicated fixture");
        }
        finally
        {
            if (cookies.ContainsKey("kohami.session"))
            {
                try { using var logoutClient = new HttpClient(new HttpClientHandler { UseCookies = false }); await Send(logoutClient, new Uri($"http://127.0.0.1:{apiPort}/api/v1/"), "auth/session", HttpMethod.Delete, cookies, null, "cleanup", settings.Origin); } catch { }
            }
            if (api is { HasExited: false }) { api.Kill(true); await api.WaitForExitAsync(); }
            if (actorCreated) await Cleanup(adminConnectionString, fixtureId, userId, email, supportUserId, supportEmail,
                cartId, quoteId, checkoutId, orderId, unknownOrderId, unknownCheckoutId, unknownAttemptId, sku, startStock);
            Require(await ReadMaintenance(adminConnectionString) == maintenanceBefore, "WALLET03_MAINTENANCE_CHANGED");
        }
    }

    private static JsonObject CheckoutPayload(string quote, string version, string email) => new()
    {
        ["quoteId"] = quote, ["cartVersion"] = version, ["contact"] = new JsonObject { ["email"] = email },
        ["delivery"] = new JsonObject { ["method"] = "mock_home", ["region"] = "demo_supported" }, ["confirmed"] = true,
        ["paymentMethod"] = "wallet",
    };

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

    private static async Task PrepareActor(string cs, Guid fixtureId, string user, string email, string hash)
    {
        await using var c = new SqlConnection(cs); await c.OpenAsync();
        await using var marker = new SqlCommand("SELECT COUNT(*) FROM ops.DevelopmentFixtureMarker WHERE FixtureId=@id AND DatasetId='kohami_local' AND InstanceFingerprint=LOWER(CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CONVERT(nvarchar(100),SERVERPROPERTY('ServerName')),'|Kohami|kohami_local')),2))", c);
        marker.Parameters.AddWithValue("@id", fixtureId); Require(Convert.ToInt32(await marker.ExecuteScalarAsync()) == 1, "WALLET03_FIXTURE_MARKER_INVALID");
        await using var insert = new SqlCommand("INSERT identity_access.Users(UserId,Email,PasswordHash,EmailConfirmed,Status,SecurityVersion) VALUES(@u,@e,@p,1,'Active',1)", c);
        insert.Parameters.AddWithValue("@u", user); insert.Parameters.AddWithValue("@e", email); insert.Parameters.AddWithValue("@p", hash); await insert.ExecuteNonQueryAsync();
    }

    private static async Task<(string Sku, int Stock)> SelectSkuAndStock(string cs)
    {
        await using var c = new SqlConnection(cs); await c.OpenAsync();
        await using var cmd = new SqlCommand("SELECT TOP(1) Sku,Stock FROM commerce.Inventory WHERE Dataset='kohami_local' AND Active=1 AND Stock>=2 AND Price BETWEEN 1 AND 49900 ORDER BY Sku", c);
        await using var r = await cmd.ExecuteReaderAsync(); Require(await r.ReadAsync(), "WALLET03_STOCK_UNAVAILABLE"); return (r.GetString(0), r.GetInt32(1));
    }

    private static async Task CreditWallet(string cs, string user, string source, long amount)
    {
        await using var c = new SqlConnection(cs); await c.OpenAsync(); await using var tx = await c.BeginTransactionAsync();
        await using var cmd = new SqlCommand("EXEC commerce.wallet_synthetic_credit @UserId,@SourceId,@AmountTwd", c, (SqlTransaction)tx);
        cmd.Parameters.AddWithValue("@UserId", user); cmd.Parameters.AddWithValue("@SourceId", source); cmd.Parameters.AddWithValue("@AmountTwd", amount); await cmd.ExecuteNonQueryAsync(); await tx.CommitAsync();
    }

    private static async Task SeedUnknownExternalPayment(string cs, string owner, string order, string checkout, string attempt, string email)
    {
        var now = DateTimeOffset.UtcNow;
        var snapshot = new JsonObject { ["currency"] = "TWD", ["totalTwd"] = 100, ["shippingTwd"] = 0, ["lines"] = new JsonArray() };
        var orderBody = new JsonObject { ["id"] = order, ["orderId"] = order, ["checkoutId"] = checkout, ["mode"] = "mock",
            ["orderState"] = "AwaitingPayment", ["paymentState"] = "Unknown", ["compensationRequired"] = false,
            ["email"] = email, ["placedAt"] = now, ["snapshot"] = snapshot.DeepClone() };
        var checkoutBody = new JsonObject { ["checkoutId"] = checkout, ["orderId"] = order, ["mode"] = "mock",
            ["orderState"] = "AwaitingPayment", ["paymentState"] = "Unknown", ["compensationRequired"] = false,
            ["expiresAt"] = now.AddMinutes(10), ["snapshot"] = snapshot.DeepClone() };
        var attemptBody = new JsonObject { ["id"] = attempt, ["checkoutId"] = checkout, ["mode"] = "mock", ["state"] = "Unknown", ["amountTwd"] = 100 };
        await using var c = new SqlConnection(cs); await c.OpenAsync();
        await using var cmd = new SqlCommand("INSERT commerce.Documents(Dataset,Kind,Id,OwnerId,Version,Body) VALUES('kohami_local','Order',@o,@u,1,@ob),('kohami_local','Checkout',@c,@u,1,@cb),('kohami_local','PaymentAttempt',@p,@u,1,@pb)", c);
        cmd.Parameters.AddWithValue("@o", order); cmd.Parameters.AddWithValue("@c", checkout); cmd.Parameters.AddWithValue("@p", attempt); cmd.Parameters.AddWithValue("@u", owner);
        cmd.Parameters.AddWithValue("@ob", orderBody.ToJsonString()); cmd.Parameters.AddWithValue("@cb", checkoutBody.ToJsonString()); cmd.Parameters.AddWithValue("@pb", attemptBody.ToJsonString());
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task GrantShipment(string cs, string support, string order, string suffix)
    {
        await using var c = new SqlConnection(cs); await c.OpenAsync();
        await using var cmd = new SqlCommand("INSERT identity_access.Grants(GrantId,UserId,Role,Permission,ScopeKind,ScopeId,ValidFrom,ValidUntil,GrantedBy,Reason) VALUES(@id,@u,'CustomerSupport','shipment.manage','Order',@o,DATEADD(minute,-1,SYSDATETIMEOFFSET()),DATEADD(hour,1,SYSDATETIMEOFFSET()),'fixture','wallet03 acceptance shipment authorization')", c);
        cmd.Parameters.AddWithValue("@id", "grant_wallet03_" + suffix); cmd.Parameters.AddWithValue("@u", support); cmd.Parameters.AddWithValue("@o", order); await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<int> ShipmentCount(string cs, string order)
    {
        await using var c = new SqlConnection(cs); await c.OpenAsync();
        await using var cmd = new SqlCommand("SELECT COUNT(*) FROM commerce.Documents WHERE Dataset='kohami_local' AND Kind='Shipment' AND JSON_VALUE(Body,'$.orderId')=@o", c);
        cmd.Parameters.AddWithValue("@o", order); return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    private static async Task<(string OrderState, string PaymentState, int Collections, long Collected, int WalletPayments)> WalletShipmentState(string cs, string user, string checkout, string order)
    {
        await using var c = new SqlConnection(cs); await c.OpenAsync();
        await using var cmd = new SqlCommand("SELECT JSON_VALUE(o.Body,'$.orderState'),JSON_VALUE(o.Body,'$.paymentState'),(SELECT COUNT(*) FROM commerce.Collections WHERE Dataset='kohami_local' AND OrderId=@o AND Kind='Primary' AND PaymentMethod='Wallet'),(SELECT ISNULL(SUM(Collected),0) FROM commerce.Collections WHERE Dataset='kohami_local' AND OrderId=@o AND Kind='Primary' AND PaymentMethod='Wallet'),(SELECT COUNT(*) FROM commerce.Documents WHERE Dataset='kohami_local' AND Kind='WalletPayment' AND OwnerId=@u AND JSON_VALUE(Body,'$.checkoutId')=@c) FROM commerce.Documents o WHERE o.Dataset='kohami_local' AND o.Kind='Order' AND o.Id=@o", c);
        cmd.Parameters.AddWithValue("@u", user); cmd.Parameters.AddWithValue("@c", checkout); cmd.Parameters.AddWithValue("@o", order);
        await using var r = await cmd.ExecuteReaderAsync(); Require(await r.ReadAsync(), "WALLET03_WALLET_ORDER_READBACK_MISSING");
        return (r.GetString(0), r.GetString(1), r.GetInt32(2), r.GetInt64(3), r.GetInt32(4));
    }

    private static async Task<(long Balance, int Debits, long DebitAmount, string DebitSource, int WalletPaymentCount, long PaymentAmount, int CheckoutCount, string CheckoutOrderId, int OrderCount, int CollectionCount, long Collected, int Stock, string OrderState, string PaymentState)> ReadCheckoutState(string cs, string user, string sku, string checkout, string order, long total)
    {
        await using var c = new SqlConnection(cs); await c.OpenAsync();
        await using var cmd = new SqlCommand("SELECT w.AvailableTwd,(SELECT COUNT(*) FROM commerce.WalletLedger WHERE Dataset='kohami_local' AND UserId=@u AND EntryType='CheckoutDebit'),ISNULL((SELECT AmountTwd FROM commerce.WalletLedger WHERE Dataset='kohami_local' AND UserId=@u AND EntryType='CheckoutDebit' AND SourceId=@c),0),ISNULL((SELECT SourceId FROM commerce.WalletLedger WHERE Dataset='kohami_local' AND UserId=@u AND EntryType='CheckoutDebit' AND SourceId=@c),''),(SELECT COUNT(*) FROM commerce.Documents WHERE Dataset='kohami_local' AND Kind='WalletPayment' AND OwnerId=@u AND JSON_VALUE(Body,'$.checkoutId')=@c),ISNULL((SELECT TRY_CONVERT(bigint,JSON_VALUE(Body,'$.amountTwd')) FROM commerce.Documents WHERE Dataset='kohami_local' AND Kind='WalletPayment' AND OwnerId=@u AND JSON_VALUE(Body,'$.checkoutId')=@c),0),(SELECT COUNT(*) FROM commerce.Documents WHERE Dataset='kohami_local' AND Kind='Checkout' AND Id=@c),ISNULL((SELECT JSON_VALUE(Body,'$.orderId') FROM commerce.Documents WHERE Dataset='kohami_local' AND Kind='Checkout' AND Id=@c),''),(SELECT COUNT(*) FROM commerce.Documents WHERE Dataset='kohami_local' AND Kind='Order' AND Id=@o),(SELECT COUNT(*) FROM commerce.Collections WHERE Dataset='kohami_local' AND OrderId=@o),ISNULL((SELECT SUM(Collected) FROM commerce.Collections WHERE Dataset='kohami_local' AND OrderId=@o),0),(SELECT Stock FROM commerce.Inventory WHERE Dataset='kohami_local' AND Sku=@s),JSON_VALUE(o.Body,'$.orderState'),JSON_VALUE(o.Body,'$.paymentState') FROM commerce.WalletAccounts w JOIN commerce.Documents o ON o.Dataset=w.Dataset AND o.Kind='Order' AND o.Id=@o WHERE w.Dataset='kohami_local' AND w.UserId=@u", c);
        cmd.Parameters.AddWithValue("@u", user); cmd.Parameters.AddWithValue("@s", sku); cmd.Parameters.AddWithValue("@o", order); cmd.Parameters.AddWithValue("@c", checkout);
        await using var r = await cmd.ExecuteReaderAsync(); Require(await r.ReadAsync(), "WALLET03_SQL_ORDER_MISSING");
        Require(r.GetInt64(2) == total && r.GetInt64(5) == total, "WALLET03_SQL_PAYMENT_AMOUNT_INVALID");
        return (r.GetInt64(0), r.GetInt32(1), r.GetInt64(2), r.GetString(3), r.GetInt32(4), r.GetInt64(5), r.GetInt32(6), r.GetString(7), r.GetInt32(8), r.GetInt32(9), r.GetInt64(10), r.GetInt32(11), r.GetString(12), r.GetString(13));
    }

    private static async Task<(long Balance, int Debits, int Stock)> ReadWalletAndStock(string cs, string user, string sku)
    {
        await using var c = new SqlConnection(cs); await c.OpenAsync();
        await using var cmd = new SqlCommand("SELECT ISNULL((SELECT AvailableTwd FROM commerce.WalletAccounts WHERE Dataset='kohami_local' AND UserId=@u),0),(SELECT COUNT(*) FROM commerce.WalletLedger WHERE Dataset='kohami_local' AND UserId=@u AND EntryType='CheckoutDebit'),(SELECT Stock FROM commerce.Inventory WHERE Dataset='kohami_local' AND Sku=@s)", c);
        cmd.Parameters.AddWithValue("@u", user); cmd.Parameters.AddWithValue("@s", sku); await using var r = await cmd.ExecuteReaderAsync(); await r.ReadAsync(); return (r.GetInt64(0), r.GetInt32(1), r.GetInt32(2));
    }

    private static async Task<(int Documents, int Collections, int Reservations, int RefundUnits, int CheckoutDebits)> FinancialArtifactCounts(string cs, string user)
    {
        await using var c = new SqlConnection(cs); await c.OpenAsync();
        await using var cmd = new SqlCommand("""
            SELECT
              (SELECT COUNT(*) FROM commerce.Documents WHERE Dataset='kohami_local' AND Kind IN('Order','Checkout','WalletPayment','PaymentAttempt') AND OwnerId=@u),
              (SELECT COUNT(*) FROM commerce.Collections WHERE Dataset='kohami_local' AND OrderId IN(SELECT Id FROM commerce.Documents WHERE Dataset='kohami_local' AND Kind='Order' AND OwnerId=@u)),
              (SELECT COUNT(*) FROM commerce.Reservations WHERE Dataset='kohami_local' AND OrderId IN(SELECT Id FROM commerce.Documents WHERE Dataset='kohami_local' AND Kind='Order' AND OwnerId=@u)),
              (SELECT COUNT(*) FROM commerce.RefundUnits WHERE Dataset='kohami_local' AND OrderId IN(SELECT Id FROM commerce.Documents WHERE Dataset='kohami_local' AND Kind='Order' AND OwnerId=@u)),
              (SELECT COUNT(*) FROM commerce.WalletLedger WHERE Dataset='kohami_local' AND UserId=@u AND EntryType='CheckoutDebit')
            """, c);
        cmd.Parameters.AddWithValue("@u", user); await using var r = await cmd.ExecuteReaderAsync(); Require(await r.ReadAsync(), "WALLET03_FINANCIAL_ARTIFACT_READBACK_MISSING");
        return (r.GetInt32(0), r.GetInt32(1), r.GetInt32(2), r.GetInt32(3), r.GetInt32(4));
    }

    private static async Task Cleanup(string cs, Guid fixtureId, string user, string email, string supportUser, string supportEmail,
        string? cart, string? quote, string? checkout, string? order, string unknownOrder, string unknownCheckout, string unknownAttempt, string? sku, int stockBefore)
    {
        await using (var c = new SqlConnection(cs))
        {
            await c.OpenAsync(); await using var tx = await c.BeginTransactionAsync();
            await using var guard = new SqlCommand("IF NOT EXISTS(SELECT 1 FROM ops.DevelopmentFixtureMarker WHERE FixtureId=@f AND DatasetId='kohami_local' AND InstanceFingerprint=LOWER(CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CONVERT(nvarchar(100),SERVERPROPERTY('ServerName')),'|Kohami|kohami_local')),2))) THROW 51000,'FIXTURE_MARKER_INVALID',1", c, (SqlTransaction)tx);
            guard.Parameters.AddWithValue("@f", fixtureId); await guard.ExecuteNonQueryAsync();
            if (cart is not null)
            {
                await using var lookup = new SqlCommand("SELECT TOP(1) JSON_VALUE(Response,'$.checkoutId'),JSON_VALUE(Response,'$.orderId') FROM commerce.Idempotency WHERE Dataset='kohami_local' AND Actor=@u AND Scope='createCheckout/'+@cart", c, (SqlTransaction)tx);
                lookup.Parameters.AddWithValue("@u", user); lookup.Parameters.AddWithValue("@cart", cart);
                await using var reader = await lookup.ExecuteReaderAsync();
                if (await reader.ReadAsync()) { if (checkout is null && !reader.IsDBNull(0)) checkout = reader.GetString(0); if (order is null && !reader.IsDBNull(1)) order = reader.GetString(1); }
            }
            await using (var removeCommerce = new SqlCommand("""
                DELETE commerce.Documents WHERE Dataset='kohami_local' AND Kind='Shipment' AND JSON_VALUE(Body,'$.orderId') IN(@o,@uo);
                DELETE commerce.RefundUnits WHERE Dataset='kohami_local' AND OrderId IN(@o,@uo);
                DELETE commerce.Collections WHERE Dataset='kohami_local' AND OrderId IN(@o,@uo);
                DELETE commerce.Reservations WHERE Dataset='kohami_local' AND OrderId IN(@o,@uo);
                DELETE commerce.Documents WHERE Dataset='kohami_local' AND (OwnerId IN(@u,@su) OR Id IN(@o,@c,@uo,@uc,@pa,@cart,@quote));
                DELETE commerce.Idempotency WHERE Dataset='kohami_local' AND Actor IN(@u,@su);
                DELETE commerce.WalletLedger WHERE Dataset='kohami_local' AND UserId=@u;
                DELETE commerce.WalletAccounts WHERE Dataset='kohami_local' AND UserId=@u;
                DELETE identity_access.Grants WHERE UserId IN(@u,@su);
                DELETE identity_access.SecurityAudit WHERE UserId IN(@u,@su);
                DELETE identity_access.Sessions WHERE UserId IN(@u,@su);
                DELETE identity_access.AuthFailures WHERE AccountKey IN(LOWER(CONVERT(varchar(64),HASHBYTES('SHA2_256',LOWER(@e)),2)),LOWER(CONVERT(varchar(64),HASHBYTES('SHA2_256',LOWER(@se)),2)));
                DELETE identity_access.Users WHERE (UserId=@u AND Email=@e) OR (UserId=@su AND Email=@se);
                """, c, (SqlTransaction)tx))
            {
                removeCommerce.Parameters.AddWithValue("@u", user); removeCommerce.Parameters.AddWithValue("@e", email);
                removeCommerce.Parameters.AddWithValue("@su", supportUser); removeCommerce.Parameters.AddWithValue("@se", supportEmail);
                removeCommerce.Parameters.AddWithValue("@o", (object?)order ?? DBNull.Value); removeCommerce.Parameters.AddWithValue("@c", (object?)checkout ?? DBNull.Value);
                removeCommerce.Parameters.AddWithValue("@uo", unknownOrder); removeCommerce.Parameters.AddWithValue("@uc", unknownCheckout);
                removeCommerce.Parameters.AddWithValue("@pa", unknownAttempt); removeCommerce.Parameters.AddWithValue("@cart", (object?)cart ?? DBNull.Value);
                removeCommerce.Parameters.AddWithValue("@quote", (object?)quote ?? DBNull.Value); await removeCommerce.ExecuteNonQueryAsync();
            }
            if (sku is not null)
            {
                await using var restoreStock = new SqlCommand("UPDATE commerce.Inventory SET Stock=@stock WHERE Dataset='kohami_local' AND Sku=@s AND Stock IN(@stock,@afterCheckout);", c, (SqlTransaction)tx);
                restoreStock.Parameters.AddWithValue("@stock", stockBefore); restoreStock.Parameters.AddWithValue("@afterCheckout", stockBefore - 1); restoreStock.Parameters.AddWithValue("@s", sku);
                Require(await restoreStock.ExecuteNonQueryAsync() == 1, "WALLET03_STOCK_RESTORE_CONFLICT");
            }
            await tx.CommitAsync();
        }

        await using (var c = new SqlConnection(cs))
        {
            await c.OpenAsync();
            await using var marker = new SqlCommand("SELECT COUNT(*) FROM ops.DevelopmentFixtureMarker WHERE FixtureId=@f AND DatasetId='kohami_local' AND InstanceFingerprint=LOWER(CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CONVERT(nvarchar(100),SERVERPROPERTY('ServerName')),'|Kohami|kohami_local')),2))", c);
            marker.Parameters.AddWithValue("@f", fixtureId); Require(Convert.ToInt32(await marker.ExecuteScalarAsync()) == 1, "WALLET03_CLEANUP_FIXTURE_MARKER_INVALID");
            await using var readback = new SqlCommand("""
                SELECT
                  (SELECT COUNT(*) FROM identity_access.Users WHERE UserId IN(@u,@su)) AS UsersLeft,
                  (SELECT COUNT(*) FROM identity_access.Grants WHERE UserId IN(@u,@su)) AS GrantsLeft,
                  (SELECT COUNT(*) FROM identity_access.Sessions WHERE UserId IN(@u,@su)) AS SessionsLeft,
                  (SELECT COUNT(*) FROM identity_access.SecurityAudit WHERE UserId IN(@u,@su)) AS AuditLeft,
                  (SELECT COUNT(*) FROM identity_access.AuthFailures WHERE AccountKey IN(LOWER(CONVERT(varchar(64),HASHBYTES('SHA2_256',LOWER(@e)),2)),LOWER(CONVERT(varchar(64),HASHBYTES('SHA2_256',LOWER(@se)),2)))) AS AuthFailuresLeft,
                  (SELECT COUNT(*) FROM commerce.Documents WHERE Dataset='kohami_local' AND (OwnerId IN(@u,@su) OR Id IN(@o,@c,@uo,@uc,@pa,@cart,@quote))) AS DocumentsLeft,
                  (SELECT COUNT(*) FROM commerce.Collections WHERE Dataset='kohami_local' AND OrderId IN(@o,@uo)) AS CollectionsLeft,
                  (SELECT COUNT(*) FROM commerce.Reservations WHERE Dataset='kohami_local' AND OrderId IN(@o,@uo)) AS ReservationsLeft,
                  (SELECT COUNT(*) FROM commerce.RefundUnits WHERE Dataset='kohami_local' AND OrderId IN(@o,@uo)) AS RefundUnitsLeft,
                  (SELECT COUNT(*) FROM commerce.Idempotency WHERE Dataset='kohami_local' AND Actor IN(@u,@su)) AS IdempotencyLeft,
                  (SELECT COUNT(*) FROM commerce.WalletLedger WHERE Dataset='kohami_local' AND UserId=@u) AS WalletLedgerLeft,
                  (SELECT COUNT(*) FROM commerce.WalletAccounts WHERE Dataset='kohami_local' AND UserId=@u) AS WalletAccountsLeft,
                  (SELECT Stock FROM commerce.Inventory WHERE Dataset='kohami_local' AND Sku=@s) AS StockAfterCleanup;
                """, c);
            readback.Parameters.AddWithValue("@u", user); readback.Parameters.AddWithValue("@e", email); readback.Parameters.AddWithValue("@su", supportUser); readback.Parameters.AddWithValue("@se", supportEmail);
            readback.Parameters.AddWithValue("@o", (object?)order ?? DBNull.Value); readback.Parameters.AddWithValue("@c", (object?)checkout ?? DBNull.Value);
            readback.Parameters.AddWithValue("@uo", unknownOrder); readback.Parameters.AddWithValue("@uc", unknownCheckout); readback.Parameters.AddWithValue("@pa", unknownAttempt);
            readback.Parameters.AddWithValue("@cart", (object?)cart ?? DBNull.Value); readback.Parameters.AddWithValue("@quote", (object?)quote ?? DBNull.Value); readback.Parameters.AddWithValue("@s", (object?)sku ?? DBNull.Value);
            await using var reader = await readback.ExecuteReaderAsync(); Require(await reader.ReadAsync(), "WALLET03_CLEANUP_READBACK_MISSING");
            for (var column = 0; column < 12; column++) Require(reader.GetInt32(column) == 0, "WALLET03_CLEANUP_RESIDUE_DETECTED");
            if (sku is not null) Require(!reader.IsDBNull(12) && reader.GetInt32(12) == stockBefore, "WALLET03_STOCK_RESTORE_READBACK_FAILED");
        }
        Console.WriteLine("PASS Wallet03 exact synthetic cleanup: member/support users, grants, sessions, audit/auth failures, documents, commerce rows, all actor idempotency, and SKU stock SQL readback");
    }

    private static async Task<bool> ReadinessAndMaintenance(string cs, Guid fixtureId)
    {
        await using var c = new SqlConnection(cs); await c.OpenAsync();
        await using var cmd = new SqlCommand("SELECT Maintenance FROM identity_access.SecurityState WHERE Id=1 AND EXISTS(SELECT 1 FROM ops.DevelopmentFixtureMarker WHERE FixtureId=@f AND DatasetId='kohami_local' AND InstanceFingerprint=LOWER(CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CONVERT(nvarchar(100),SERVERPROPERTY('ServerName')),'|Kohami|kohami_local')),2)))", c);
        cmd.Parameters.AddWithValue("@f", fixtureId); var value = await cmd.ExecuteScalarAsync(); Require(value is not null, "WALLET03_FIXTURE_MARKER_INVALID"); return Convert.ToBoolean(value);
    }
    private static async Task<bool> ReadMaintenance(string cs) { await using var c = new SqlConnection(cs); await c.OpenAsync(); await using var cmd = new SqlCommand("SELECT Maintenance FROM identity_access.SecurityState WHERE Id=1", c); return Convert.ToBoolean(await cmd.ExecuteScalarAsync()); }
    private static Process StartApi(string root, string config, int port)
    {
        var dll = Path.Combine(root, "apps/api/bin/Release/net10.0/Api.dll"); Require(File.Exists(dll), "WALLET03_API_BUILD_REQUIRED");
        var start = new ProcessStartInfo("/usr/local/share/dotnet/dotnet", dll) { WorkingDirectory = root, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        start.Environment["KOHAMI_CONFIG"] = config; start.Environment["KOHAMI_API_PORT"] = port.ToString(System.Globalization.CultureInfo.InvariantCulture);
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
