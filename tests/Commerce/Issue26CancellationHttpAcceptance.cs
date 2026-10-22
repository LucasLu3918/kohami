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

internal static class Issue26CancellationHttpAcceptance
{
    private sealed record Actor(string UserId, string Email, string Password);
    private sealed record RunState(string OrderId, string CheckoutId, string Sku, long UnitPrice, int StockBefore, Actor Owner, Actor Foreign);
    private sealed record Response(HttpStatusCode Status, JsonObject Body);
    private sealed record BusinessState(string OrderState, string OrderPaymentState, string CheckoutState,
        string CheckoutPaymentState, string ReservationState, int Stock, int CancelRows);

    public static async Task RunAsync(string configPath, string apiConfigPath, int apiPort)
    {
        var (settings, adminConnectionString) = Issue11CartAcceptance.DedicatedAdmin(configPath);
        apiConfigPath = Path.GetFullPath(apiConfigPath);
        Require(apiPort is >= 5080 and <= 5099 && !PortInUse(apiPort), "ISSUE26_CANCEL_API_PORT_INVALID");
        await using (var verify = new SqlConnection(adminConnectionString))
        {
            await verify.OpenAsync();
            await Issue11CartAcceptance.VerifyFixtureMarkerAsync(verify);
            await using var maintenance = new SqlCommand("SELECT Maintenance FROM identity_access.SecurityState WHERE Id=1", verify);
            Require(Equals(await maintenance.ExecuteScalarAsync(), false), "ISSUE26_CANCEL_MAINTENANCE_WINDOW_NOT_OPEN");
        }

        var local = Path.GetDirectoryName(Path.GetFullPath(configPath))!;
        var statePath = Path.Combine(local, "issue26-cancellation-private.json");
        Require(!File.Exists(statePath) && new FileInfo(statePath).LinkTarget is null, "ISSUE26_CANCEL_PRIVATE_STATE_ALREADY_EXISTS");
        using var provisioner = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(local, "provisioner-private.json")));
        var fixtureId = Guid.Parse(provisioner.RootElement.GetProperty("fixtureId").GetString()!);
        var suffix = Guid.NewGuid().ToString("N");
        var owner = NewActor("owner", suffix);
        var foreign = NewActor("foreign", suffix);
        var ids = new RunState("issue26_cancel_order_" + suffix, "issue26_cancel_checkout_" + suffix, "", 0, 0, owner, foreign);
        await SavePrivateState(statePath, ids);
        var seeded = false;
        Process? api = null;
        var client = new HttpClient(new HttpClientHandler { UseCookies = false }) { Timeout = TimeSpan.FromSeconds(20) };
        var actors = new List<(Dictionary<string, string> Cookies, string Token)>();
        try
        {
            ids = await SeedAsync(adminConnectionString, ids);
            seeded = true;
            await SavePrivateState(statePath, ids);
            api = StartApi(Directory.GetCurrentDirectory(), apiConfigPath, apiPort);
            var baseUri = new Uri($"http://127.0.0.1:{apiPort}/api/v1/");
            await WaitForApi(client, new Uri($"http://127.0.0.1:{apiPort}/health"), api);
            var ownerSession = await Login(client, baseUri, settings.Origin, owner);
            actors.Add(ownerSession);
            var foreignSession = await Login(client, baseUri, settings.Origin, foreign);
            actors.Add(foreignSession);

            var version1 = CommerceWire.Version("Order", ids.OrderId, 1);
            var before = await ReadBusinessState(adminConnectionString, ids);
            Require(before == new BusinessState("AwaitingPayment", "Created", "AwaitingPayment", "Created", "Held", ids.StockBefore - 1, 0),
                "ISSUE26_CANCEL_INITIAL_SQL_STATE_INVALID");

            var foreignDenied = await Send(client, baseUri, $"orders/{ids.OrderId}/cancellations", HttpMethod.Post,
                foreignSession.Cookies, new JsonObject { ["expectedVersion"] = version1 }, foreignSession.Token,
                settings.Origin, version1, "issue26-cancel-foreign-" + suffix);
            Require(foreignDenied.Status == HttpStatusCode.NotFound, "ISSUE26_CANCEL_CROSS_MEMBER_NOT_DENIED");
            Require(await ReadBusinessState(adminConnectionString, ids) == before,
                "ISSUE26_CANCEL_CROSS_MEMBER_DENIAL_MUTATED_STATE");

            var key = "issue26-cancel-owner-" + suffix;
            var requestBody = new JsonObject { ["expectedVersion"] = version1 };
            var cancelled = await Send(client, baseUri, $"orders/{ids.OrderId}/cancellations", HttpMethod.Post,
                ownerSession.Cookies, requestBody, ownerSession.Token, settings.Origin, version1, key);
            Require(cancelled.Status == HttpStatusCode.Accepted && cancelled.Body["state"]?.GetValue<string>() == "Cancelled",
                "ISSUE26_CANCEL_OWNER_REQUEST_FAILED");
            var committed = await ReadBusinessState(adminConnectionString, ids);
            Require(committed == new BusinessState("Cancelled", "Created", "Cancelled", "Created", "Released", ids.StockBefore, 1),
                "ISSUE26_CANCEL_RESERVATION_OR_STOCK_SQL_STATE_INVALID");

            var replay = await Send(client, baseUri, $"orders/{ids.OrderId}/cancellations", HttpMethod.Post,
                ownerSession.Cookies, requestBody, ownerSession.Token, settings.Origin, version1, key);
            Require(replay.Status == HttpStatusCode.Accepted && replay.Body.ToJsonString() == cancelled.Body.ToJsonString(),
                "ISSUE26_CANCEL_IDEMPOTENT_REPLAY_MISMATCH");
            Require(await ReadBusinessState(adminConnectionString, ids) == committed,
                "ISSUE26_CANCEL_IDEMPOTENT_REPLAY_MUTATED_STATE");

            var stale = await Send(client, baseUri, $"orders/{ids.OrderId}/cancellations", HttpMethod.Post,
                ownerSession.Cookies, requestBody, ownerSession.Token, settings.Origin, version1, "issue26-cancel-stale-" + suffix);
            Require(stale.Status == HttpStatusCode.PreconditionFailed && stale.Body["code"]?.GetValue<string>() == "VERSION_CHANGED",
                "ISSUE26_CANCEL_STALE_VERSION_NOT_REJECTED");
            Require(await ReadBusinessState(adminConnectionString, ids) == committed,
                "ISSUE26_CANCEL_STALE_VERSION_MUTATED_STATE");
            Console.WriteLine("PASS Issue26 cancellation live HTTP/SQL: owner 202, same-key replay, stale-version rejection, foreign-member 404, reservation release and stock restored");
        }
        finally
        {
            foreach (var (cookies, token) in actors)
                if (cookies.ContainsKey("kohami.session"))
                {
                    try { await Send(client, new Uri($"http://127.0.0.1:{apiPort}/api/v1/"), "auth/session", HttpMethod.Delete, cookies, null, token, settings.Origin); }
                    catch { }
                }
            if (api is { HasExited: false }) { api.Kill(true); await api.WaitForExitAsync(); }
            client.Dispose();
            if (seeded) await CleanupAndVerifyAsync(adminConnectionString, fixtureId, ids);
            if (File.Exists(statePath)) File.Delete(statePath);
        }
    }

    private static Actor NewActor(string role, string suffix) => new("issue26_cancel_" + role + "_" + suffix,
        "issue26_cancel_" + role + "_" + suffix + "@example.test",
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_'));

    private static async Task<RunState> SeedAsync(string cs, RunState state)
    {
        var (sku, unitPrice, stockBefore) = await SelectSkuAsync(cs);
        state = state with { Sku = sku, UnitPrice = unitPrice, StockBefore = stockBefore };
        var ownerHash = new PasswordHasher<string>().HashPassword(state.Owner.Email, state.Owner.Password);
        var foreignHash = new PasswordHasher<string>().HashPassword(state.Foreign.Email, state.Foreign.Password);
        var now = DateTimeOffset.UtcNow;
        var expires = now.AddMinutes(15);
        var snapshot = CommerceWire.Snapshot(new JsonArray(CommerceWire.Item(sku, 1, unitPrice, stockBefore)), 60, false);
        var line = snapshot["lines"]!.AsArray()[0]!.AsObject();
        line["orderLineId"] = "issue26_cancel_line_" + state.OrderId[^32..];
        line["units"]!.AsArray()[0]!.AsObject()["unitId"] = "issue26_cancel_unit_" + state.OrderId[^32..];
        var body = CommerceWire.Obj(("checkoutId", state.CheckoutId), ("orderId", state.OrderId), ("mode", "mock"),
            ("version", CommerceWire.Version("Checkout", state.CheckoutId, 1)), ("expiresAt", expires), ("snapshot", snapshot),
            ("statusUrl", "/api/v1/checkouts/" + state.CheckoutId + "/status"), ("capabilities", new[] { "checkout.read", "checkout.pay" }),
            ("orderState", "AwaitingPayment"), ("paymentState", "Created"), ("compensationRequired", false),
            ("placedAt", now), ("email", state.Owner.Email));
        var orderBody = (JsonObject)body.DeepClone();
        orderBody["id"] = state.OrderId;
        orderBody["version"] = CommerceWire.Version("Order", state.OrderId, 1);

        await using var connection = new SqlConnection(cs); await connection.OpenAsync();
        await Issue11CartAcceptance.VerifyFixtureMarkerAsync(connection);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        await using (var seed = new SqlCommand("""
            INSERT identity_access.Users(UserId,Email,PasswordHash,EmailConfirmed,SecurityHold,Status,SecurityVersion)
            VALUES(@owner,@ownerEmail,@ownerHash,1,0,'Active',1),(@foreign,@foreignEmail,@foreignHash,1,0,'Active',1);
            UPDATE commerce.Inventory SET Stock=Stock-1 WHERE Dataset='kohami_local' AND Sku=@sku AND Stock=@stock AND Stock>=1;
            IF @@ROWCOUNT<>1 THROW 51000,'ISSUE26_CANCEL_STOCK_CHANGED',1;
            INSERT commerce.Reservations VALUES('kohami_local',@order,@sku,1,'Held',@expires);
            INSERT commerce.Documents(Dataset,Kind,Id,OwnerId,Version,Body)
            VALUES('kohami_local','Checkout',@checkout,@owner,1,@body),('kohami_local','Order',@order,@owner,1,@orderBody);
            """, connection, transaction))
        {
            seed.Parameters.AddWithValue("@owner", state.Owner.UserId); seed.Parameters.AddWithValue("@ownerEmail", state.Owner.Email); seed.Parameters.AddWithValue("@ownerHash", ownerHash);
            seed.Parameters.AddWithValue("@foreign", state.Foreign.UserId); seed.Parameters.AddWithValue("@foreignEmail", state.Foreign.Email); seed.Parameters.AddWithValue("@foreignHash", foreignHash);
            seed.Parameters.AddWithValue("@sku", sku); seed.Parameters.AddWithValue("@stock", stockBefore); seed.Parameters.AddWithValue("@order", state.OrderId);
            seed.Parameters.AddWithValue("@checkout", state.CheckoutId); seed.Parameters.AddWithValue("@expires", expires);
            seed.Parameters.AddWithValue("@body", body.ToJsonString()); seed.Parameters.AddWithValue("@orderBody", orderBody.ToJsonString());
            await seed.ExecuteNonQueryAsync();
        }
        await transaction.CommitAsync();
        return state;
    }

    private static async Task<(string Sku, long UnitPrice, int Stock)> SelectSkuAsync(string cs)
    {
        await using var connection = new SqlConnection(cs); await connection.OpenAsync();
        await Issue11CartAcceptance.VerifyFixtureMarkerAsync(connection);
        await using var command = new SqlCommand("SELECT TOP(1) Sku,Price,Stock FROM commerce.Inventory WHERE Dataset='kohami_local' AND Active=1 AND Stock>=1 AND Price BETWEEN 1 AND 49900 ORDER BY Sku", connection);
        await using var reader = await command.ExecuteReaderAsync();
        Require(await reader.ReadAsync(), "ISSUE26_CANCEL_STOCK_UNAVAILABLE");
        return (reader.GetString(0), reader.GetInt64(1), reader.GetInt32(2));
    }

    private static async Task<(Dictionary<string, string> Cookies, string Token)> Login(HttpClient client, Uri baseUri, string origin, Actor actor)
    {
        var cookies = new Dictionary<string, string>(StringComparer.Ordinal);
        var csrf = await Send(client, baseUri, "auth/csrf", HttpMethod.Get, cookies, null, null, origin);
        Require(csrf.Status == HttpStatusCode.OK && csrf.Body["requestToken"] is JsonValue, "ISSUE26_CANCEL_CSRF_SETUP_FAILED");
        var token = csrf.Body["requestToken"]!.GetValue<string>();
        var login = await Send(client, baseUri, "auth/sessions", HttpMethod.Post, cookies,
            new JsonObject { ["email"] = actor.Email, ["password"] = actor.Password }, token, origin);
        Require(login.Status == HttpStatusCode.OK && login.Body["state"]?.GetValue<string>() == "MemberAuthenticated",
            "ISSUE26_CANCEL_MEMBER_LOGIN_FAILED");
        Require(cookies.ContainsKey("kohami.session"), "ISSUE26_CANCEL_SESSION_COOKIE_MISSING");
        return (cookies, token);
    }

    private static async Task<Response> Send(HttpClient client, Uri baseUri, string path, HttpMethod method,
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
        return new(response.StatusCode, result);
    }

    private static async Task<BusinessState> ReadBusinessState(string cs, RunState state)
    {
        await using var connection = new SqlConnection(cs); await connection.OpenAsync();
        await Issue11CartAcceptance.VerifyFixtureMarkerAsync(connection);
        await using var command = new SqlCommand("""
            SELECT JSON_VALUE(o.Body,'$.orderState'),JSON_VALUE(o.Body,'$.paymentState'),
              JSON_VALUE(c.Body,'$.orderState'),JSON_VALUE(c.Body,'$.paymentState'),
              (SELECT TOP(1) State FROM commerce.Reservations WHERE Dataset='kohami_local' AND OrderId=@o AND Sku=@s),
              (SELECT Stock FROM commerce.Inventory WHERE Dataset='kohami_local' AND Sku=@s),
              (SELECT COUNT(*) FROM commerce.Idempotency WHERE Dataset='kohami_local' AND Actor=@a AND Scope='cancel/'+@o)
            FROM commerce.Documents o JOIN commerce.Documents c ON c.Dataset=o.Dataset AND c.Kind='Checkout' AND c.Id=@c
            WHERE o.Dataset='kohami_local' AND o.Kind='Order' AND o.Id=@o AND o.OwnerId=@a;
            """, connection);
        command.Parameters.AddWithValue("@o", state.OrderId); command.Parameters.AddWithValue("@c", state.CheckoutId);
        command.Parameters.AddWithValue("@s", state.Sku); command.Parameters.AddWithValue("@a", state.Owner.UserId);
        await using var reader = await command.ExecuteReaderAsync();
        Require(await reader.ReadAsync(), "ISSUE26_CANCEL_BUSINESS_READBACK_MISSING");
        return new(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
            reader.GetString(4), reader.GetInt32(5), reader.GetInt32(6));
    }

    private static async Task CleanupAndVerifyAsync(string cs, Guid fixtureId, RunState state)
    {
        await using (var connection = new SqlConnection(cs))
        {
            await connection.OpenAsync();
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
            await using var guard = new SqlCommand("IF NOT EXISTS(SELECT 1 FROM ops.DevelopmentFixtureMarker WHERE FixtureId=@fixture AND DatasetId='kohami_local' AND InstanceFingerprint=LOWER(CONVERT(varchar(64),HASHBYTES('SHA2_256',CONCAT(CONVERT(nvarchar(100),SERVERPROPERTY('ServerName')),'|Kohami|kohami_local')),2))) THROW 51000,'ISSUE26_CANCEL_FIXTURE_MARKER_INVALID',1", connection, transaction);
            guard.Parameters.AddWithValue("@fixture", fixtureId);
            await guard.ExecuteNonQueryAsync();
            await using var remove = new SqlCommand("""
                DELETE commerce.Reservations WHERE Dataset='kohami_local' AND OrderId=@o AND Sku=@s;
                DELETE commerce.Collections WHERE Dataset='kohami_local' AND OrderId=@o;
                DELETE commerce.RefundUnits WHERE Dataset='kohami_local' AND OrderId=@o;
                DELETE commerce.Idempotency WHERE Dataset='kohami_local' AND Actor IN(@a,@b);
                DELETE commerce.Documents WHERE Dataset='kohami_local' AND (OwnerId IN(@a,@b) OR Id IN(@o,@c));
                DELETE identity_access.Grants WHERE UserId IN(@a,@b);
                DELETE identity_access.SecurityAudit WHERE UserId IN(@a,@b);
                DELETE identity_access.Sessions WHERE UserId IN(@a,@b);
                DELETE identity_access.AuthFailures WHERE AccountKey IN(
                  LOWER(CONVERT(varchar(64),HASHBYTES('SHA2_256',LOWER(@ae)),2)),
                  LOWER(CONVERT(varchar(64),HASHBYTES('SHA2_256',LOWER(@be)),2)));
                DELETE identity_access.Users WHERE (UserId=@a AND Email=@ae) OR (UserId=@b AND Email=@be);
                UPDATE commerce.Inventory SET Stock=@stock
                  WHERE Dataset='kohami_local' AND Sku=@s AND Stock IN(@stock,@reserved);
                IF @@ROWCOUNT<>1 THROW 51000,'ISSUE26_CANCEL_STOCK_RESTORE_CONFLICT',1;
                """, connection, transaction);
            AddStateParameters(remove, state);
            await remove.ExecuteNonQueryAsync();
            await transaction.CommitAsync();
        }
        await using (var connection = new SqlConnection(cs))
        {
            await connection.OpenAsync(); await Issue11CartAcceptance.VerifyFixtureMarkerAsync(connection);
            await using var readback = new SqlCommand("""
                SELECT
                  (SELECT COUNT(*) FROM identity_access.Users WHERE UserId IN(@a,@b)) AS UsersLeft,
                  (SELECT COUNT(*) FROM identity_access.Grants WHERE UserId IN(@a,@b)) AS GrantsLeft,
                  (SELECT COUNT(*) FROM identity_access.Sessions WHERE UserId IN(@a,@b)) AS SessionsLeft,
                  (SELECT COUNT(*) FROM identity_access.SecurityAudit WHERE UserId IN(@a,@b)) AS AuditLeft,
                  (SELECT COUNT(*) FROM identity_access.AuthFailures WHERE AccountKey IN(
                    LOWER(CONVERT(varchar(64),HASHBYTES('SHA2_256',LOWER(@ae)),2)),
                    LOWER(CONVERT(varchar(64),HASHBYTES('SHA2_256',LOWER(@be)),2)))) AS AuthFailuresLeft,
                  (SELECT COUNT(*) FROM commerce.Documents WHERE Dataset='kohami_local' AND (OwnerId IN(@a,@b) OR Id IN(@o,@c))) AS DocumentsLeft,
                  (SELECT COUNT(*) FROM commerce.Collections WHERE Dataset='kohami_local' AND OrderId=@o) AS CollectionsLeft,
                  (SELECT COUNT(*) FROM commerce.Reservations WHERE Dataset='kohami_local' AND OrderId=@o) AS ReservationsLeft,
                  (SELECT COUNT(*) FROM commerce.RefundUnits WHERE Dataset='kohami_local' AND OrderId=@o) AS RefundUnitsLeft,
                  (SELECT COUNT(*) FROM commerce.Idempotency WHERE Dataset='kohami_local' AND Actor IN(@a,@b)) AS IdempotencyLeft,
                  (SELECT COUNT(*) FROM commerce.WalletLedger WHERE Dataset='kohami_local' AND UserId IN(@a,@b)) AS WalletLedgerLeft,
                  (SELECT COUNT(*) FROM commerce.WalletAccounts WHERE Dataset='kohami_local' AND UserId IN(@a,@b)) AS WalletAccountsLeft,
                  (SELECT Stock FROM commerce.Inventory WHERE Dataset='kohami_local' AND Sku=@s) AS StockAfterCleanup;
                """, connection);
            AddStateParameters(readback, state);
            await using var reader = await readback.ExecuteReaderAsync();
            Require(await reader.ReadAsync(), "ISSUE26_CANCEL_CLEANUP_READBACK_MISSING");
            for (var column = 0; column < 12; column++) Require(reader.GetInt32(column) == 0, "ISSUE26_CANCEL_SYNTHETIC_RESIDUE_DETECTED");
            Require(reader.GetInt32(12) == state.StockBefore, "ISSUE26_CANCEL_STOCK_RESTORE_READBACK_FAILED");
        }
        Console.WriteLine("PASS Issue26 cancellation exact cleanup: both synthetic members, sessions/audit/auth failures, order/checkout, all actor idempotency, reservation, and SKU stock readback");
    }

    private static void AddStateParameters(SqlCommand command, RunState state)
    {
        command.Parameters.AddWithValue("@a", state.Owner.UserId); command.Parameters.AddWithValue("@ae", state.Owner.Email);
        command.Parameters.AddWithValue("@b", state.Foreign.UserId); command.Parameters.AddWithValue("@be", state.Foreign.Email);
        command.Parameters.AddWithValue("@o", state.OrderId); command.Parameters.AddWithValue("@c", state.CheckoutId);
        command.Parameters.AddWithValue("@s", state.Sku); command.Parameters.AddWithValue("@stock", state.StockBefore);
        command.Parameters.AddWithValue("@reserved", state.StockBefore - 1);
    }

    private static async Task SavePrivateState(string path, RunState state)
    {
        await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        await JsonSerializer.SerializeAsync(stream, state); await stream.WriteAsync("\n"u8.ToArray());
    }

    private static Process StartApi(string root, string config, int port)
    {
        var dll = Path.Combine(root, "apps/api/bin/Release/net10.0/Api.dll"); Require(File.Exists(dll), "ISSUE26_CANCEL_API_BUILD_REQUIRED");
        var start = new ProcessStartInfo("/usr/local/share/dotnet/dotnet", dll) { WorkingDirectory = root, UseShellExecute = false,
            RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        start.Environment["KOHAMI_CONFIG"] = config; start.Environment["KOHAMI_API_PORT"] = port.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var process = Process.Start(start) ?? throw new InvalidOperationException("ISSUE26_CANCEL_API_START_FAILED");
        process.OutputDataReceived += (_, _) => { }; process.ErrorDataReceived += (_, _) => { }; process.BeginOutputReadLine(); process.BeginErrorReadLine(); return process;
    }

    private static async Task WaitForApi(HttpClient client, Uri health, Process process)
    {
        for (var attempt = 0; attempt < 60; attempt++)
        {
            if (process.HasExited) throw new InvalidOperationException("ISSUE26_CANCEL_API_EXITED");
            try { using var response = await client.GetAsync(health); if (response.IsSuccessStatusCode) return; }
            catch (HttpRequestException) { }
            await Task.Delay(250);
        }
        throw new TimeoutException("ISSUE26_CANCEL_API_START_TIMEOUT");
    }

    private static bool PortInUse(int port)
    {
        try { using var client = new System.Net.Sockets.TcpClient(); var pending = client.ConnectAsync("127.0.0.1", port); return pending.Wait(250) && client.Connected; }
        catch { return false; }
    }

    private static void Require(bool condition, string code) { if (!condition) throw new InvalidOperationException(code); }
}
