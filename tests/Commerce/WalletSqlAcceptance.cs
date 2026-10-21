using System.Text;
using System.Diagnostics;
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
            Encrypt = SqlConnectionEncryptOption.Strict,
            TrustServerCertificate = false,
            HostNameInCertificate = settings.SqlServerName
        };
        admin["Password"] = secrets.GetProperty("migrator").GetString();
        if (!string.Equals(new SqlConnectionStringBuilder(settings.ConnectionString).DataSource, "localhost,14349", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(admin.InitialCatalog, "KohamiM0", StringComparison.Ordinal)
            || settings.DatasetId != "demo_m0")
            throw new InvalidOperationException("WALLET_SQL_ACCEPTANCE_SCOPE_INVALID");
        admin["Server Certificate"] = settings.SqlCertificatePath;

        var suffix = Guid.NewGuid().ToString("N");
        var insufficientActor = "wallet_accept_ins_" + suffix;
        var rollbackActor = "wallet_accept_rollback_" + suffix;
        var replayActor = "wallet_accept_rep_" + suffix;
        var raceActor = "wallet_accept_race_" + suffix;
        var members = new Dictionary<string, string>
        {
            [insufficientActor] = insufficientActor + "@example.test",
            [rollbackActor] = rollbackActor + "@example.test",
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
            await InsertSyntheticMember(admin.ConnectionString, rollbackActor, members[rollbackActor]);
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

            // A downstream reservation failure occurs after the debit procedure has written inside the transaction.
            var downstreamFailure = await SeedCheckout(store, admin.ConnectionString, rollbackActor, sku, "downstream-failure");
            cases.Add(downstreamFailure);
            await Credit(admin.ConnectionString, rollbackActor, sourcePrefix + ":downstream-failure", downstreamFailure.Total);
            var beforeDownstreamFailure = await WalletState(admin.ConnectionString, rollbackActor);
            await SetReservationState(admin.ConnectionString, downstreamFailure, "Released");
            var downstreamRollbackObserved = false;
            try { await Pay(new CommerceService(store, crypto, new MemberSecurity(rollbackActor), TimeProvider.System), downstreamFailure, "wallet-downstream-failure-" + suffix); }
            catch (M0Exception e) when (e.Status == 409 && e.Code == "RESERVATION_INVALID") { downstreamRollbackObserved = true; }
            var afterDownstreamFailure = await WalletState(admin.ConnectionString, rollbackActor);
            var downstreamState = await CheckoutState(admin.ConnectionString, downstreamFailure);
            Check(downstreamRollbackObserved && beforeDownstreamFailure == afterDownstreamFailure
                && downstreamState == ("AwaitingPayment", "Created", "Released", 0, 0),
                "downstream failure after wallet debit rolls back ledger and payment writes atomically");

            // Drop the first HTTP result after the SQL transaction commits; the same request recovers its original response.
            var replay = await SeedCheckout(store, admin.ConnectionString, replayActor, sku, "replay");
            cases.Add(replay);
            await Credit(admin.ConnectionString, replayActor, sourcePrefix + ":replay", replay.Total);
            var replayBalanceBefore = await WalletState(admin.ConnectionString, replayActor);
            var replayService = new CommerceService(store, crypto, new MemberSecurity(replayActor), TimeProvider.System);
            var replayKey = "wallet-replay-" + suffix;
            _ = await replayService.Pay(NewRequest(replay, replayKey), replay.CheckoutId); // Intentionally discard the committed response.
            var recovered = await Pay(replayService, replay, replayKey);
            Check(recovered.Status == 200 && recovered.Body["state"]?.GetValue<string>() == "Succeeded",
                "same-key wallet payment recovers the committed response after a lost response");
            var conflictObserved = false;
            try { await Pay(replayService, replay, replayKey, "different-payload"); }
            catch (M0Exception e) when (e.Status == 409 && e.Code == "KEY_CONFLICT") { conflictObserved = true; }
            Check(conflictObserved, "same idempotency key with a different request payload is rejected");
            var anonymousDenied = false;
            try { await Pay(new CommerceService(store, crypto, new AnonymousSecurity(), TimeProvider.System), replay, replayKey); }
            catch (M0Exception e) when (e.Status == 404 && e.Code == "RESOURCE_NOT_FOUND") { anonymousDenied = true; }
            Check(anonymousDenied, "anonymous visitor cannot submit or replay a member wallet payment");
            var replayBalanceAfter = await WalletState(admin.ConnectionString, replayActor);
            var replayState = await CheckoutState(admin.ConnectionString, replay);
            Check(replayBalanceBefore.Balance == replay.Total && replayBalanceAfter.Balance == 0
                && replayBalanceAfter.Debits == 1
                && replayState == ("Confirmed", "Succeeded", "Consumed", 1, 1),
                "lost-response recovery and replay conflict create exactly one debit, collection, and consumed reservation");

            // Two independent orders compete for one exact checkout amount: only one may debit.
            var concurrentA = await SeedCheckout(store, admin.ConnectionString, raceActor, sku, "race-a");
            cases.Add(concurrentA);
            var concurrentB = await SeedCheckout(store, admin.ConnectionString, raceActor, sku, "race-b");
            cases.Add(concurrentB);
            Check(concurrentA.Total == concurrentB.Total, "concurrent checkout totals match for a one-credit race");
            await Credit(admin.ConnectionString, raceActor, sourcePrefix + ":race", concurrentA.Total);
            var raceService = new CommerceService(store, crypto, new MemberSecurity(raceActor), TimeProvider.System);
            var outcomes = await RunMultiProcessRaceAsync(rootPath, concurrentA, concurrentB,
                "wallet-race-a-" + suffix, "wallet-race-b-" + suffix);
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
            await VerifyCurrentWalletRead(replayService, raceService, replayActor);
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
        await using var skuQuery = new SqlCommand("SELECT TOP(1) Sku,Price,Stock FROM m0commerce.Inventory WHERE Dataset='demo_m0' AND Active=1 AND Stock>=5 ORDER BY Sku", connection);
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

    private sealed record WalletReadResult(int Status, JsonObject Body, bool HasEtag);

    private static async Task VerifyCurrentWalletRead(CommerceService replayService, CommerceService raceService, string replayActor)
    {
        var firstPage = await ReadWallet(replayService, "?limit=1&userId=wallet_accept_race_must_be_ignored");
        Check(firstPage.Status == 200 && firstPage.Body["mode"]?.GetValue<string>() == "mock"
            && firstPage.Body["entries"]?.AsArray().Count == 1
            && !firstPage.HasEtag
            && firstPage.Body["nextCursor"]?.GetValue<string>() is { Length: > 0 },
            "member wallet read returns a bounded first page and continuation cursor without requiring ETag");

        var cursor = firstPage.Body["nextCursor"]!.GetValue<string>();
        var secondPage = await ReadWallet(replayService, "?limit=1&cursor=" + Uri.EscapeDataString(cursor));
        var firstId = firstPage.Body["entries"]![0]!["id"]!.GetValue<string>();
        var secondId = secondPage.Body["entries"]![0]!["id"]!.GetValue<string>();
        Check(secondPage.Status == 200 && secondPage.Body["entries"]?.AsArray().Count == 1
            && secondPage.Body["nextCursor"] is null && firstId != secondId,
            "member wallet keyset pages are stable and do not repeat an entry");

        var ownEntries = await ReadWallet(replayService, "?limit=50");
        Check(ownEntries.Body["entries"]?.AsArray().Count == 2
            && ownEntries.Body["entries"]!.AsArray().All(entry =>
                !entry!.AsObject()["sourceId"]!.GetValue<string>().Contains("race", StringComparison.Ordinal)),
            "member wallet read remains bound to the authenticated member despite a userId query parameter");

        var raceEntries = await ReadWallet(raceService, "?limit=50");
        Check(raceEntries.Body["entries"]?.AsArray().Count == 2
            && raceEntries.Body["entries"]!.AsArray().All(entry =>
                !entry!.AsObject()["sourceId"]!.GetValue<string>().Contains(replayActor, StringComparison.Ordinal)),
            "wallet read for a second member contains only that member's ledger rows");

        var badCursorRejected = false;
        try { await ReadWallet(replayService, "?limit=1&cursor=not-a-valid-cursor"); }
        catch (M0Exception error) when (error.Status == 422 && error.Code == "VALIDATION_ERROR") { badCursorRejected = true; }
        Check(badCursorRejected, "malformed member wallet cursors are rejected");

        var badLimitRejected = false;
        try { await ReadWallet(replayService, "?limit=0"); }
        catch (M0Exception error) when (error.Status == 422 && error.Code == "VALIDATION_ERROR") { badLimitRejected = true; }
        Check(badLimitRejected, "out-of-range member wallet page sizes are rejected");
    }

    private static async Task<WalletReadResult> ReadWallet(CommerceService service, string query)
    {
        var context = new DefaultHttpContext { RequestServices = new TestServices() };
        context.Request.Method = "GET";
        context.Request.QueryString = QueryString.FromUriComponent(query);
        context.Response.Body = new MemoryStream();
        var result = await service.CurrentWallet(context);
        await result.ExecuteAsync(context);
        context.Response.Body.Position = 0;
        var body = await JsonNode.ParseAsync(context.Response.Body) as JsonObject ?? new JsonObject();
        return new(context.Response.StatusCode, body, context.Response.Headers.ETag.Count > 0);
    }

    private sealed record PayResult(int Status, JsonNode Body);
    private static async Task<PayResult> Pay(CommerceService service, CheckoutCase item, string key)
    {
        return await Pay(service, item, key, null);
    }

    private static async Task<PayResult> Pay(CommerceService service, CheckoutCase item, string key, string? expectedVersionBody)
    {
        var context = NewRequest(item, key, expectedVersionBody);
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

    private static HttpContext NewRequest(CheckoutCase item, string key, string? expectedVersionBody = null)
    {
        var context = new DefaultHttpContext { RequestServices = new TestServices() };
        context.Request.Method = "POST";
        context.Request.ContentType = "application/json";
        context.Request.Headers["Idempotency-Key"] = key;
        var version = CommerceWire.Version("Checkout", item.CheckoutId, item.Version);
        context.Request.Headers.IfMatch = '"' + version + '"';
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(new JsonObject { ["expectedVersion"] = expectedVersionBody ?? version }.ToJsonString()));
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task SetReservationState(string connectionString, CheckoutCase item, string state)
    {
        if (state is not ("Released" or "Held")) throw new ArgumentOutOfRangeException(nameof(state));
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("UPDATE m0commerce.Reservations SET State=@state WHERE Dataset='demo_m0' AND OrderId=@order AND Sku=@sku AND State='Held'", connection);
        command.Parameters.AddWithValue("@state", state);
        command.Parameters.AddWithValue("@order", item.OrderId);
        command.Parameters.AddWithValue("@sku", item.Sku);
        if (await command.ExecuteNonQueryAsync() != 1) throw new InvalidOperationException("WALLET_SQL_ACCEPTANCE_RESERVATION_STATE_CHANGE_FAILED");
    }

    private static async Task<IReadOnlyList<PayResult>> RunMultiProcessRaceAsync(string root, CheckoutCase first, CheckoutCase second, string firstKey, string secondKey)
    {
        var directory = Path.Combine(Path.GetTempPath(), "kohami-wallet03-race-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var startPath = Path.Combine(directory, "start");
        var processes = new List<(Process Process, string ReadyPath)>();
        try
        {
            var assembly = Environment.GetCommandLineArgs().FirstOrDefault() ?? throw new InvalidOperationException("WALLET_SQL_ACCEPTANCE_ASSEMBLY_PATH_MISSING");
            foreach (var (item, key, index) in new[] { (first, firstKey, 1), (second, secondKey, 2) })
            {
                var readyPath = Path.Combine(directory, $"ready-{index}");
                var startInfo = new ProcessStartInfo(Environment.ProcessPath ?? throw new InvalidOperationException("WALLET_SQL_ACCEPTANCE_PROCESS_PATH_MISSING"))
                {
                    WorkingDirectory = root,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                };
                startInfo.ArgumentList.Add(assembly);
                startInfo.ArgumentList.Add("--wallet-pay-child");
                startInfo.ArgumentList.Add(root);
                startInfo.ArgumentList.Add(item.CheckoutId);
                startInfo.ArgumentList.Add(item.Actor);
                startInfo.ArgumentList.Add(key);
                startInfo.ArgumentList.Add(readyPath);
                startInfo.ArgumentList.Add(startPath);
                var process = Process.Start(startInfo) ?? throw new InvalidOperationException("WALLET_SQL_ACCEPTANCE_CHILD_START_FAILED");
                processes.Add((process, readyPath));
            }

            var deadline = DateTimeOffset.UtcNow.AddSeconds(20);
            while (processes.Any(x => !File.Exists(x.ReadyPath)) && DateTimeOffset.UtcNow < deadline)
            {
                if (processes.Any(x => x.Process.HasExited)) throw new InvalidOperationException("WALLET_SQL_ACCEPTANCE_CHILD_EXITED_BEFORE_READY");
                await Task.Delay(25);
            }
            if (processes.Any(x => !File.Exists(x.ReadyPath))) throw new TimeoutException("WALLET_SQL_ACCEPTANCE_CHILD_READY_TIMEOUT");
            await File.WriteAllTextAsync(startPath, "go");
            await Task.WhenAll(processes.Select(x => x.Process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(40))));
            var results = new List<PayResult>();
            foreach (var child in processes)
            {
                var stdout = await child.Process.StandardOutput.ReadToEndAsync();
                _ = await child.Process.StandardError.ReadToEndAsync();
                if (child.Process.ExitCode != 0) throw new InvalidOperationException("WALLET_SQL_ACCEPTANCE_CHILD_FAILED");
                var statusLine = stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries).LastOrDefault(x => x.StartsWith("WALLET_CHILD_STATUS=", StringComparison.Ordinal));
                if (statusLine is null || !int.TryParse(statusLine["WALLET_CHILD_STATUS=".Length..], out var status))
                    throw new InvalidOperationException("WALLET_SQL_ACCEPTANCE_CHILD_RESULT_INVALID");
                results.Add(new(status, new JsonObject()));
            }
            Check(results.Count == 2, "wallet checkout competitors ran in two independent operating-system processes");
            return results;
        }
        finally
        {
            foreach (var (process, _) in processes)
            {
                if (!process.HasExited) { try { process.Kill(true); } catch { } }
                process.Dispose();
            }
            Directory.Delete(directory, recursive: true);
        }
    }

    public static async Task RunPayChildAsync(string projectRoot, string checkoutId, string actor, string key, string readyPath, string startPath)
    {
        if (!actor.StartsWith("wallet_accept_race_", StringComparison.Ordinal)
            || !(checkoutId.StartsWith("wallet_checkout_race-a_", StringComparison.Ordinal)
                || checkoutId.StartsWith("wallet_checkout_race-b_", StringComparison.Ordinal))
            || !key.StartsWith("wallet-race-", StringComparison.Ordinal))
            throw new InvalidOperationException("WALLET_SQL_ACCEPTANCE_CHILD_SCOPE_INVALID");
        var root = Path.GetFullPath(projectRoot);
        var local = Path.Combine(root, ".local", "kohami-sql", "m0");
        var configPath = Path.Combine(local, "runtime-private.json");
        var provisionerPath = Path.Combine(local, "provisioner-private.json");
        EnsurePrivateFile(configPath, local);
        EnsurePrivateFile(provisionerPath, local);
        var settings = M0Settings.Load(configPath);
        using var provisioner = JsonDocument.Parse(await File.ReadAllTextAsync(provisionerPath));
        if (!Guid.TryParse(provisioner.RootElement.GetProperty("fixtureId").GetString(), out var fixtureId))
            throw new InvalidOperationException("WALLET_SQL_ACCEPTANCE_FIXTURE_ID_INVALID");
        var runtimeConnection = new SqlConnectionStringBuilder(settings.ConnectionString);
        if (!string.Equals(runtimeConnection.DataSource, "localhost,14349", StringComparison.OrdinalIgnoreCase)
            || settings.DatasetId != "demo_m0")
            throw new InvalidOperationException("WALLET_SQL_ACCEPTANCE_CHILD_TARGET_INVALID");
        var admin = new SqlConnectionStringBuilder(runtimeConnection.ConnectionString)
        {
            DataSource = "localhost,14349",
            InitialCatalog = "KohamiM0",
            UserID = "kohami_m0_migrator",
            Encrypt = SqlConnectionEncryptOption.Strict,
            TrustServerCertificate = false,
            HostNameInCertificate = settings.SqlServerName
        };
        admin["Password"] = provisioner.RootElement.GetProperty("migrator").GetString();
        admin["Server Certificate"] = settings.SqlCertificatePath;
        await EnsureRaceChildFixtureAsync(admin.ConnectionString, fixtureId, checkoutId, actor);
        EnsureRaceChildDirectory(readyPath, startPath);
        var service = new CommerceService(new CommerceStore(new M0Sql(settings)), new M0Crypto(new EphemeralDataProtectionProvider()), new MemberSecurity(actor), TimeProvider.System);
        await File.WriteAllTextAsync(readyPath, "ready");
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while (!File.Exists(startPath) && DateTimeOffset.UtcNow < deadline) await Task.Delay(25);
        if (!File.Exists(startPath)) throw new TimeoutException("WALLET_SQL_ACCEPTANCE_CHILD_START_TIMEOUT");
        try
        {
            var result = await Pay(service, new(checkoutId, "", actor, "", 0, 1), key);
            Console.WriteLine("WALLET_CHILD_STATUS=" + result.Status.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        catch (M0Exception error) when (error.Status == 409 && error.Code == "WALLET_INSUFFICIENT_BALANCE")
        {
            Console.WriteLine("WALLET_CHILD_STATUS=409");
        }
    }

    private static async Task EnsureRaceChildFixtureAsync(string connectionString, Guid fixtureId, string checkoutId, string actor)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var guard = new SqlCommand(@"
SELECT
  (SELECT COUNT(*) FROM ops.DevelopmentFixtureMarker WHERE FixtureId=@fixture AND DatasetId='demo_m0' AND InstanceFingerprint=LOWER(CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CONVERT(nvarchar(100),SERVERPROPERTY('ServerName')),'|KohamiM0|demo_m0')),2))),
  (SELECT COUNT(*) FROM m0.SecurityState WHERE Id=1 AND Maintenance=0),
  (SELECT COUNT(*) FROM m0.Users WHERE UserId=@actor AND Email=@email AND Status='Active'),
  (SELECT COUNT(*) FROM m0commerce.Documents c JOIN m0commerce.Documents o ON o.Dataset=c.Dataset AND o.Kind='Order' AND o.Id=JSON_VALUE(c.Body,'$.orderId') JOIN m0commerce.Reservations r ON r.Dataset=c.Dataset AND r.OrderId=o.Id WHERE c.Dataset='demo_m0' AND c.Kind='Checkout' AND c.Id=@checkout AND c.OwnerId=@actor AND JSON_VALUE(c.Body,'$.orderState')='AwaitingPayment' AND JSON_VALUE(c.Body,'$.paymentState')='Created' AND o.OwnerId=@actor AND r.State='Held' AND r.Sku=JSON_VALUE(c.Body,'$.snapshot.lines[0].skuId'))", connection);
        guard.Parameters.AddWithValue("@fixture", fixtureId);
        guard.Parameters.AddWithValue("@actor", actor);
        guard.Parameters.AddWithValue("@email", actor + "@example.test");
        guard.Parameters.AddWithValue("@checkout", checkoutId);
        await using var reader = await guard.ExecuteReaderAsync();
        if (!await reader.ReadAsync() || reader.GetInt32(0) != 1 || reader.GetInt32(1) != 1 || reader.GetInt32(2) != 1 || reader.GetInt32(3) != 1)
            throw new InvalidOperationException("WALLET_SQL_ACCEPTANCE_CHILD_FIXTURE_GUARD_FAILED");
    }

    private static void EnsureRaceChildDirectory(string readyPath, string startPath)
    {
        var ready = Path.GetFullPath(readyPath);
        var start = Path.GetFullPath(startPath);
        var directory = Path.GetDirectoryName(ready);
        if (directory is null || !string.Equals(directory, Path.GetDirectoryName(start), StringComparison.Ordinal)
            || !Path.GetFileName(directory).StartsWith("kohami-wallet03-race-", StringComparison.Ordinal)
            || !Directory.Exists(directory) || new DirectoryInfo(directory).LinkTarget is not null)
            throw new InvalidOperationException("WALLET_SQL_ACCEPTANCE_CHILD_BARRIER_PATH_INVALID");
        if (!OperatingSystem.IsWindows())
        {
            const UnixFileMode expected = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
            if (File.GetUnixFileMode(directory) != expected)
                throw new InvalidOperationException("WALLET_SQL_ACCEPTANCE_CHILD_BARRIER_MODE_INVALID");
        }
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

    private sealed class AnonymousSecurity : IM0Security
    {
        public Task<M0Principal?> ResolveAsync(HttpContext context, CancellationToken ct = default) => Task.FromResult<M0Principal?>(null);
        public Task<M0Principal> RequirePermissionAsync(HttpContext context, string permission, string resourceKind, string resourceId, CancellationToken ct = default) => Task.FromException<M0Principal>(new M0Exception(401, "AUTHENTICATION_REQUIRED"));
        public Task<bool> CanDispatchAsync(string actorUserId, long securityVersion, string permission, string resourceKind, string resourceId, CancellationToken ct = default) => Task.FromResult(false);
    }

    private sealed class TestServices : IServiceProvider
    {
        public object? GetService(Type serviceType) => serviceType == typeof(IOptions<JsonOptions>)
            ? Options.Create(new JsonOptions())
            : serviceType == typeof(ILoggerFactory) ? NullLoggerFactory.Instance : null;
    }
}
