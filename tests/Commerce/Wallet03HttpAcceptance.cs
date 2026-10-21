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

internal static class Wallet03HttpAcceptance
{
    private const string Prefix = "wallet03_http_";

    public static async Task RunAsync(string repoRoot, string localDirectory, int apiPort)
    {
        var root = Path.GetFullPath(repoRoot);
        var local = Path.GetFullPath(localDirectory);
        Require(local.StartsWith(Path.Combine(root, ".local") + Path.DirectorySeparatorChar, StringComparison.Ordinal), "WALLET03_LOCAL_DIR_SCOPE_INVALID");
        var runtimePath = Path.Combine(local, "runtime-private.json");
        var provisionerPath = Path.Combine(local, "provisioner-private.json");
        var fixturePath = Path.Combine(local, "fixture-private.json");
        foreach (var path in new[] { runtimePath, provisionerPath, fixturePath })
            Require(File.Exists(path) && !new FileInfo(path).Attributes.HasFlag(FileAttributes.ReparsePoint), "WALLET03_FIXTURE_CONFIG_INVALID");
        Require(apiPort is >= 5080 and <= 5099, "WALLET03_API_PORT_INVALID");

        var settings = M0Settings.Load(runtimePath);
        new M0Sql(settings).ValidateTls();
        if (OperatingSystem.IsWindows()) throw new InvalidOperationException("WALLET03_PRIVATE_FILE_MODE_UNSUPPORTED");
        using var fixture = JsonDocument.Parse(await File.ReadAllTextAsync(fixturePath));
        using var provisioner = JsonDocument.Parse(await File.ReadAllTextAsync(provisionerPath));
        var fixtureRoot = fixture.RootElement;
        Require(fixtureRoot.TryGetProperty("Wallet03HttpDedicated", out var dedicated) && dedicated.ValueKind == JsonValueKind.True,
            "WALLET03_DEDICATED_FIXTURE_REQUIRED");
        var fixtureId = Guid.Parse(provisioner.RootElement.GetProperty("fixtureId").GetString()!);
        var runtimeBuilder = new SqlConnectionStringBuilder(settings.ConnectionString);
        Require(runtimeBuilder.DataSource.Equals($"localhost,{fixtureRoot.GetProperty("port").GetInt32()}", StringComparison.OrdinalIgnoreCase)
            && fixtureRoot.GetProperty("port").GetInt32() != 14349
            && runtimeBuilder.InitialCatalog == "KohamiM0"
            && runtimeBuilder.Encrypt == SqlConnectionEncryptOption.Strict
            && !runtimeBuilder.TrustServerCertificate,
            "WALLET03_SQL_TARGET_INVALID");
        var fixturePort = fixtureRoot.GetProperty("port").GetInt32();
        Require(fixturePort is >= 14336 and <= 14399 && fixturePort != 14349, "WALLET03_SQL_PORT_INVALID");
        var migratorCredential = provisioner.RootElement.GetProperty("migrator").GetString();
        var admin = new SqlConnectionStringBuilder(settings.ConnectionString)
        {
            DataSource = $"localhost,{fixturePort}", InitialCatalog = "KohamiM0", UserID = "kohami_m0_migrator",
            Encrypt = SqlConnectionEncryptOption.Strict,
            TrustServerCertificate = false, HostNameInCertificate = settings.SqlServerName,
        };
        admin["Password"] = migratorCredential;
        admin["Server Certificate"] = settings.SqlCertificatePath;
        var maintenanceBefore = await ReadinessAndMaintenance(admin.ConnectionString, fixtureId);
        Require(!maintenanceBefore, "WALLET03_MAINTENANCE_ENABLED");
        Require(!PortInUse(apiPort), "WALLET03_API_PORT_IN_USE");
        Require(File.Exists(Path.Combine(root, "apps/api/bin/Release/net10.0/Api.dll")), "WALLET03_API_BUILD_REQUIRED");

        var suffix = Guid.NewGuid().ToString("N");
        var userId = Prefix + "member_" + suffix;
        var email = Prefix + suffix + "@example.test";
        var generatedPassword = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var actorCreated = false;
        string? cartId = null, quoteId = null, checkoutId = null, orderId = null, sku = null;
        int startStock = 0;
        Process? api = null;
        var cookies = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            await PrepareActor(admin.ConnectionString, fixtureId, userId, email, new PasswordHasher<string>().HashPassword(email, generatedPassword));
            actorCreated = true;
            (sku, startStock) = await SelectSkuAndStock(admin.ConnectionString);

            api = StartApi(root, runtimePath, apiPort);
            using var client = new HttpClient(new HttpClientHandler { UseCookies = false }) { Timeout = TimeSpan.FromSeconds(15) };
            var baseUri = new Uri($"http://127.0.0.1:{apiPort}/api/v1/");
            await WaitForApi(client, new Uri($"http://127.0.0.1:{apiPort}/health"), api);
            var csrfResponse = await Send(client, baseUri, "auth/csrf", HttpMethod.Get, cookies, null, null, null);
            Require(csrfResponse.Status == HttpStatusCode.OK && csrfResponse.Body["requestToken"] is JsonValue, "WALLET03_CSRF_SETUP_FAILED");
            var token = csrfResponse.Body["requestToken"]!.GetValue<string>();
            var login = await Send(client, baseUri, "auth/sessions", HttpMethod.Post, cookies,
                new JsonObject { ["email"] = email, ["password"] = generatedPassword }, token, settings.Origin);
            Require(login.Status == HttpStatusCode.OK && login.Body["state"]?.GetValue<string>() == "MemberAuthenticated", "WALLET03_REAL_LOGIN_FAILED");
            Require(cookies.ContainsKey("kohami.session"), "WALLET03_SESSION_COOKIE_MISSING");

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
            await CreditWallet(admin.ConnectionString, userId, "synthetic-test-credit:wallet03-http:" + suffix + ":short", total - 1);
            var beforeFailure = await ReadWalletAndStock(admin.ConnectionString, userId, sku);
            var checkoutPayload = CheckoutPayload(quoteId, cartVersion, email);
            var failed = await Send(client, baseUri, "checkouts", HttpMethod.Post, cookies, checkoutPayload, token, settings.Origin,
                cartVersion, "wallet03-http-failure-" + suffix);
            Require((int)failed.Status == 409 && failed.Body["code"]?.GetValue<string>() == "WALLET_INSUFFICIENT_BALANCE", "WALLET03_INSUFFICIENT_BALANCE_NOT_REJECTED");
            var afterFailure = await ReadWalletAndStock(admin.ConnectionString, userId, sku);
            Require(beforeFailure == afterFailure && await NoFinancialArtifacts(admin.ConnectionString, userId, cartId), "WALLET03_FAILED_CHECKOUT_NOT_ROLLED_BACK");

            await CreditWallet(admin.ConnectionString, userId, "synthetic-test-credit:wallet03-http:" + suffix + ":topup", 1);
            var successful = await Send(client, baseUri, "checkouts", HttpMethod.Post, cookies, checkoutPayload, token, settings.Origin,
                cartVersion, "wallet03-http-success-" + suffix);
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
                && persisted.Stock == startStock - 1 && persisted.OrderState == "Confirmed" && persisted.PaymentState == "Succeeded",
                "WALLET03_HTTP_CHECKOUT_SQL_STATE_INVALID");
            var logout = await Send(client, baseUri, "auth/session", HttpMethod.Delete, cookies, null, token, settings.Origin);
            Require(logout.Status == HttpStatusCode.NoContent && !cookies.ContainsKey("kohami.session"), "WALLET03_LOGOUT_COOKIE_NOT_CLEARED");
            Console.WriteLine("PASS wallet03 real HTTP login/CSRF/wallet checkout and SQL assertions on dedicated fixture");
        }
        finally
        {
            if (cookies.ContainsKey("kohami.session"))
            {
                try { using var logoutClient = new HttpClient(new HttpClientHandler { UseCookies = false }); await Send(logoutClient, new Uri($"http://127.0.0.1:{apiPort}/api/v1/"), "auth/session", HttpMethod.Delete, cookies, null, "cleanup", settings.Origin); } catch { }
            }
            if (api is { HasExited: false }) { api.Kill(true); await api.WaitForExitAsync(); }
            if (actorCreated) await Cleanup(admin.ConnectionString, fixtureId, userId, email, cartId, quoteId, checkoutId, orderId, sku, startStock);
            Require(await ReadMaintenance(admin.ConnectionString) == maintenanceBefore, "WALLET03_MAINTENANCE_CHANGED");
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
        await using var marker = new SqlCommand("SELECT COUNT(*) FROM ops.DevelopmentFixtureMarker WHERE FixtureId=@id AND DatasetId='demo_m0' AND InstanceFingerprint=LOWER(CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CONVERT(nvarchar(100),SERVERPROPERTY('ServerName')),'|KohamiM0|demo_m0')),2))", c);
        marker.Parameters.AddWithValue("@id", fixtureId); Require(Convert.ToInt32(await marker.ExecuteScalarAsync()) == 1, "WALLET03_FIXTURE_MARKER_INVALID");
        await using var insert = new SqlCommand("INSERT m0.Users(UserId,Email,PasswordHash,EmailConfirmed,Status,SecurityVersion) VALUES(@u,@e,@p,1,'Active',1)", c);
        insert.Parameters.AddWithValue("@u", user); insert.Parameters.AddWithValue("@e", email); insert.Parameters.AddWithValue("@p", hash); await insert.ExecuteNonQueryAsync();
    }

    private static async Task<(string Sku, int Stock)> SelectSkuAndStock(string cs)
    {
        await using var c = new SqlConnection(cs); await c.OpenAsync();
        await using var cmd = new SqlCommand("SELECT TOP(1) Sku,Stock FROM m0commerce.Inventory WHERE Dataset='demo_m0' AND Active=1 AND Stock>=2 AND Price BETWEEN 1 AND 49900 ORDER BY Sku", c);
        await using var r = await cmd.ExecuteReaderAsync(); Require(await r.ReadAsync(), "WALLET03_STOCK_UNAVAILABLE"); return (r.GetString(0), r.GetInt32(1));
    }

    private static async Task CreditWallet(string cs, string user, string source, long amount)
    {
        await using var c = new SqlConnection(cs); await c.OpenAsync(); await using var tx = await c.BeginTransactionAsync();
        await using var cmd = new SqlCommand("EXEC m0commerce.wallet_synthetic_credit @UserId,@SourceId,@AmountTwd", c, (SqlTransaction)tx);
        cmd.Parameters.AddWithValue("@UserId", user); cmd.Parameters.AddWithValue("@SourceId", source); cmd.Parameters.AddWithValue("@AmountTwd", amount); await cmd.ExecuteNonQueryAsync(); await tx.CommitAsync();
    }

    private static async Task<(long Balance, int Debits, long DebitAmount, string DebitSource, int WalletPaymentCount, long PaymentAmount, int CheckoutCount, string CheckoutOrderId, int OrderCount, int CollectionCount, long Collected, int Stock, string OrderState, string PaymentState)> ReadCheckoutState(string cs, string user, string sku, string checkout, string order, long total)
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

    private static async Task Cleanup(string cs, Guid fixtureId, string user, string email, string? cart, string? quote, string? checkout, string? order, string? sku, int stockBefore)
    {
        await using var c = new SqlConnection(cs); await c.OpenAsync(); await using var tx = await c.BeginTransactionAsync();
        await using var guard = new SqlCommand("IF NOT EXISTS(SELECT 1 FROM ops.DevelopmentFixtureMarker WHERE FixtureId=@f AND DatasetId='demo_m0' AND InstanceFingerprint=LOWER(CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CONVERT(nvarchar(100),SERVERPROPERTY('ServerName')),'|KohamiM0|demo_m0')),2))) THROW 51000,'FIXTURE_MARKER_INVALID',1", c, (SqlTransaction)tx);
        guard.Parameters.AddWithValue("@f", fixtureId); await guard.ExecuteNonQueryAsync();
        if (cart is not null)
        {
            await using var lookup = new SqlCommand("SELECT TOP(1) JSON_VALUE(Response,'$.checkoutId'),JSON_VALUE(Response,'$.orderId') FROM m0commerce.Idempotency WHERE Dataset='demo_m0' AND Actor=@u AND Scope='createCheckout/'+@cart", c, (SqlTransaction)tx);
            lookup.Parameters.AddWithValue("@u", user); lookup.Parameters.AddWithValue("@cart", cart);
            await using (var reader = await lookup.ExecuteReaderAsync())
                if (await reader.ReadAsync()) { if (checkout is null && !reader.IsDBNull(0)) checkout = reader.GetString(0); if (order is null && !reader.IsDBNull(1)) order = reader.GetString(1); }
        }
        if (order is not null) { await using var cmd = new SqlCommand("DELETE m0commerce.RefundUnits WHERE Dataset='demo_m0' AND OrderId=@o; DELETE m0commerce.Collections WHERE Dataset='demo_m0' AND OrderId=@o; DELETE m0commerce.Reservations WHERE Dataset='demo_m0' AND OrderId=@o; DELETE m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='WalletPayment' AND OwnerId=@u; DELETE m0commerce.Documents WHERE Dataset='demo_m0' AND Id IN(@o,@c)", c, (SqlTransaction)tx); cmd.Parameters.AddWithValue("@o", order); cmd.Parameters.AddWithValue("@c", (object?)checkout ?? DBNull.Value); cmd.Parameters.AddWithValue("@u", user); await cmd.ExecuteNonQueryAsync(); }
        if (cart is not null) { await using var cmd = new SqlCommand("DELETE m0commerce.Idempotency WHERE Dataset='demo_m0' AND Actor=@u AND Scope LIKE 'createCheckout/%'; DELETE m0commerce.Documents WHERE Dataset='demo_m0' AND Id IN(@cart,@quote)", c, (SqlTransaction)tx); cmd.Parameters.AddWithValue("@u", user); cmd.Parameters.AddWithValue("@cart", cart); cmd.Parameters.AddWithValue("@quote", (object?)quote ?? DBNull.Value); await cmd.ExecuteNonQueryAsync(); }
        await using (var cmd = new SqlCommand("DELETE m0commerce.WalletLedger WHERE Dataset='demo_m0' AND UserId=@u; DELETE m0commerce.WalletAccounts WHERE Dataset='demo_m0' AND UserId=@u; DELETE m0.SecurityAudit WHERE UserId=@u; DELETE m0.Sessions WHERE UserId=@u; DELETE m0.AuthFailures WHERE AccountKey=LOWER(CONVERT(varchar(64),HASHBYTES('SHA2_256',LOWER(@e)),2)); DELETE m0.Users WHERE UserId=@u AND Email=@e", c, (SqlTransaction)tx)) { cmd.Parameters.AddWithValue("@u", user); cmd.Parameters.AddWithValue("@e", email); await cmd.ExecuteNonQueryAsync(); }
        if (sku is not null) { await using var cmd = new SqlCommand("UPDATE m0commerce.Inventory SET Stock=@stock WHERE Dataset='demo_m0' AND Sku=@s", c, (SqlTransaction)tx); cmd.Parameters.AddWithValue("@stock", stockBefore); cmd.Parameters.AddWithValue("@s", sku); await cmd.ExecuteNonQueryAsync(); }
        await tx.CommitAsync();
    }

    private static async Task<bool> ReadinessAndMaintenance(string cs, Guid fixtureId)
    {
        await using var c = new SqlConnection(cs); await c.OpenAsync();
        await using var cmd = new SqlCommand("SELECT Maintenance FROM m0.SecurityState WHERE Id=1 AND EXISTS(SELECT 1 FROM ops.DevelopmentFixtureMarker WHERE FixtureId=@f AND DatasetId='demo_m0' AND InstanceFingerprint=LOWER(CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CONVERT(nvarchar(100),SERVERPROPERTY('ServerName')),'|KohamiM0|demo_m0')),2)))", c);
        cmd.Parameters.AddWithValue("@f", fixtureId); var value = await cmd.ExecuteScalarAsync(); Require(value is not null, "WALLET03_FIXTURE_MARKER_INVALID"); return Convert.ToBoolean(value);
    }
    private static async Task<bool> ReadMaintenance(string cs) { await using var c = new SqlConnection(cs); await c.OpenAsync(); await using var cmd = new SqlCommand("SELECT Maintenance FROM m0.SecurityState WHERE Id=1", c); return Convert.ToBoolean(await cmd.ExecuteScalarAsync()); }
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
