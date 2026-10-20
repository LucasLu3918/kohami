using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Kohami.M0.Commerce;
using Kohami.M0.Core;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace Kohami.M0.Commerce.Tests;

internal static class WalletSqlAcceptance
{
    private sealed record CheckoutCase(string CheckoutId, string OrderId, string Actor, string Sku, long Total, long Version);

    public static async Task RunAsync(string projectRoot)
    {
        var rootPath = Path.GetFullPath(projectRoot);
        var local = Path.Combine(rootPath, ".local", "kohami-sql", "m0");
        var configPath = Path.Combine(local, "runtime-private.json");
        var provisionerPath = Path.Combine(local, "provisioner-private.json");
        EnsurePrivateFile(configPath, local);
        EnsurePrivateFile(provisionerPath, local);
        var settings = M0Settings.Load(configPath);
        using var provisioner = JsonDocument.Parse(await File.ReadAllTextAsync(provisionerPath));
        var secrets = provisioner.RootElement;
        if (!Guid.TryParse(secrets.GetProperty("fixtureId").GetString(), out var fixtureId))
            throw new InvalidOperationException("WALLET_SQL_ACCEPTANCE_FIXTURE_ID_INVALID");
        var admin = new SqlConnectionStringBuilder(settings.ConnectionString)
        {
            DataSource = "localhost,14349",
            InitialCatalog = "KohamiM0",
            UserID = "kohami_m0_migrator",
            Password = secrets.GetProperty("migrator").GetString(),
            Encrypt = SqlConnectionEncryptOption.Strict,
            TrustServerCertificate = false,
            HostNameInCertificate = settings.SqlServerName
        };
        if (!string.Equals(new SqlConnectionStringBuilder(settings.ConnectionString).DataSource, "localhost,14349", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(admin.InitialCatalog, "KohamiM0", StringComparison.Ordinal)
            || settings.DatasetId != "demo_m0")
            throw new InvalidOperationException("WALLET_SQL_ACCEPTANCE_SCOPE_INVALID");
        admin["Server Certificate"] = settings.SqlCertificatePath;

        var suffix = Guid.NewGuid().ToString("N");
        var insufficientActor = "wallet_accept_ins_" + suffix;
        var replayActor = "wallet_accept_rep_" + suffix;
        var raceActor = "wallet_accept_race_" + suffix;
        var members = new Dictionary<string, string>
        {
            [insufficientActor] = insufficientActor + "@example.test",
            [replayActor] = replayActor + "@example.test",
            [raceActor] = raceActor + "@example.test"
        };
        var sourcePrefix = "synthetic-test-credit:issue26-" + suffix;
        var store = new CommerceStore(new M0Sql(settings));
        var crypto = new M0Crypto(new EphemeralDataProtectionProvider());
        var cases = new List<CheckoutCase>();
        var sku = (Sku: "", Price: 0L, Stock: 0);
        var maintenanceBefore = await ReadMaintenance(admin.ConnectionString);
        if (maintenanceBefore) throw new InvalidOperationException("WALLET_SQL_ACCEPTANCE_MAINTENANCE_ENABLED");

        try
        {
            sku = await PrepareMemberAndReadSku(admin.ConnectionString, insufficientActor, members[insufficientActor], fixtureId);
            await InsertSyntheticMember(admin.ConnectionString, replayActor, members[replayActor]);
            await InsertSyntheticMember(admin.ConnectionString, raceActor, members[raceActor]);

            // A one-dollar shortfall must roll back the debit procedure, ledger, checkout and reservation together.
            var insufficient = await SeedCheckout(store, admin.ConnectionString, insufficientActor, sku, "insufficient");
            cases.Add(insufficient);
            await Credit(admin.ConnectionString, insufficientActor, sourcePrefix + ":insufficient", insufficient.Total - 1);
            var beforeInsufficient = await WalletState(admin.ConnectionString, insufficientActor);
            await ExpectWalletFailure(new CommerceService(store, crypto, new MemberSecurity(insufficientActor), TimeProvider.System), insufficient, "wallet-insufficient-" + suffix);
            var afterInsufficient = await WalletState(admin.ConnectionString, insufficientActor);
            var failedState = await CheckoutState(admin.ConnectionString, insufficient);
            Check(beforeInsufficient == afterInsufficient && failedState == ("AwaitingPayment", "Created", "Held", 0, 0),
                "insufficient balance leaves wallet, ledger, checkout, collection, and reservation unchanged");

            // Same idempotency key and exact request must replay the committed response without another debit.
            var replay = await SeedCheckout(store, admin.ConnectionString, replayActor, sku, "replay");
            cases.Add(replay);
            await Credit(admin.ConnectionString, replayActor, sourcePrefix + ":replay", replay.Total);
            var replayBalanceBefore = await WalletState(admin.ConnectionString, replayActor);
            var replayService = new CommerceService(store, crypto, new MemberSecurity(replayActor), TimeProvider.System);
            var first = await Pay(replayService, replay, "wallet-replay-" + suffix);
            var replayed = await Pay(replayService, replay, "wallet-replay-" + suffix);
            Check(first.Status == 200 && replayed.Status == 200 && JsonNode.DeepEquals(first.Body, replayed.Body),
                "same-key wallet payment replay returns the original success response");
            var replayBalanceAfter = await WalletState(admin.ConnectionString, replayActor);
            var replayState = await CheckoutState(admin.ConnectionString, replay);
            Check(replayBalanceBefore.Balance == replay.Total && replayBalanceAfter.Balance == 0
                && replayBalanceAfter.Debits == 1
                && replayState == ("Confirmed", "Succeeded", "Consumed", 1, 1),
                "idempotent replay records exactly one debit, collection, and consumed reservation");

            // Two independent orders compete for one exact checkout amount: only one may debit.
            var concurrentA = await SeedCheckout(store, admin.ConnectionString, raceActor, sku, "race-a");
            cases.Add(concurrentA);
            var concurrentB = await SeedCheckout(store, admin.ConnectionString, raceActor, sku, "race-b");
            cases.Add(concurrentB);
            Check(concurrentA.Total == concurrentB.Total, "concurrent checkout totals match for a one-credit race");
            await Credit(admin.ConnectionString, raceActor, sourcePrefix + ":race", concurrentA.Total);
            var raceService = new CommerceService(store, crypto, new MemberSecurity(raceActor), TimeProvider.System);
            using var ready = new CountdownEvent(2);
            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            async Task<PayResult> Race(CheckoutCase item, string key)
            {
                ready.Signal();
                await start.Task;
                try { return await Pay(raceService, item, key); }
                catch (M0Exception e) when (e.Status == 409 && e.Code == "WALLET_INSUFFICIENT_BALANCE")
                { return new(409, new JsonObject { ["code"] = e.Code }); }
            }
            var raceA = Task.Run(() => Race(concurrentA, "wallet-race-a-" + suffix));
            var raceB = Task.Run(() => Race(concurrentB, "wallet-race-b-" + suffix));
            if (!ready.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("WALLET_SQL_RACE_READY_TIMEOUT");
            start.SetResult();
            var outcomes = await Task.WhenAll(raceA, raceB);
            var winners = outcomes.Count(x => x.Status == 200);
            var losers = outcomes.Count(x => x.Status == 409);
            var stateA = await CheckoutState(admin.ConnectionString, concurrentA);
            var stateB = await CheckoutState(admin.ConnectionString, concurrentB);
            var raceWallet = await WalletState(admin.ConnectionString, raceActor);
            Check(winners == 1 && losers == 1 && raceWallet.Balance == 0 && raceWallet.Debits == 1,
                "concurrent wallet debits have exactly one winner and cannot overdraw the account");
            Check(new[] { stateA, stateB }.Count(x => x == ("Confirmed", "Succeeded", "Consumed", 1, 1)) == 1
                && new[] { stateA, stateB }.Count(x => x == ("AwaitingPayment", "Created", "Held", 0, 0)) == 1,
                "concurrent loser retains its unpaid order and held reservation without partial collection");
            Console.WriteLine("PASS Issue26 wallet SQL acceptance on marker-guarded localhost:14349/KohamiM0");
        }
        finally
        {
            await Cleanup(admin.ConnectionString, members, cases, sku.Sku);
            if (await ReadMaintenance(admin.ConnectionString) != maintenanceBefore)
                throw new InvalidOperationException("WALLET_SQL_ACCEPTANCE_MAINTENANCE_CHANGED");
        }
    }

    private static async Task<(string Sku, long Price, int Stock)> PrepareMemberAndReadSku(string connectionString, string actor, string email, Guid fixtureId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var marker = new SqlCommand("SELECT COUNT(*) FROM ops.DevelopmentFixtureMarker WHERE FixtureId=@id AND DatasetId='demo_m0' AND InstanceFingerprint=LOWER(CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CONVERT(nvarchar(100),SERVERPROPERTY('ServerName')),'|KohamiM0|demo_m0')),2))", connection);
        marker.Parameters.AddWithValue("@id", fixtureId);
        if (Convert.ToInt32(await marker.ExecuteScalarAsync()) != 1) throw new InvalidOperationException("WALLET_SQL_ACCEPTANCE_MARKER_INVALID");
        await using var maintenance = new SqlCommand("SELECT Maintenance FROM m0.SecurityState WHERE Id=1", connection);
        if (Convert.ToBoolean(await maintenance.ExecuteScalarAsync())) throw new InvalidOperationException("WALLET_SQL_ACCEPTANCE_MAINTENANCE_ENABLED");
        await using var skuQuery = new SqlCommand("SELECT TOP(1) Sku,Price,Stock FROM m0commerce.Inventory WHERE Dataset='demo_m0' AND Active=1 AND Stock>=4 ORDER BY Sku", connection);
        await using var reader = await skuQuery.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) throw new InvalidOperationException("WALLET_SQL_ACCEPTANCE_STOCK_UNAVAILABLE");
        var sku = reader.GetString(0);
        var price = reader.GetInt64(1);
        var stock = reader.GetInt32(2);
        await reader.CloseAsync();
        if (price <= 0 || price > 49900) throw new InvalidOperationException("WALLET_SQL_ACCEPTANCE_PRICE_INVALID");
        await using var seed = new SqlCommand("INSERT m0.Users(UserId,Email,PasswordHash,EmailConfirmed,Status,SecurityVersion) VALUES(@u,@e,REPLICATE(N'x',100),1,'Active',1)", connection);
        seed.Parameters.AddWithValue("@u", actor);
        seed.Parameters.AddWithValue("@e", email);
        await seed.ExecuteNonQueryAsync();
        return (sku, price, stock);
    }

    private static async Task InsertSyntheticMember(string connectionString, string actor, string email)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("INSERT m0.Users(UserId,Email,PasswordHash,EmailConfirmed,Status,SecurityVersion) VALUES(@u,@e,REPLICATE(N'x',100),1,'Active',1)", connection);
        command.Parameters.AddWithValue("@u", actor);
        command.Parameters.AddWithValue("@e", email);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<CheckoutCase> SeedCheckout(CommerceStore store, string admin, string actor, (string Sku, long Price, int Stock) sku, string label)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var checkoutId = "wallet_checkout_" + label + "_" + suffix;
        var orderId = "wallet_order_" + label + "_" + suffix;
        var now = DateTimeOffset.UtcNow;
        var snapshot = CommerceWire.Snapshot(new JsonArray(CommerceWire.Item(sku.Sku, 1, sku.Price, sku.Stock)), 60, false);
        var line = snapshot["lines"]!.AsArray()[0]!.AsObject();
        line["orderLineId"] = "wallet_line_" + suffix;
        line["units"]!.AsArray()[0]!.AsObject()["unitId"] = "wallet_unit_" + suffix;
        var expires = now.AddMinutes(15);
        var body = CommerceWire.Obj(("checkoutId", checkoutId), ("orderId", orderId), ("mode", "mock"),
            ("version", CommerceWire.Version("Checkout", checkoutId, 1)), ("expiresAt", expires), ("snapshot", snapshot),
            ("statusUrl", "/api/v1/checkouts/" + checkoutId + "/status"), ("capabilities", new[] { "checkout.read", "checkout.pay" }),
            ("orderState", "AwaitingPayment"), ("paymentState", "Created"), ("compensationRequired", false),
            ("placedAt", now), ("email", actor + "@example.test"));
        var checkout = new CommerceDocument("Checkout", checkoutId, actor, 1, body);
        var orderBody = (JsonObject)body.DeepClone();
        orderBody["id"] = orderId;
        orderBody["version"] = CommerceWire.Version("Order", orderId, 1);
        var order = new CommerceDocument("Order", orderId, actor, 1, orderBody);
        await using (var connection = new SqlConnection(admin))
        {
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            await using var prepare = new SqlCommand("UPDATE m0commerce.Inventory SET Stock=Stock-1 WHERE Dataset='demo_m0' AND Sku=@s AND Stock>=1; IF @@ROWCOUNT<>1 THROW 51000,'WALLET_SQL_ACCEPTANCE_STOCK_CHANGED',1; INSERT m0commerce.Reservations VALUES('demo_m0',@o,@s,1,'Held',@e)", connection, (SqlTransaction)transaction);
            prepare.Parameters.AddWithValue("@s", sku.Sku);
            prepare.Parameters.AddWithValue("@o", orderId);
            prepare.Parameters.AddWithValue("@e", expires);
            await prepare.ExecuteNonQueryAsync();
            await transaction.CommitAsync();
        }
        try
        {
            await store.Transaction(async tx => { await tx.Save(checkout, true); await tx.Save(order, true); return true; });
        }
        catch
        {
            await using var connection = new SqlConnection(admin);
            await connection.OpenAsync();
            await using var undo = new SqlCommand("DELETE m0commerce.Reservations WHERE Dataset='demo_m0' AND OrderId=@o; UPDATE m0commerce.Inventory SET Stock=Stock+1 WHERE Dataset='demo_m0' AND Sku=@s", connection);
            undo.Parameters.AddWithValue("@o", orderId);
            undo.Parameters.AddWithValue("@s", sku.Sku);
            await undo.ExecuteNonQueryAsync();
            throw;
        }
        return new(checkoutId, orderId, actor, sku.Sku, (long)snapshot["totalTwd"]!, 1);
    }

    private sealed record PayResult(int Status, JsonNode Body);
    private static async Task<PayResult> Pay(CommerceService service, CheckoutCase item, string key)
    {
        var context = NewRequest(item, key);
        var result = await service.Pay(context, item.CheckoutId);
        await result.ExecuteAsync(context);
        context.Response.Body.Position = 0;
        var body = await JsonNode.ParseAsync(context.Response.Body) ?? new JsonObject();
        return new(context.Response.StatusCode, body);
    }

    private static async Task ExpectWalletFailure(CommerceService service, CheckoutCase item, string key)
    {
        try { await Pay(service, item, key); throw new InvalidOperationException("WALLET_SQL_ACCEPTANCE_INSUFFICIENT_ACCEPTED"); }
        catch (M0Exception e) when (e.Status == 409 && e.Code == "WALLET_INSUFFICIENT_BALANCE") { }
    }

    private static HttpContext NewRequest(CheckoutCase item, string key)
    {
        var context = new DefaultHttpContext { RequestServices = new TestServices() };
        context.Request.Method = "POST";
        context.Request.ContentType = "application/json";
        context.Request.Headers["Idempotency-Key"] = key;
        var version = CommerceWire.Version("Checkout", item.CheckoutId, item.Version);
        context.Request.Headers.IfMatch = '"' + version + '"';
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(new JsonObject { ["expectedVersion"] = version }.ToJsonString()));
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<(long Balance, int Debits)> WalletState(string connectionString, string actor)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("SELECT a.AvailableTwd,(SELECT COUNT(*) FROM m0commerce.WalletLedger l WHERE l.Dataset=a.Dataset AND l.UserId=a.UserId AND l.EntryType='CheckoutDebit') FROM m0commerce.WalletAccounts a WHERE a.Dataset='demo_m0' AND a.UserId=@u", connection);
        command.Parameters.AddWithValue("@u", actor);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return (0, 0);
        return (reader.GetInt64(0), reader.GetInt32(1));
    }

    private static async Task<(string Order, string Payment, string Reservation, int Collections, int Debits)> CheckoutState(string connectionString, CheckoutCase item)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("SELECT JSON_VALUE(c.Body,'$.orderState'),JSON_VALUE(c.Body,'$.paymentState'),r.State,(SELECT COUNT(*) FROM m0commerce.Collections x WHERE x.Dataset=c.Dataset AND x.OrderId=@o),(SELECT COUNT(*) FROM m0commerce.WalletLedger l WHERE l.Dataset=c.Dataset AND l.UserId=c.OwnerId AND l.EntryType='CheckoutDebit' AND l.SourceId=c.Id) FROM m0commerce.Documents c JOIN m0commerce.Reservations r ON r.Dataset=c.Dataset AND r.OrderId=@o WHERE c.Dataset='demo_m0' AND c.Kind='Checkout' AND c.Id=@c", connection);
        command.Parameters.AddWithValue("@c", item.CheckoutId);
        command.Parameters.AddWithValue("@o", item.OrderId);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) throw new InvalidOperationException("WALLET_SQL_ACCEPTANCE_CHECKOUT_MISSING");
        return (reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetInt32(3), reader.GetInt32(4));
    }

    private static async Task Credit(string connectionString, string actor, string source, long amount)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        try
        {
            await using var credit = new SqlCommand("EXEC m0commerce.wallet_synthetic_credit @UserId,@SourceId,@AmountTwd", connection, (SqlTransaction)transaction);
            credit.Parameters.AddWithValue("@UserId", actor);
            credit.Parameters.AddWithValue("@SourceId", source);
            credit.Parameters.AddWithValue("@AmountTwd", amount);
            await credit.ExecuteNonQueryAsync();
            await transaction.CommitAsync();
        }
        catch { await transaction.RollbackAsync(); throw; }
    }

    private static async Task<bool> ReadMaintenance(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("SELECT Maintenance FROM m0.SecurityState WHERE Id=1", connection);
        return Convert.ToBoolean(await command.ExecuteScalarAsync());
    }

    private static async Task Cleanup(string connectionString, IReadOnlyDictionary<string, string> members, IReadOnlyCollection<CheckoutCase> cases, string sku)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        try
        {
            foreach (var item in cases)
            {
                await using var cleanupCase = new SqlCommand("DELETE m0commerce.RefundUnits WHERE Dataset='demo_m0' AND OrderId=@o; DELETE m0commerce.Collections WHERE Dataset='demo_m0' AND OrderId=@o; DELETE m0commerce.Idempotency WHERE Dataset='demo_m0' AND Actor=@a; DELETE m0commerce.Reservations WHERE Dataset='demo_m0' AND OrderId=@o; DELETE m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='WalletPayment' AND OwnerId=@a AND JSON_VALUE(Body,'$.checkoutId')=@c; DELETE m0commerce.Documents WHERE Dataset='demo_m0' AND Id IN(@c,@o);", connection, (SqlTransaction)transaction);
                cleanupCase.Parameters.AddWithValue("@o", item.OrderId);
                cleanupCase.Parameters.AddWithValue("@c", item.CheckoutId);
                cleanupCase.Parameters.AddWithValue("@a", item.Actor);
                await cleanupCase.ExecuteNonQueryAsync();
            }
            foreach (var (actor, email) in members)
            {
                await using var cleanupActor = new SqlCommand("DELETE m0.SecurityAudit WHERE UserId=@a AND Action IN('SyntheticWalletCredit'); DELETE m0commerce.WalletLedger WHERE Dataset='demo_m0' AND UserId=@a; DELETE m0commerce.WalletAccounts WHERE Dataset='demo_m0' AND UserId=@a; DELETE m0.Users WHERE UserId=@a AND Email=@e", connection, (SqlTransaction)transaction);
                cleanupActor.Parameters.AddWithValue("@a", actor);
                cleanupActor.Parameters.AddWithValue("@e", email);
                await cleanupActor.ExecuteNonQueryAsync();
            }
            await using var restoreInventory = new SqlCommand("UPDATE m0commerce.Inventory SET Stock=Stock+@n WHERE Dataset='demo_m0' AND Sku=@s", connection, (SqlTransaction)transaction);
            restoreInventory.Parameters.AddWithValue("@s", sku);
            restoreInventory.Parameters.AddWithValue("@n", cases.Count);
            await restoreInventory.ExecuteNonQueryAsync();
            // Add back exactly the inventory units held by this runner; do not overwrite concurrent local changes.
            await transaction.CommitAsync();
        }
        catch { await transaction.RollbackAsync(); throw; }
        foreach (var (actor, email) in members)
        {
            await using var verifyActor = new SqlCommand("SELECT (SELECT COUNT(*) FROM m0.Users WHERE UserId=@a AND Email=@e)+(SELECT COUNT(*) FROM m0commerce.WalletAccounts WHERE Dataset='demo_m0' AND UserId=@a)+(SELECT COUNT(*) FROM m0commerce.WalletLedger WHERE Dataset='demo_m0' AND UserId=@a)+(SELECT COUNT(*) FROM m0.SecurityAudit WHERE UserId=@a)", connection);
            verifyActor.Parameters.AddWithValue("@a", actor);
            verifyActor.Parameters.AddWithValue("@e", email);
            if (Convert.ToInt32(await verifyActor.ExecuteScalarAsync()) != 0) throw new InvalidOperationException("WALLET_SQL_ACCEPTANCE_ACTOR_CLEANUP_INCOMPLETE");
        }
        foreach (var item in cases)
        {
            await using var verify = new SqlCommand("SELECT (SELECT COUNT(*) FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Id IN(@c,@o))+(SELECT COUNT(*) FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='WalletPayment' AND OwnerId=@a AND JSON_VALUE(Body,'$.checkoutId')=@c)+(SELECT COUNT(*) FROM m0commerce.Reservations WHERE Dataset='demo_m0' AND OrderId=@o)+(SELECT COUNT(*) FROM m0commerce.Collections WHERE Dataset='demo_m0' AND OrderId=@o)+(SELECT COUNT(*) FROM m0commerce.Idempotency WHERE Dataset='demo_m0' AND Actor=@a)+(SELECT COUNT(*) FROM m0commerce.WalletLedger WHERE Dataset='demo_m0' AND UserId=@a)", connection);
            verify.Parameters.AddWithValue("@c", item.CheckoutId);
            verify.Parameters.AddWithValue("@o", item.OrderId);
            verify.Parameters.AddWithValue("@a", item.Actor);
            if (Convert.ToInt32(await verify.ExecuteScalarAsync()) != 0) throw new InvalidOperationException("WALLET_SQL_ACCEPTANCE_CLEANUP_INCOMPLETE");
        }
    }

    private static void EnsurePrivateFile(string path, string localDirectory)
    {
        if (!Directory.Exists(localDirectory) || new DirectoryInfo(localDirectory).LinkTarget is not null
            || !File.Exists(path) || new FileInfo(path).LinkTarget is not null)
            throw new InvalidOperationException("WALLET_SQL_ACCEPTANCE_PRIVATE_PROFILE_INVALID");
        if (!OperatingSystem.IsWindows())
        {
            const UnixFileMode forbidden = UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute
                | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;
            if ((File.GetUnixFileMode(path) & forbidden) != 0 || (File.GetUnixFileMode(localDirectory) & forbidden) != 0)
                throw new InvalidOperationException("WALLET_SQL_ACCEPTANCE_PRIVATE_MODE_INVALID");
        }
    }

    private static void Check(bool pass, string name)
    {
        if (!pass) throw new InvalidOperationException("FAILED: " + name);
        Console.WriteLine("PASS: " + name);
    }

    private sealed class MemberSecurity(string actor) : IM0Security
    {
        public Task<M0Principal?> ResolveAsync(HttpContext context, CancellationToken ct = default) => Task.FromResult<M0Principal?>(new(actor, "issue26-wallet-session", 1, "MemberAuthenticated", Array.Empty<string>()));
        public Task<M0Principal> RequirePermissionAsync(HttpContext context, string permission, string resourceKind, string resourceId, CancellationToken ct = default) => Task.FromException<M0Principal>(new M0Exception(403, "FORBIDDEN"));
        public Task<bool> CanDispatchAsync(string actorUserId, long securityVersion, string permission, string resourceKind, string resourceId, CancellationToken ct = default) => Task.FromResult(false);
    }

    private sealed class TestServices : IServiceProvider
    {
        public object? GetService(Type serviceType) => serviceType == typeof(IOptions<JsonOptions>)
            ? Options.Create(new JsonOptions())
            : serviceType == typeof(ILoggerFactory) ? NullLoggerFactory.Instance : null;
    }
}
