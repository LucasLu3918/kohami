using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Kohami.M0.Commerce;
using Kohami.Commerce.Domain;
using Kohami.M0.Core;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kohami.M0.Commerce.Tests;

internal static class Wallet03SqlAcceptance
{
    private sealed record CheckoutInput(string CartId, string QuoteId, string Actor, string Sku, long Total, long UnitPrice, long CartVersion);
    private sealed record CheckoutResult(int Status, JsonObject Body);
    private sealed record Persisted(string OrderState, string PaymentState, int Orders, int Checkouts, int Reservations, int Collections, int RefundUnits, int WalletPayments, int Debits, int Stock);

    public static async Task RunAsync(string projectRoot)
    {
        var root = Path.GetFullPath(projectRoot);
        var local = Path.Combine(root, ".local", "kohami-sql", "m0");
        var configPath = Path.Combine(local, "runtime-private.json");
        var provisionerPath = Path.Combine(local, "provisioner-private.json");
        EnsurePrivateFile(configPath, local);
        EnsurePrivateFile(provisionerPath, local);
        var settings = M0Settings.Load(configPath);
        using var provisioner = JsonDocument.Parse(await File.ReadAllTextAsync(provisionerPath));
        var fixtureId = Guid.Parse(provisioner.RootElement.GetProperty("fixtureId").GetString()!);
        var migratorCredential = provisioner.RootElement.GetProperty("migrator").GetString();
        var admin = new SqlConnectionStringBuilder(settings.ConnectionString)
        {
            DataSource = "localhost,14349", InitialCatalog = "KohamiM0", UserID = "kohami_m0_migrator",
            Encrypt = SqlConnectionEncryptOption.Strict,
            TrustServerCertificate = false, HostNameInCertificate = settings.SqlServerName
        };
        admin["Password"] = migratorCredential;
        if (!string.Equals(new SqlConnectionStringBuilder(settings.ConnectionString).DataSource, "localhost,14349", StringComparison.OrdinalIgnoreCase)
            || settings.DatasetId != "demo_m0" || !string.Equals(admin.InitialCatalog, "KohamiM0", StringComparison.Ordinal))
            throw new InvalidOperationException("WALLET03_SQL_ACCEPTANCE_SCOPE_INVALID");
        admin["Server Certificate"] = settings.SqlCertificatePath;
        var maintenanceBefore = await Maintenance(admin.ConnectionString);
        if (maintenanceBefore) throw new InvalidOperationException("WALLET03_SQL_ACCEPTANCE_MAINTENANCE_ENABLED");

        var suffix = Guid.NewGuid().ToString("N");
        var actors = new[] { "wallet03_short_" + suffix, "wallet03_conflict_" + suffix, "wallet03_success_" + suffix, "wallet03_race_" + suffix };
        var members = actors.ToDictionary(x => x, x => x + "@example.test", StringComparer.Ordinal);
        var inputs = new List<CheckoutInput>();
        var completed = new List<(string CheckoutId, string OrderId, string Actor)>();
        var sku = (Id: "", Price: 0L, Stock: 0);
        var store = new CommerceStore(new M0Sql(settings));
        var crypto = new M0Crypto(new EphemeralDataProtectionProvider());
        try
        {
            sku = await PrepareMembersAndSku(admin.ConnectionString, fixtureId, members);

            // Red/green guard: no order, checkout, reservation, or wallet debit exists before this single API call.
            var insufficient = await SeedCartQuote(store, actors[0], sku.Id, sku.Price, "short_" + suffix);
            inputs.Add(insufficient);
            await Credit(admin.ConnectionString, actors[0], "synthetic-test-credit:wallet03:" + suffix + ":short", insufficient.Total - 1);
            var shortBefore = await WalletAndStock(admin.ConnectionString, actors[0], sku.Id);
            var insufficientObserved = false;
            try { _ = await Checkout(new CommerceService(store, crypto, new MemberSecurity(actors[0]), TimeProvider.System), crypto, insufficient, "wallet03-short-" + suffix); }
            catch (M0Exception e) when (e.Status == 409 && e.Code == "WALLET_INSUFFICIENT_BALANCE") { insufficientObserved = true; }
            var shortAfter = await WalletAndStock(admin.ConnectionString, actors[0], sku.Id);
            Check(insufficientObserved && shortBefore == shortAfter && await CartAndQuoteUnchanged(store, insufficient) && await NoCheckoutArtifacts(admin.ConnectionString, insufficient),
                "insufficient wallet balance leaves cart/quote, checkout/order, inventory, reservations, collections and ledger unchanged");

            // Force a downstream stock predicate failure after wallet_debit has executed inside the SQL transaction.
            var conflict = await SeedCartQuote(store, actors[1], sku.Id, sku.Price, "conflict_" + suffix);
            inputs.Add(conflict);
            await Credit(admin.ConnectionString, actors[1], "synthetic-test-credit:wallet03:" + suffix + ":conflict", conflict.Total);
            await SetPrice(admin.ConnectionString, sku.Id, sku.Price + 1);
            var conflictBalanceBefore = await WalletState(admin.ConnectionString, actors[1]);
            var stockConflictObserved = false;
            try { _ = await Checkout(new CommerceService(store, crypto, new MemberSecurity(actors[1]), TimeProvider.System), crypto, conflict, "wallet03-stock-conflict-" + suffix); }
            catch (M0Exception e) when (e.Status == 409 && e.Code == "STOCK_CHANGED") { stockConflictObserved = true; }
            finally { await SetPrice(admin.ConnectionString, sku.Id, sku.Price); }
            Check(stockConflictObserved && conflictBalanceBefore == await WalletState(admin.ConnectionString, actors[1])
                && await CartAndQuoteUnchanged(store, conflict) && await NoCheckoutArtifacts(admin.ConnectionString, conflict),
                "downstream stock conflict rolls back the earlier wallet debit and leaves no order or checkout");

            var success = await SeedCartQuote(store, actors[2], sku.Id, sku.Price, "success_" + suffix);
            inputs.Add(success);
            await Credit(admin.ConnectionString, actors[2], "synthetic-test-credit:wallet03:" + suffix + ":success", success.Total);
            var successService = new CommerceService(store, crypto, new MemberSecurity(actors[2]), TimeProvider.System);
            var key = "wallet03-replay-" + suffix;
            _ = await Checkout(successService, crypto, success, key); // Discard the committed HTTP result to model a lost response.
            var recovered = await Checkout(successService, crypto, success, key);
            var checkoutId = recovered.Body["checkoutId"]!.GetValue<string>();
            var orderId = recovered.Body["orderId"]!.GetValue<string>();
            completed.Add((checkoutId, orderId, actors[2]));
            var keyConflict = false;
            try { _ = await Checkout(successService, crypto, success, key, "different@example.test"); }
            catch (M0Exception e) when (e.Status == 409 && e.Code == "KEY_CONFLICT") { keyConflict = true; }
            var state = await PersistedState(admin.ConnectionString, actors[2], checkoutId, orderId, sku.Id);
            var beforeOwnerCheck = state;
            var anonymousDenied = false;
            try { _ = await Checkout(new CommerceService(store, crypto, new AnonymousSecurity(), TimeProvider.System), crypto, success, "wallet03-anon-" + suffix); }
            catch (M0Exception e) when (e.Status is 401 or 404) { anonymousDenied = true; }
            var otherOwnerDenied = false;
            try { _ = await Checkout(new CommerceService(store, crypto, new MemberSecurity(actors[3]), TimeProvider.System), crypto, success, "wallet03-owner-" + suffix); }
            catch (M0Exception e) when (e.Status == 404 && e.Code == "RESOURCE_NOT_FOUND") { otherOwnerDenied = true; }
            Check(recovered.Status == 201 && recovered.Body["checkoutId"]?.GetValue<string>() == checkoutId
                && keyConflict && anonymousDenied && otherOwnerDenied && beforeOwnerCheck == state,
                "same-key lost-response replay returns the original checkout; changed body and non-owner requests are rejected");
            Check(state.OrderState == "Confirmed" && state.PaymentState == "Succeeded" && state.Orders == 1 && state.Checkouts == 1
                && state.Reservations == 1 && state.Collections == 1 && state.RefundUnits > 0 && state.WalletPayments == 1 && state.Debits == 1
                && state.Stock == sku.Stock - 1 && await CartClearedAndQuoteUsed(store, success),
                "atomic wallet checkout commits exactly one debit, collection, order, consumed reservation, and decremented stock");

            var raceA = await SeedCartQuote(store, actors[3], sku.Id, sku.Price, "race_a_" + suffix);
            var raceB = await SeedCartQuote(store, actors[3], sku.Id, sku.Price, "race_b_" + suffix);
            inputs.Add(raceA); inputs.Add(raceB);
            if (raceA.Total != raceB.Total) throw new InvalidOperationException("WALLET03_SQL_ACCEPTANCE_TOTAL_MISMATCH");
            await Credit(admin.ConnectionString, actors[3], "synthetic-test-credit:wallet03:" + suffix + ":race", raceA.Total);
            var race = await RunProcessRace(root, fixtureId, raceA, raceB, suffix);
            var winners = race.Count(x => x.Status == 201);
            var losers = race.Count(x => x.Status == 409);
            Check(winners == 1 && losers == 1 && race.All(x => x.Status == 201 || x.Body["code"]?.GetValue<string>() is "WALLET_INSUFFICIENT_BALANCE" or "STOCK_CHANGED"),
                "independent processes racing the last wallet amount and stock unit produce one winner and one conflict");
            var raceStates = new List<Persisted>();
            for (var i = 0; i < race.Count; i++)
            {
                var id = race[i].Body["checkoutId"]?.GetValue<string>();
                var order = race[i].Body["orderId"]?.GetValue<string>();
                if (id is not null && order is not null) { completed.Add((id, order, actors[3])); raceStates.Add(await PersistedState(admin.ConnectionString, actors[3], id, order, sku.Id)); }
            }
            var raceWallet = await WalletState(admin.ConnectionString, actors[3]);
            var raceStock = await Stock(admin.ConnectionString, sku.Id);
            Check(raceWallet == (0L, 1) && raceStates.Count == 1 && raceStates[0].OrderState == "Confirmed"
                && raceStates[0].Debits == 1 && raceStates[0].Collections == 1 && raceStock == sku.Stock - 2,
                "race loser has no partial checkout or inventory write and cannot overdraw or oversell");
            Console.WriteLine("PASS wallet03 atomic checkout SQL/API acceptance on dedicated marker-guarded localhost:14349/KohamiM0");
        }
        finally
        {
            await Cleanup(admin.ConnectionString, members, inputs, completed, sku.Id);
            if (await Maintenance(admin.ConnectionString) != maintenanceBefore)
                throw new InvalidOperationException("WALLET03_SQL_ACCEPTANCE_MAINTENANCE_CHANGED");
        }
    }

    public static async Task RunCheckoutChildAsync(string root, string cartId, string quoteId, string actor, string sku, long total, long unitPrice, long cartVersion, string key, string readyPath, string startPath)
    {
        var rootPath = Path.GetFullPath(root); var local = Path.Combine(rootPath, ".local", "kohami-sql", "m0");
        var configPath = Path.Combine(local, "runtime-private.json"); var provisionerPath = Path.Combine(local, "provisioner-private.json");
        EnsurePrivateFile(configPath, local); EnsurePrivateFile(provisionerPath, local);
        var settings = M0Settings.Load(configPath);
        using var provisioner = JsonDocument.Parse(await File.ReadAllTextAsync(provisionerPath));
        var fixtureId = Guid.Parse(provisioner.RootElement.GetProperty("fixtureId").GetString()!);
        if (!actor.StartsWith("wallet03_race_", StringComparison.Ordinal) || !cartId.StartsWith("wallet03_cart_race_", StringComparison.Ordinal)
            || !quoteId.StartsWith("wallet03_quote_race_", StringComparison.Ordinal) || !key.StartsWith("wallet03-race-", StringComparison.Ordinal)
            || settings.DatasetId != "demo_m0" || !string.Equals(new SqlConnectionStringBuilder(settings.ConnectionString).DataSource, "localhost,14349", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("WALLET03_CHILD_SCOPE_INVALID");
        EnsureBarrier(readyPath, startPath);
        var crypto = new M0Crypto(new EphemeralDataProtectionProvider());
        var service = new CommerceService(new CommerceStore(new M0Sql(settings)), crypto, new MemberSecurity(actor), TimeProvider.System);
        var input = new CheckoutInput(cartId, quoteId, actor, sku, total, unitPrice, cartVersion);
        await VerifyRaceInput(rootPath, input, fixtureId);
        await File.WriteAllTextAsync(readyPath, "ready");
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while (!File.Exists(startPath) && DateTimeOffset.UtcNow < deadline) await Task.Delay(25);
        if (!File.Exists(startPath)) throw new TimeoutException("WALLET03_CHILD_START_TIMEOUT");
        try
        {
            var result = await Checkout(service, crypto, input, key);
            Console.WriteLine("WALLET03_CHILD=" + JsonSerializer.Serialize(result));
        }
        catch (M0Exception e)
        {
            Console.WriteLine("WALLET03_CHILD=" + JsonSerializer.Serialize(new CheckoutResult(e.Status, CommerceWire.Obj(("code", e.Code)))));
        }
    }

    private static async Task<(string Id, long Price, int Stock)> PrepareMembersAndSku(string connectionString, Guid fixtureId, IReadOnlyDictionary<string, string> members)
    {
        await using var connection = new SqlConnection(connectionString); await connection.OpenAsync();
        await using var marker = new SqlCommand("SELECT COUNT(*) FROM ops.DevelopmentFixtureMarker WHERE FixtureId=@id AND DatasetId='demo_m0' AND InstanceFingerprint=LOWER(CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CONVERT(nvarchar(100),SERVERPROPERTY('ServerName')),'|KohamiM0|demo_m0')),2))", connection);
        marker.Parameters.AddWithValue("@id", fixtureId);
        if (Convert.ToInt32(await marker.ExecuteScalarAsync()) != 1) throw new InvalidOperationException("WALLET03_FIXTURE_MARKER_INVALID");
        await using var maintenance = new SqlCommand("SELECT Maintenance FROM m0.SecurityState WHERE Id=1", connection);
        if (Convert.ToBoolean(await maintenance.ExecuteScalarAsync())) throw new InvalidOperationException("WALLET03_MAINTENANCE_ENABLED");
        await using var skuQuery = new SqlCommand("SELECT TOP(1) Sku,Price,Stock FROM m0commerce.Inventory WHERE Dataset='demo_m0' AND Active=1 AND Stock>=5 ORDER BY Sku", connection);
        await using var reader = await skuQuery.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) throw new InvalidOperationException("WALLET03_STOCK_UNAVAILABLE");
        var sku = reader.GetString(0); var price = reader.GetInt64(1); var stock = reader.GetInt32(2); await reader.CloseAsync();
        if (price <= 0 || price > 49900) throw new InvalidOperationException("WALLET03_PRICE_INVALID");
        foreach (var (actor, email) in members)
        {
            await using var insert = new SqlCommand("INSERT m0.Users(UserId,Email,PasswordHash,EmailConfirmed,Status,SecurityVersion) VALUES(@u,@e,REPLICATE(N'x',100),1,'Active',1)", connection);
            insert.Parameters.AddWithValue("@u", actor); insert.Parameters.AddWithValue("@e", email); await insert.ExecuteNonQueryAsync();
        }
        return (sku, price, stock);
    }

    private static async Task<CheckoutInput> SeedCartQuote(CommerceStore store, string actor, string sku, long price, string suffix)
    {
        var cartId = "wallet03_cart_" + suffix; var quoteId = "wallet03_quote_" + suffix;
        var item = CommerceWire.Item(sku, 1, price, 10);
        var cartVersion = CommerceWire.Version("Cart", cartId, 1);
        var cartBody = CommerceWire.Obj(("id", cartId), ("mode", "mock"), ("version", cartVersion), ("items", new JsonArray(item)), ("promotionCode", null));
        var fee = MockShippingFeePolicy.Resolve("mock_home", "demo_supported") ?? throw new InvalidOperationException("WALLET03_FEE_UNAVAILABLE");
        var snapshot = CommerceWire.Snapshot(new JsonArray(item.DeepClone()), fee, false);
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(5);
        var quoteBody = CommerceWire.Obj(("id", quoteId), ("mode", "mock"), ("expiresAt", expiresAt), ("snapshot", snapshot),
            ("version", CommerceWire.Version("Quote", quoteId, 1)), ("cartId", cartId), ("cartVersion", cartVersion),
            ("deliveryMethod", "mock_home"), ("region", "demo_supported"), ("used", false));
        await store.Transaction(async tx =>
        {
            await tx.Save(new("Cart", cartId, actor, 1, cartBody), true);
            await tx.Save(new("Quote", quoteId, actor, 1, quoteBody), true);
            return true;
        });
        return new(cartId, quoteId, actor, sku, snapshot["totalTwd"]!.GetValue<long>(), price, 1);
    }

    private static async Task<CheckoutResult> Checkout(CommerceService service, M0Crypto crypto, CheckoutInput input, string key, string? email = null)
    {
        var context = Request(crypto, input, key, email);
        var result = await service.Checkout(context);
        await result.ExecuteAsync(context);
        context.Response.Body.Position = 0;
        var body = await JsonNode.ParseAsync(context.Response.Body) as JsonObject ?? new JsonObject();
        return new(context.Response.StatusCode, body);
    }

    private static DefaultHttpContext Request(M0Crypto crypto, CheckoutInput input, string key, string? email = null)
    {
        var context = new DefaultHttpContext { RequestServices = new TestServices() };
        context.Request.Method = "POST"; context.Request.Path = "/api/v1/checkouts"; context.Request.ContentType = "application/json";
        context.Request.Headers["Idempotency-Key"] = key;
        context.Request.Headers.IfMatch = '"' + CommerceWire.Version("Cart", input.CartId, input.CartVersion) + '"';
        var cookieValue = CommerceWire.Obj(("value", input.CartId), ("until", DateTimeOffset.UtcNow.AddDays(1).ToString("O"))).ToJsonString();
        context.Request.Headers.Cookie = "kohami.cart=" + crypto.Sessions.CreateProtector("kohami.cart").Protect(cookieValue);
        var body = CommerceWire.Obj(("quoteId", input.QuoteId), ("cartVersion", CommerceWire.Version("Cart", input.CartId, input.CartVersion)),
            ("contact", CommerceWire.Obj(("email", email ?? input.Actor + "@example.test"))),
            ("delivery", CommerceWire.Obj(("method", "mock_home"), ("region", "demo_supported"))), ("confirmed", true), ("paymentMethod", "wallet"));
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body.ToJsonString())); context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task Credit(string connectionString, string actor, string source, long amount)
    {
        await using var connection = new SqlConnection(connectionString); await connection.OpenAsync(); await using var transaction = await connection.BeginTransactionAsync();
        try
        {
            await using var command = new SqlCommand("EXEC m0commerce.wallet_synthetic_credit @UserId,@SourceId,@AmountTwd", connection, (SqlTransaction)transaction);
            command.Parameters.AddWithValue("@UserId", actor); command.Parameters.AddWithValue("@SourceId", source); command.Parameters.AddWithValue("@AmountTwd", amount);
            await command.ExecuteNonQueryAsync(); await transaction.CommitAsync();
        }
        catch { await transaction.RollbackAsync(); throw; }
    }

    private static async Task<(long Balance, int Debits)> WalletState(string connectionString, string actor)
    {
        await using var connection = new SqlConnection(connectionString); await connection.OpenAsync();
        await using var command = new SqlCommand("SELECT a.AvailableTwd,(SELECT COUNT(*) FROM m0commerce.WalletLedger l WHERE l.Dataset=a.Dataset AND l.UserId=a.UserId AND l.EntryType='CheckoutDebit') FROM m0commerce.WalletAccounts a WHERE a.Dataset='demo_m0' AND a.UserId=@u", connection);
        command.Parameters.AddWithValue("@u", actor); await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? (reader.GetInt64(0), reader.GetInt32(1)) : (0, 0);
    }

    private static async Task<(long Balance, int Debits, int Stock)> WalletAndStock(string connectionString, string actor, string sku)
    {
        var wallet = await WalletState(connectionString, actor); return (wallet.Balance, wallet.Debits, await Stock(connectionString, sku));
    }

    private static async Task<int> Stock(string connectionString, string sku)
    {
        await using var connection = new SqlConnection(connectionString); await connection.OpenAsync();
        await using var command = new SqlCommand("SELECT Stock FROM m0commerce.Inventory WHERE Dataset='demo_m0' AND Sku=@s", connection);
        command.Parameters.AddWithValue("@s", sku); return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task SetPrice(string connectionString, string sku, long price)
    {
        await using var connection = new SqlConnection(connectionString); await connection.OpenAsync();
        await using var command = new SqlCommand("UPDATE m0commerce.Inventory SET Price=@p WHERE Dataset='demo_m0' AND Sku=@s", connection);
        command.Parameters.AddWithValue("@p", price); command.Parameters.AddWithValue("@s", sku); if (await command.ExecuteNonQueryAsync() != 1) throw new InvalidOperationException("WALLET03_PRICE_RESTORE_FAILED");
    }

    private static async Task<Persisted> PersistedState(string connectionString, string actor, string checkoutId, string orderId, string sku)
    {
        await using var connection = new SqlConnection(connectionString); await connection.OpenAsync();
        await using var command = new SqlCommand(@"
SELECT JSON_VALUE(c.Body,'$.orderState'),JSON_VALUE(c.Body,'$.paymentState'),
 (SELECT COUNT(*) FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='Order' AND Id=@o AND OwnerId=@a),
 (SELECT COUNT(*) FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='Checkout' AND Id=@c AND OwnerId=@a),
 (SELECT COUNT(*) FROM m0commerce.Reservations WHERE Dataset='demo_m0' AND OrderId=@o AND State='Consumed' AND Sku=@s),
 (SELECT COUNT(*) FROM m0commerce.Collections WHERE Dataset='demo_m0' AND OrderId=@o),
 (SELECT COUNT(*) FROM m0commerce.RefundUnits WHERE Dataset='demo_m0' AND OrderId=@o),
 (SELECT COUNT(*) FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='WalletPayment' AND OwnerId=@a AND JSON_VALUE(Body,'$.checkoutId')=@c),
 (SELECT COUNT(*) FROM m0commerce.WalletLedger WHERE Dataset='demo_m0' AND UserId=@a AND EntryType='CheckoutDebit' AND SourceId=@c),
 (SELECT Stock FROM m0commerce.Inventory WHERE Dataset='demo_m0' AND Sku=@s)
FROM m0commerce.Documents c WHERE c.Dataset='demo_m0' AND c.Kind='Checkout' AND c.Id=@c", connection);
        command.Parameters.AddWithValue("@a", actor); command.Parameters.AddWithValue("@c", checkoutId); command.Parameters.AddWithValue("@o", orderId); command.Parameters.AddWithValue("@s", sku);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) throw new InvalidOperationException("WALLET03_CHECKOUT_NOT_FOUND");
        return new(reader.GetString(0), reader.GetString(1), reader.GetInt32(2), reader.GetInt32(3), reader.GetInt32(4), reader.GetInt32(5), reader.GetInt32(6), reader.GetInt32(7), reader.GetInt32(8), reader.GetInt32(9));
    }

    private static async Task<bool> NoCheckoutArtifacts(string connectionString, CheckoutInput input)
    {
        await using var connection = new SqlConnection(connectionString); await connection.OpenAsync();
        await using var command = new SqlCommand(@"
SELECT
 (SELECT COUNT(*) FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind IN('Checkout','Order','WalletPayment') AND OwnerId=@a AND JSON_VALUE(Body,'$.snapshot.lines[0].skuId')=@s)+
 (SELECT COUNT(*) FROM m0commerce.Reservations r JOIN m0commerce.Documents o ON o.Dataset=r.Dataset AND o.Id=r.OrderId WHERE r.Dataset='demo_m0' AND o.OwnerId=@a AND r.Sku=@s)+
 (SELECT COUNT(*) FROM m0commerce.Collections c JOIN m0commerce.Documents o ON o.Dataset=c.Dataset AND o.Id=c.OrderId WHERE c.Dataset='demo_m0' AND o.OwnerId=@a)+
 (SELECT COUNT(*) FROM m0commerce.WalletLedger WHERE Dataset='demo_m0' AND UserId=@a AND EntryType='CheckoutDebit')+
 (SELECT COUNT(*) FROM m0commerce.Idempotency WHERE Dataset='demo_m0' AND Actor=@a AND Scope='createCheckout/'+@cart)", connection);
        command.Parameters.AddWithValue("@a", input.Actor); command.Parameters.AddWithValue("@s", input.Sku); command.Parameters.AddWithValue("@cart", input.CartId);
        return Convert.ToInt32(await command.ExecuteScalarAsync()) == 0;
    }

    private static async Task<bool> CartClearedAndQuoteUsed(CommerceStore store, CheckoutInput input) => await store.Transaction(async tx =>
    {
        var cart = await tx.Get("Cart", input.CartId); var quote = await tx.Get("Quote", input.QuoteId);
        return cart.Body["items"]!.AsArray().Count == 0 && quote.Body["used"]!.GetValue<bool>();
    });

    private static async Task<bool> CartAndQuoteUnchanged(CommerceStore store, CheckoutInput input) => await store.Transaction(async tx =>
    {
        var cart = await tx.Get("Cart", input.CartId); var quote = await tx.Get("Quote", input.QuoteId);
        var item = cart.Body["items"]!.AsArray().SingleOrDefault()?.AsObject();
        return cart.Version == input.CartVersion && item is not null && item.Text("skuId") == input.Sku && item.Number("quantity") == 1
            && !quote.Body["used"]!.GetValue<bool>();
    });

    private static async Task<IReadOnlyList<CheckoutResult>> RunProcessRace(string root, Guid fixtureId, CheckoutInput a, CheckoutInput b, string suffix)
    {
        var directory = Path.Combine(Path.GetTempPath(), "kohami-wallet03-race-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var start = Path.Combine(directory, "start"); var children = new List<(Process Process, string Ready)>();
        try
        {
            var assembly = Environment.GetCommandLineArgs().FirstOrDefault() ?? throw new InvalidOperationException("WALLET03_ASSEMBLY_MISSING");
            foreach (var (input, key, index) in new[] { (a, "wallet03-race-a-" + suffix, 1), (b, "wallet03-race-b-" + suffix, 2) })
            {
                await VerifyRaceInput(root, input, fixtureId);
                var ready = Path.Combine(directory, "ready-" + index);
                var info = new ProcessStartInfo(Environment.ProcessPath ?? throw new InvalidOperationException("WALLET03_PROCESS_PATH_MISSING"))
                { WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
                foreach (var arg in new[] { assembly, "--wallet-checkout-child", root, input.CartId, input.QuoteId, input.Actor, input.Sku, input.Total.ToString(System.Globalization.CultureInfo.InvariantCulture), input.UnitPrice.ToString(System.Globalization.CultureInfo.InvariantCulture), input.CartVersion.ToString(System.Globalization.CultureInfo.InvariantCulture), key, ready, start }) info.ArgumentList.Add(arg);
                children.Add((Process.Start(info) ?? throw new InvalidOperationException("WALLET03_CHILD_START_FAILED"), ready));
            }
            var deadline = DateTimeOffset.UtcNow.AddSeconds(20);
            while (children.Any(x => !File.Exists(x.Ready)) && DateTimeOffset.UtcNow < deadline)
            {
                if (children.Any(x => x.Process.HasExited)) throw new InvalidOperationException("WALLET03_CHILD_EARLY_EXIT");
                await Task.Delay(25);
            }
            if (children.Any(x => !File.Exists(x.Ready))) throw new TimeoutException("WALLET03_CHILD_READY_TIMEOUT");
            await File.WriteAllTextAsync(start, "go");
            await Task.WhenAll(children.Select(x => x.Process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(40))));
            var results = new List<CheckoutResult>();
            foreach (var child in children)
            {
                var stdout = await child.Process.StandardOutput.ReadToEndAsync(); var stderr = await child.Process.StandardError.ReadToEndAsync();
                var line = stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries).LastOrDefault(x => x.StartsWith("WALLET03_CHILD=", StringComparison.Ordinal));
                if (child.Process.ExitCode != 0 || line is null) throw new InvalidOperationException("WALLET03_CHILD_FAILED:" + stderr);
                results.Add(JsonSerializer.Deserialize<CheckoutResult>(line["WALLET03_CHILD=".Length..]) ?? throw new InvalidOperationException("WALLET03_CHILD_RESULT_INVALID"));
            }
            return results;
        }
        finally
        {
            foreach (var item in children) { if (!item.Process.HasExited) item.Process.Kill(true); item.Process.Dispose(); }
            try { Directory.Delete(directory, true); } catch (IOException) { }
        }
    }

    private static async Task VerifyRaceInput(string root, CheckoutInput input, Guid fixtureId)
    {
        var local = Path.Combine(root, ".local", "kohami-sql", "m0"); var settings = M0Settings.Load(Path.Combine(local, "runtime-private.json"));
        using var provisioner = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(local, "provisioner-private.json")));
        if (settings.DatasetId != "demo_m0" || !string.Equals(new SqlConnectionStringBuilder(settings.ConnectionString).DataSource, "localhost,14349", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("WALLET03_CHILD_TARGET_INVALID");
        var migratorCredential = provisioner.RootElement.GetProperty("migrator").GetString();
        var admin = new SqlConnectionStringBuilder(settings.ConnectionString) { DataSource = "localhost,14349", InitialCatalog = "KohamiM0", UserID = "kohami_m0_migrator", Encrypt = SqlConnectionEncryptOption.Strict, TrustServerCertificate = false, HostNameInCertificate = settings.SqlServerName };
        admin["Password"] = migratorCredential;
        admin["Server Certificate"] = settings.SqlCertificatePath;
        await using var connection = new SqlConnection(admin.ConnectionString); await connection.OpenAsync();
        await using var command = new SqlCommand("SELECT (SELECT COUNT(*) FROM ops.DevelopmentFixtureMarker WHERE FixtureId=@f AND DatasetId='demo_m0' AND InstanceFingerprint=LOWER(CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CONVERT(nvarchar(100),SERVERPROPERTY('ServerName')),'|KohamiM0|demo_m0')),2))),(SELECT COUNT(*) FROM m0.SecurityState WHERE Id=1 AND Maintenance=0),(SELECT COUNT(*) FROM m0.Users WHERE UserId=@a AND Email=@e AND Status='Active'),(SELECT COUNT(*) FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='Cart' AND Id=@cart AND OwnerId=@a),(SELECT COUNT(*) FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='Quote' AND Id=@quote AND OwnerId=@a AND JSON_VALUE(Body,'$.used')='false')", connection);
        command.Parameters.AddWithValue("@f", fixtureId); command.Parameters.AddWithValue("@a", input.Actor); command.Parameters.AddWithValue("@e", input.Actor + "@example.test"); command.Parameters.AddWithValue("@cart", input.CartId); command.Parameters.AddWithValue("@quote", input.QuoteId);
        await using var reader = await command.ExecuteReaderAsync(); if (!await reader.ReadAsync() || Enumerable.Range(0, 5).Any(i => reader.GetInt32(i) != 1)) throw new InvalidOperationException("WALLET03_CHILD_FIXTURE_GUARD_FAILED");
    }

    private static async Task Cleanup(string connectionString, IReadOnlyDictionary<string, string> members, IReadOnlyCollection<CheckoutInput> inputs, IReadOnlyCollection<(string CheckoutId, string OrderId, string Actor)> completed, string sku)
    {
        await using var connection = new SqlConnection(connectionString); await connection.OpenAsync(); await using var tx = await connection.BeginTransactionAsync();
        try
        {
            var artifacts = completed.ToDictionary(x => x.CheckoutId, StringComparer.Ordinal);
            foreach (var input in inputs)
            {
                await using var lookup = new SqlCommand("SELECT TOP(1) JSON_VALUE(Response,'$.checkoutId'),JSON_VALUE(Response,'$.orderId') FROM m0commerce.Idempotency WHERE Dataset='demo_m0' AND Actor=@a AND Scope='createCheckout/'+@cart", connection, (SqlTransaction)tx);
                lookup.Parameters.AddWithValue("@a", input.Actor); lookup.Parameters.AddWithValue("@cart", input.CartId);
                await using var reader = await lookup.ExecuteReaderAsync();
                if (await reader.ReadAsync() && !reader.IsDBNull(0) && !reader.IsDBNull(1)) artifacts[reader.GetString(0)] = (reader.GetString(0), reader.GetString(1), input.Actor);
            }
            foreach (var item in artifacts.Values)
            {
                await using var command = new SqlCommand("DELETE m0commerce.RefundUnits WHERE Dataset='demo_m0' AND OrderId=@o; DELETE m0commerce.Collections WHERE Dataset='demo_m0' AND OrderId=@o; DELETE m0commerce.Reservations WHERE Dataset='demo_m0' AND OrderId=@o; DELETE m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='WalletPayment' AND OwnerId=@a AND JSON_VALUE(Body,'$.checkoutId')=@c; DELETE m0commerce.Documents WHERE Dataset='demo_m0' AND Id IN(@c,@o)", connection, (SqlTransaction)tx);
                command.Parameters.AddWithValue("@o", item.OrderId); command.Parameters.AddWithValue("@a", item.Actor); command.Parameters.AddWithValue("@c", item.CheckoutId); await command.ExecuteNonQueryAsync();
            }
            foreach (var input in inputs)
            {
                await using var command = new SqlCommand("DELETE m0commerce.Idempotency WHERE Dataset='demo_m0' AND Actor=@a AND Scope='createCheckout/'+@cart; DELETE m0commerce.Documents WHERE Dataset='demo_m0' AND Id IN(@cart,@quote)", connection, (SqlTransaction)tx);
                command.Parameters.AddWithValue("@a", input.Actor); command.Parameters.AddWithValue("@cart", input.CartId); command.Parameters.AddWithValue("@quote", input.QuoteId); await command.ExecuteNonQueryAsync();
            }
            foreach (var (actor, email) in members)
            {
                await using var command = new SqlCommand("DELETE m0.SecurityAudit WHERE UserId=@a AND Action IN('SyntheticWalletCredit'); DELETE m0commerce.WalletLedger WHERE Dataset='demo_m0' AND UserId=@a; DELETE m0commerce.WalletAccounts WHERE Dataset='demo_m0' AND UserId=@a; DELETE m0.Users WHERE UserId=@a AND Email=@e", connection, (SqlTransaction)tx);
                command.Parameters.AddWithValue("@a", actor); command.Parameters.AddWithValue("@e", email); await command.ExecuteNonQueryAsync();
            }
            await using var restore = new SqlCommand("UPDATE m0commerce.Inventory SET Stock=Stock+@n WHERE Dataset='demo_m0' AND Sku=@s", connection, (SqlTransaction)tx);
            restore.Parameters.AddWithValue("@n", artifacts.Count); restore.Parameters.AddWithValue("@s", sku); await restore.ExecuteNonQueryAsync(); await tx.CommitAsync();
        }
        catch { await tx.RollbackAsync(); throw; }
    }

    private static async Task<bool> Maintenance(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString); await connection.OpenAsync(); await using var command = new SqlCommand("SELECT Maintenance FROM m0.SecurityState WHERE Id=1", connection); return Convert.ToBoolean(await command.ExecuteScalarAsync());
    }

    private static void EnsurePrivateFile(string path, string directory)
    {
        if (!Directory.Exists(directory) || new DirectoryInfo(directory).LinkTarget is not null || !File.Exists(path) || new FileInfo(path).LinkTarget is not null) throw new InvalidOperationException("WALLET03_PRIVATE_PROFILE_INVALID");
        if (!OperatingSystem.IsWindows())
        {
            const UnixFileMode forbidden = UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;
            if ((File.GetUnixFileMode(path) & forbidden) != 0 || (File.GetUnixFileMode(directory) & forbidden) != 0) throw new InvalidOperationException("WALLET03_PRIVATE_MODE_INVALID");
        }
    }

    private static void EnsureBarrier(string readyPath, string startPath)
    {
        var ready = Path.GetFullPath(readyPath); var start = Path.GetFullPath(startPath); var directory = Path.GetDirectoryName(ready);
        if (directory is null || directory != Path.GetDirectoryName(start) || !Path.GetFileName(directory).StartsWith("kohami-wallet03-race-", StringComparison.Ordinal) || !Directory.Exists(directory) || new DirectoryInfo(directory).LinkTarget is not null) throw new InvalidOperationException("WALLET03_BARRIER_PATH_INVALID");
        if (!OperatingSystem.IsWindows() && File.GetUnixFileMode(directory) != (UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute)) throw new InvalidOperationException("WALLET03_BARRIER_MODE_INVALID");
    }

    private static void Check(bool pass, string name) { if (!pass) throw new InvalidOperationException("FAILED: " + name); Console.WriteLine("PASS: " + name); }

    private sealed class MemberSecurity(string actor) : IM0Security
    {
        public Task<M0Principal?> ResolveAsync(HttpContext context, CancellationToken ct = default) => Task.FromResult<M0Principal?>(new(actor, "wallet03-member-session", 1, "MemberAuthenticated", Array.Empty<string>()));
        public Task<M0Principal> RequirePermissionAsync(HttpContext context, string permission, string resourceKind, string resourceId, CancellationToken ct = default) => Task.FromException<M0Principal>(new M0Exception(403, "FORBIDDEN"));
        public Task<bool> CanDispatchAsync(string actorUserId, long securityVersion, string permission, string resourceKind, string resourceId, CancellationToken ct = default) => Task.FromResult(false);
    }

    private sealed class AnonymousSecurity : IM0Security
    {
        public Task<M0Principal?> ResolveAsync(HttpContext context, CancellationToken ct = default) => Task.FromResult<M0Principal?>(null);
        public Task<M0Principal> RequirePermissionAsync(HttpContext context, string permission, string resourceKind, string resourceId, CancellationToken ct = default) => Task.FromException<M0Principal>(new M0Exception(401, "AUTHENTICATION_REQUIRED"));
        public Task<bool> CanDispatchAsync(string actorUserId, long securityVersion, string permission, string resourceKind, string resourceId, CancellationToken ct = default) => Task.FromResult(false);
    }

    private sealed class TestServices : IServiceProvider
    {
        public object? GetService(Type type) => type == typeof(IOptions<JsonOptions>) ? Options.Create(new JsonOptions()) : type == typeof(ILoggerFactory) ? NullLoggerFactory.Instance : null;
    }
}
