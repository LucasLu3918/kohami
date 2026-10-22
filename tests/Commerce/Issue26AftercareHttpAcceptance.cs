using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Kohami.Commerce;
using Kohami.Core;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;

namespace Kohami.Commerce.Tests;

internal static class Issue26AftercareHttpAcceptance
{
    private sealed record Actor(string UserId, string Email, string Password, string[] GrantIds);
    private sealed record RunState(string OrderId, string LineId, string UnitId, string CollectionId,
        string ReceiptId, string ShipmentId, string ReturnId, string MovementId, string RefundCaseId,
        string Sku, int StockBefore, Actor MemberA, Actor MemberB, Actor Support, string[] IdempotencyKeys);
    private sealed record Response(HttpStatusCode Status, JsonObject Body);

    public static async Task CleanupResidualAsync(string configPath, string statePath)
    {
        var expectedDirectory = Path.GetDirectoryName(Path.GetFullPath(configPath));
        var file = new FileInfo(statePath);
        if (!file.Exists || file.LinkTarget is not null || Path.GetDirectoryName(file.FullName) != expectedDirectory)
            throw new InvalidOperationException("ISSUE26_AFTERCARE_RECOVERY_STATE_PATH_INVALID");
        var state = JsonSerializer.Deserialize<RunState>(await File.ReadAllTextAsync(file.FullName))
            ?? throw new InvalidOperationException("ISSUE26_AFTERCARE_RECOVERY_STATE_INVALID");
        if (!state.OrderId.StartsWith("order_issue26_", StringComparison.Ordinal)
            || !state.Sku.StartsWith("sku_issue26_", StringComparison.Ordinal)
            || !state.UnitId.StartsWith("unit_issue26_", StringComparison.Ordinal)
            || !state.IdempotencyKeys.All(key => key.StartsWith("issue26-", StringComparison.Ordinal))
            || !state.MemberA.UserId.StartsWith("issue26_", StringComparison.Ordinal)
            || !state.MemberB.UserId.StartsWith("issue26_", StringComparison.Ordinal)
            || !state.Support.UserId.StartsWith("issue26_", StringComparison.Ordinal))
            throw new InvalidOperationException("ISSUE26_AFTERCARE_RECOVERY_STATE_SCOPE_INVALID");

        var (_, connectionString) = Issue11CartAcceptance.DedicatedAdmin(configPath);
        await using (var connection = new SqlConnection(connectionString))
        {
            await connection.OpenAsync();
            await Issue11CartAcceptance.VerifyFixtureMarkerAsync(connection);
            await using var maintenance = new SqlCommand("SELECT Maintenance FROM identity_access.SecurityState WHERE Id=1", connection);
            if (!Equals(await maintenance.ExecuteScalarAsync(), true))
                throw new InvalidOperationException("ISSUE26_AFTERCARE_RECOVERY_MAINTENANCE_REQUIRED");
        }

        await CleanupAndVerify(connectionString, state);
        File.Delete(file.FullName);
        Console.WriteLine("PASS Issue26 aftercare residual exact cleanup and SQL readback");
    }

    public static async Task RunAsync(string configPath, string apiConfigPath, int apiPort)
    {
        var (settings, adminCs) = Issue11CartAcceptance.DedicatedAdmin(configPath);
        apiConfigPath = Path.GetFullPath(apiConfigPath);
        await using (var verify = new SqlConnection(adminCs))
        {
            await verify.OpenAsync();
            await Issue11CartAcceptance.VerifyFixtureMarkerAsync(verify);
            await using var maintenance = new SqlCommand("SELECT Maintenance FROM identity_access.SecurityState WHERE Id=1", verify);
            if (!Equals(await maintenance.ExecuteScalarAsync(), false)) throw new InvalidOperationException("ISSUE26_MAINTENANCE_WINDOW_NOT_OPEN");
        }
        var runtimePath = Path.GetFullPath(configPath);
        var statePath = Path.Combine(Path.GetDirectoryName(runtimePath)!, "issue26-aftercare-private.json");
        if (File.Exists(statePath) || new FileInfo(statePath).LinkTarget is not null)
            throw new InvalidOperationException("ISSUE26_PRIVATE_STATE_ALREADY_EXISTS");
        if (PortInUse(apiPort)) throw new InvalidOperationException("ISSUE26_API_PORT_IN_USE");
        var suffix = Guid.NewGuid().ToString("N");
        var sku = "sku_issue26_" + suffix;
        const int stock = 0;
        var a = NewActor("member_a", suffix);
        var b = NewActor("member_b", suffix);
        var support = NewActor("support", suffix);
        var orderId = "order_issue26_" + suffix;
        var lineId = "line_issue26_" + suffix;
        var unitId = "unit_issue26_" + suffix;
        var collectionId = "collection_issue26_" + suffix;
        var receiptId = "receipt_issue26_" + suffix;
        var grants = new[] { "shipment.manage", "support.respond", "return.inspect", "orders.read", "refund.request", "refund.check" }
            .Select(_ => "grant_issue26_" + Guid.NewGuid().ToString("N")).ToArray();
        support = support with { GrantIds = grants };
        var keys = new[] { "issue26-b-denied-return-" + suffix, "issue26-b-denied-cancel-" + suffix,
            "issue26-shipment-create-" + suffix, "issue26-shipment-shipped-" + suffix,
            "issue26-shipment-transit-" + suffix, "issue26-shipment-delivered-" + suffix,
            "issue26-return-request-" + suffix, "issue26-return-accept-" + suffix,
            "issue26-return-receive-" + suffix, "issue26-return-inspect-" + suffix };
        var shipmentId = ""; var returnId = ""; var movementId = ""; var refundCaseId = "";
        var ids = new RunState(orderId, lineId, unitId, collectionId, receiptId, shipmentId, returnId,
            movementId, refundCaseId, sku, stock, a, b, support, keys);
        await SavePrivateState(statePath, ids);
        Process? api = null;
        var clients = new List<(HttpClient Client, Dictionary<string, string> Cookies, string Token)>();
        try
        {
            await Seed(adminCs, settings, ids);
            api = StartApi(Directory.GetCurrentDirectory(), apiConfigPath, apiPort);
            using var client = new HttpClient(new HttpClientHandler { UseCookies = false }) { Timeout = TimeSpan.FromSeconds(20) };
            var baseUri = new Uri($"http://127.0.0.1:{apiPort}/api/v1/");
            await WaitForApi(client, new Uri($"http://127.0.0.1:{apiPort}/health"), api);
            var memberA = await Login(client, baseUri, settings.Origin, a);
            clients.Add((client, memberA.Cookies, memberA.Token));
            var memberB = await Login(client, baseUri, settings.Origin, b);
            clients.Add((client, memberB.Cookies, memberB.Token));
            var customerSupport = await Login(client, baseUri, settings.Origin, support, "AdminAuthenticated");
            clients.Add((client, customerSupport.Cookies, customerSupport.Token));

            var ownList = await Send(client, baseUri, "orders", HttpMethod.Get, memberA.Cookies, null, memberA.Token, settings.Origin);
            Require(ownList.Status == HttpStatusCode.OK && ContainsOrder(ownList.Body, orderId), "ISSUE26_MEMBER_A_ORDER_LIST_MISSING");
            var foreignList = await Send(client, baseUri, "orders", HttpMethod.Get, memberB.Cookies, null, memberB.Token, settings.Origin);
            Require(foreignList.Status == HttpStatusCode.OK && !ContainsOrder(foreignList.Body, orderId), "ISSUE26_MEMBER_B_ORDER_LIST_LEAK");
            var before = await ReadBusinessSnapshot(adminCs, ids);
            var version1 = CommerceWire.Version("Order", orderId, 1);
            RequireStatus(await Send(client, baseUri, $"orders/{orderId}", HttpMethod.Get, memberB.Cookies, null, memberB.Token, settings.Origin), HttpStatusCode.NotFound, "cross-member order read");
            RequireStatus(await Send(client, baseUri, $"orders/{orderId}/receipts", HttpMethod.Get, memberB.Cookies, null, memberB.Token, settings.Origin), HttpStatusCode.NotFound, "cross-member receipt read");
            RequireStatus(await Send(client, baseUri, $"orders/{orderId}/returns", HttpMethod.Post, memberB.Cookies,
                new JsonObject { ["orderLineId"] = lineId, ["quantity"] = 1, ["reason"] = "cross member" }, memberB.Token,
                settings.Origin, idempotency: "issue26-b-denied-return-" + suffix), HttpStatusCode.NotFound, "cross-member return");
            RequireStatus(await Send(client, baseUri, $"orders/{orderId}/cancellations", HttpMethod.Post, memberB.Cookies,
                new JsonObject { ["expectedVersion"] = version1 }, memberB.Token, settings.Origin,
                version1, "issue26-b-denied-cancel-" + suffix), HttpStatusCode.NotFound, "cross-member cancellation");
            Require(await ReadBusinessSnapshot(adminCs, ids) == before, "ISSUE26_CROSS_MEMBER_DENIAL_MUTATED_STATE");

            var ownOrder = await Send(client, baseUri, $"orders/{orderId}", HttpMethod.Get, memberA.Cookies, null, memberA.Token, settings.Origin);
            if (ownOrder.Status != HttpStatusCode.OK || ownOrder.Body["id"]?.GetValue<string>() != orderId)
                throw new InvalidOperationException($"ISSUE26_MEMBER_ORDER_READ_FAILED_{(int)ownOrder.Status}_{ownOrder.Body["code"]?.GetValue<string>() ?? "NO_CODE"}");
            var receipts = await Send(client, baseUri, $"orders/{orderId}/receipts", HttpMethod.Get, memberA.Cookies, null, memberA.Token, settings.Origin);
            Require(receipts.Status == HttpStatusCode.OK && receipts.Body["receipts"] is JsonArray, "ISSUE26_MEMBER_RECEIPT_READ_FAILED");

            var version = version1;
            var created = await Send(client, baseUri, $"admin/orders/{orderId}/shipments", HttpMethod.Post, customerSupport.Cookies,
                new JsonObject { ["expectedVersion"] = version }, customerSupport.Token, settings.Origin, version, keys[2]);
            Require(created.Status == HttpStatusCode.Created, "ISSUE26_SHIPMENT_CREATE_FAILED_" + (int)created.Status);
            shipmentId = created.Body["id"]!.GetValue<string>();
            ids = ids with { ShipmentId = shipmentId };
            version = CommerceWire.Version("Shipment", shipmentId, 1);
            foreach (var (state, key) in new[] { ("Shipped", keys[3]), ("InTransit", keys[4]), ("Delivered", keys[5]) })
            {
                var transitioned = await Send(client, baseUri, $"admin/shipments/{shipmentId}/transitions", HttpMethod.Post,
                    customerSupport.Cookies, new JsonObject { ["state"] = state, ["eventId"] = "issue26-" + state.ToLowerInvariant() + "-" + suffix, ["expectedVersion"] = version },
                    customerSupport.Token, settings.Origin, version, key);
                Require(transitioned.Status == HttpStatusCode.OK, "ISSUE26_SHIPMENT_TRANSITION_FAILED_" + state);
                version = CommerceWire.Version("Shipment", shipmentId, state == "Delivered" ? 4 : state == "InTransit" ? 3 : 2);
            }

            var returnEligibleOrder = await Send(client, baseUri, $"orders/{orderId}", HttpMethod.Get,
                memberA.Cookies, null, memberA.Token, settings.Origin);
            Require(returnEligibleOrder.Status == HttpStatusCode.OK && ActionAllowed(returnEligibleOrder.Body, "RequestReturn")
                && ReturnableQuantity(returnEligibleOrder.Body, lineId) == 2,
                "ISSUE26_RETURN_ACTION_NOT_ALLOWED");

            var requested = await Send(client, baseUri, $"orders/{orderId}/returns", HttpMethod.Post, memberA.Cookies,
                new JsonObject { ["orderLineId"] = lineId, ["quantity"] = 1, ["reason"] = "synthetic aftercare acceptance" },
                memberA.Token, settings.Origin, idempotency: keys[6]);
            Require(requested.Status == HttpStatusCode.Created, "ISSUE26_RETURN_REQUEST_FAILED");
            returnId = requested.Body["id"]!.GetValue<string>(); ids = ids with { ReturnId = returnId };
            var remainingReturnableOrder = await Send(client, baseUri, $"orders/{orderId}", HttpMethod.Get,
                memberA.Cookies, null, memberA.Token, settings.Origin);
            Require(remainingReturnableOrder.Status == HttpStatusCode.OK
                && ActionAllowed(remainingReturnableOrder.Body, "RequestReturn")
                && ReturnableQuantity(remainingReturnableOrder.Body, lineId) == 1,
                "ISSUE26_REMAINING_RETURNABLE_QUANTITY_INVALID");
            Console.WriteLine("PASS Issue26 owner GET Order return eligibility: available quantity 2 before request; 1 remaining after one-unit return request");
            _ = await TransitionReturn(client, baseUri, customerSupport, settings.Origin, returnId, "Accepted", 1, keys[7]);
            var received = await TransitionReturn(client, baseUri, customerSupport, settings.Origin, returnId, "Received", 2, keys[8]);
            var inspected = await Send(client, baseUri, $"admin/returns/{returnId}/inspections", HttpMethod.Post, customerSupport.Cookies,
                new JsonObject { ["resellableQuantity"] = 1, ["reason"] = "synthetic item inspection", ["expectedVersion"] = VersionFrom(received.Body, "version", "Return", returnId, 3) },
                customerSupport.Token, settings.Origin, VersionFrom(received.Body, "version", "Return", returnId, 3), keys[9]);
            Require(inspected.Status == HttpStatusCode.OK && inspected.Body["state"]?.GetValue<string>() == "Inspected", "ISSUE26_RETURN_INSPECTION_FAILED");
            var inspection = inspected.Body["inspection"]?.AsObject() ?? throw new InvalidOperationException("ISSUE26_INSPECTION_RESULT_MISSING");
            refundCaseId = inspection["refundCaseId"]!.GetValue<string>();
            movementId = inspection["inventoryMovementId"]!.GetValue<string>();
            ids = ids with { ReturnId = returnId, RefundCaseId = refundCaseId, MovementId = movementId };
            var aftercareState = await ReadInspectionState(adminCs, ids);
            Require(aftercareState == ("Inspected", "Processing", "Requested", 1, 1), "ISSUE26_AFTERCARE_SQL_READBACK_INVALID");
            var memberAftercare = await Send(client, baseUri, $"orders/{orderId}", HttpMethod.Get, memberA.Cookies, null, memberA.Token, settings.Origin);
            Require(memberAftercare.Status == HttpStatusCode.OK && ContainsReturn(memberAftercare.Body, returnId, "Inspected"), "ISSUE26_MEMBER_AFTERCARE_PROJECTION_MISSING");
            Console.WriteLine("PASS Issue26 member A/B aftercare HTTP journey, scoped support shipment/return inspection, and SQL RefundCase readback");
        }
        finally
        {
            foreach (var (client, cookies, token) in clients)
                if (cookies.ContainsKey("kohami.session")) { try { await Send(client, new Uri($"http://127.0.0.1:{apiPort}/api/v1/"), "auth/session", HttpMethod.Delete, cookies, null, token, settings.Origin); } catch { } }
            if (api is { HasExited: false }) { api.Kill(true); await api.WaitForExitAsync(); }
            await CleanupAndVerify(adminCs, ids);
            if (File.Exists(statePath)) File.Delete(statePath);
        }
        // Refund execution is intentionally outside this non-Owner acceptance; refund.execute is Owner-only.
        Console.WriteLine("DEFERRED Issue26 refund execution/worker terminal outcome requires Owner authority");
    }

    private static Actor NewActor(string kind, string suffix) => new("issue26_" + kind + "_" + suffix,
        "issue26_" + kind + "_" + suffix + "@example.test", Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)), []);

    private static async Task Seed(string cs, RuntimeSettings settings, RunState state)
    {
        var now = DateTimeOffset.UtcNow;
        var snapshot = CommerceWire.Snapshot(new JsonArray(CommerceWire.Item(state.Sku, 2, 500, 2)), 60, false);
        var line = snapshot["lines"]![0]!.AsObject(); line["orderLineId"] = state.LineId;
        line["units"]![0]!["unitId"] = state.UnitId; line["units"]![1]!["unitId"] = state.UnitId + "_remaining";
        var order = CommerceWire.Obj(("id", state.OrderId), ("orderId", state.OrderId), ("mode", "mock"), ("orderState", "Confirmed"),
            ("paymentState", "Succeeded"), ("paymentConfirmedAt", now.AddMinutes(-2)), ("compensationRequired", false),
            ("email", state.MemberA.Email), ("placedAt", now), ("snapshot", snapshot));
        await using var connection = new SqlConnection(cs); await connection.OpenAsync();
        await Issue11CartAcceptance.VerifyFixtureMarkerAsync(connection);
        await using var tx = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        await using (var seed = new SqlCommand("""
            INSERT identity_access.Users(UserId,Email,PasswordHash,EmailConfirmed,SecurityHold,Status,SecurityVersion)
            VALUES(@a,@ae,@ah,1,0,'Active',1),(@b,@be,@bh,1,0,'Active',1),(@s,@se,@sh,1,0,'Active',1);
            INSERT commerce.Documents(Dataset,Kind,Id,OwnerId,Version,Body) VALUES('kohami_local','Order',@o,@a,1,@body);
            INSERT commerce.Documents(Dataset,Kind,Id,OwnerId,Version,Body) VALUES('kohami_local','DemoReceipt',@receipt,@a,1,@receiptBody);
            INSERT commerce.Inventory(Dataset,Sku,Price,Stock,Active) VALUES('kohami_local',@sku,500,0,1);
            INSERT commerce.Collections(Dataset,Id,OrderId,Kind,Collected,Pending,Refunded,ShippingPaid)
            VALUES('kohami_local',@c,@o,'Primary',1060,0,0,60);
            INSERT commerce.RefundUnits(Dataset,UnitId,OrderId,LineId,CollectionId,Ordinal,Paid)
            VALUES('kohami_local',@unit,@o,@line,@c,0,500),('kohami_local',@remainingUnit,@o,@line,@c,1,500);
            """, connection, tx))
        {
            seed.Parameters.AddWithValue("@a", state.MemberA.UserId); seed.Parameters.AddWithValue("@ae", state.MemberA.Email); seed.Parameters.AddWithValue("@ah", Hash(state.MemberA));
            seed.Parameters.AddWithValue("@b", state.MemberB.UserId); seed.Parameters.AddWithValue("@be", state.MemberB.Email); seed.Parameters.AddWithValue("@bh", Hash(state.MemberB));
            seed.Parameters.AddWithValue("@s", state.Support.UserId); seed.Parameters.AddWithValue("@se", state.Support.Email); seed.Parameters.AddWithValue("@sh", Hash(state.Support));
            seed.Parameters.AddWithValue("@o", state.OrderId); seed.Parameters.AddWithValue("@body", order.ToJsonString());
            seed.Parameters.AddWithValue("@receipt", state.ReceiptId); seed.Parameters.AddWithValue("@receiptBody", CommerceWire.Obj(("id", state.ReceiptId), ("orderId", state.OrderId), ("collectionId", state.CollectionId), ("kind", "Receipt"), ("mode", "mock"), ("state", "Issued"), ("amountTwd", 1060), ("currency", "TWD"), ("taxEffect", "none"), ("displayNotice", "synthetic mock receipt"), ("createdAt", now)).ToJsonString());
            seed.Parameters.AddWithValue("@c", state.CollectionId); seed.Parameters.AddWithValue("@unit", state.UnitId); seed.Parameters.AddWithValue("@remainingUnit", state.UnitId + "_remaining"); seed.Parameters.AddWithValue("@line", state.LineId);
            seed.Parameters.AddWithValue("@sku", state.Sku);
            await seed.ExecuteNonQueryAsync();
        }
        var grantPermissions = new[] { "shipment.manage", "support.respond", "return.inspect", "orders.read", "refund.request", "refund.check" };
        for (var i = 0; i < grantPermissions.Length; i++)
        {
            await using var grant = new SqlCommand("INSERT identity_access.Grants(GrantId,UserId,Role,Permission,ScopeKind,ScopeId,ValidFrom,ValidUntil,Version,GrantedBy,Reason) VALUES(@id,@u,'CustomerSupport',@p,'Order',@o,DATEADD(minute,-1,SYSDATETIMEOFFSET()),DATEADD(hour,1,SYSDATETIMEOFFSET()),1,'issue26-synthetic','Dedicated Issue26 aftercare fixture')", connection, tx);
            grant.Parameters.AddWithValue("@id", state.Support.GrantIds[i]);
            grant.Parameters.AddWithValue("@u", state.Support.UserId); grant.Parameters.AddWithValue("@p", grantPermissions[i]); grant.Parameters.AddWithValue("@o", state.OrderId);
            await grant.ExecuteNonQueryAsync();
        }
        await using (var grantCheck = new SqlCommand("SELECT COUNT(*),SUM(CASE WHEN Role='Owner' OR Permission='refund.execute' THEN 1 ELSE 0 END) FROM identity_access.Grants WHERE UserId=@u AND ScopeKind='Order' AND ScopeId=@order", connection, tx))
        {
            grantCheck.Parameters.AddWithValue("@u", state.Support.UserId); grantCheck.Parameters.AddWithValue("@order", state.OrderId);
            await using var readback = await grantCheck.ExecuteReaderAsync();
            if (!await readback.ReadAsync() || readback.GetInt32(0) != grantPermissions.Length || readback.GetInt32(1) != 0)
                throw new InvalidOperationException("ISSUE26_SUPPORT_GRANT_SCOPE_INVALID");
        }
        await tx.CommitAsync();
        _ = settings;
    }

    private static string Hash(Actor actor) => new PasswordHasher<string>().HashPassword(actor.Email, actor.Password);

    private static async Task<(Dictionary<string,string> Cookies, string Token)> Login(HttpClient client, Uri baseUri, string origin, Actor actor, string expectedState = "MemberAuthenticated")
    {
        var cookies = new Dictionary<string, string>(StringComparer.Ordinal);
        var csrf = await Send(client, baseUri, "auth/csrf", HttpMethod.Get, cookies, null, null, origin);
        Require(csrf.Status == HttpStatusCode.OK && csrf.Body["requestToken"] is JsonValue, "ISSUE26_CSRF_SETUP_FAILED");
        var token = csrf.Body["requestToken"]!.GetValue<string>();
        var login = await Send(client, baseUri, "auth/sessions", HttpMethod.Post, cookies,
            new JsonObject { ["email"] = actor.Email, ["password"] = actor.Password }, token, origin);
        Require(login.Status == HttpStatusCode.OK && login.Body["state"]?.GetValue<string>() == expectedState, "ISSUE26_ACTOR_LOGIN_FAILED_" + (int)login.Status);
        Require(cookies.ContainsKey("kohami.session"), "ISSUE26_SESSION_COOKIE_MISSING");
        return (cookies, token);
    }

    private static async Task<Response> Send(HttpClient client, Uri baseUri, string path, HttpMethod method,
        IDictionary<string,string> cookies, JsonObject? body, string? csrf, string? origin, string? etag = null, string? idempotency = null)
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
            if (setCookie.Contains("Max-Age=0", StringComparison.OrdinalIgnoreCase) || value.Length == 0) cookies.Remove(name); else cookies[name] = value;
        }
        JsonObject result; try { result = JsonNode.Parse(await response.Content.ReadAsStringAsync()) as JsonObject ?? new(); } catch { result = new(); }
        return new(response.StatusCode, result);
    }

    private static async Task<Response> TransitionReturn(HttpClient c, Uri b, (Dictionary<string,string> Cookies, string Token) actor, string origin, string id, string state, int version, string key)
    {
        var tag = CommerceWire.Version("Return", id, version);
        var result = await Send(c, b, $"admin/returns/{id}/transitions", HttpMethod.Post, actor.Cookies,
            new JsonObject { ["state"] = state, ["expectedVersion"] = tag }, actor.Token, origin, tag, key);
        Require(result.Status == HttpStatusCode.OK, "ISSUE26_RETURN_TRANSITION_FAILED_" + state);
        return result;
    }

    private static async Task<(string ReturnState, string UnitSlot, string RefundCaseState, int Stock, int MovementQuantity)> ReadInspectionState(string cs, RunState s)
    {
        await using var c = new SqlConnection(cs); await c.OpenAsync();
        await using var cmd = new SqlCommand("SELECT JSON_VALUE(r.Body,'$.state'),u.Slot,JSON_VALUE(f.Body,'$.state'),i.Stock,TRY_CONVERT(int,JSON_VALUE(m.Body,'$.quantity')) FROM commerce.Documents r JOIN commerce.RefundUnits u ON u.Dataset='kohami_local' AND u.UnitId=@unit JOIN commerce.Documents f ON f.Dataset='kohami_local' AND f.Kind='RefundCase' AND f.Id=@refund JOIN commerce.Inventory i ON i.Dataset='kohami_local' AND i.Sku=@sku JOIN commerce.Documents m ON m.Dataset='kohami_local' AND m.Kind='InventoryMovement' AND m.Id=@movement WHERE r.Dataset='kohami_local' AND r.Kind='Return' AND r.Id=@ret", c);
        cmd.Parameters.AddWithValue("@unit", s.UnitId); cmd.Parameters.AddWithValue("@refund", s.RefundCaseId); cmd.Parameters.AddWithValue("@sku", s.Sku); cmd.Parameters.AddWithValue("@movement", s.MovementId); cmd.Parameters.AddWithValue("@ret", s.ReturnId);
        await using var readback = await cmd.ExecuteReaderAsync();
        if (!await readback.ReadAsync()) throw new InvalidOperationException("ISSUE26_AFTERCARE_SQL_READBACK_MISSING");
        return (readback.GetString(0), readback.GetString(1), readback.GetString(2), readback.GetInt32(3), readback.GetInt32(4));
    }

    private static async Task<string> ReadBusinessSnapshot(string cs, RunState s)
    {
        await using var c = new SqlConnection(cs); await c.OpenAsync();
        await using var cmd = new SqlCommand("SELECT (SELECT Version FROM commerce.Documents WHERE Dataset='kohami_local' AND Kind='Order' AND Id=@o),(SELECT Body FROM commerce.Documents WHERE Dataset='kohami_local' AND Kind='Order' AND Id=@o),(SELECT Slot FROM commerce.RefundUnits WHERE Dataset='kohami_local' AND UnitId=@u),(SELECT COUNT(*) FROM commerce.Documents WHERE Dataset='kohami_local' AND JSON_VALUE(Body,'$.orderId')=@o),(SELECT Stock FROM commerce.Inventory WHERE Dataset='kohami_local' AND Sku=@sku),(SELECT COUNT(*) FROM commerce.Collections WHERE Dataset='kohami_local' AND OrderId=@o AND Pending>0)", c);
        cmd.Parameters.AddWithValue("@o", s.OrderId); cmd.Parameters.AddWithValue("@u", s.UnitId); cmd.Parameters.AddWithValue("@sku", s.Sku);
        await using var r = await cmd.ExecuteReaderAsync(); if (!await r.ReadAsync()) throw new InvalidOperationException("ISSUE26_SNAPSHOT_READBACK_FAILED");
        return string.Join("|", Enumerable.Range(0, r.FieldCount).Select(i => r.IsDBNull(i) ? "<null>" : Convert.ToString(r.GetValue(i), System.Globalization.CultureInfo.InvariantCulture)));
    }

    private static async Task CleanupAndVerify(string cs, RunState s)
    {
        await using var c = new SqlConnection(cs); await c.OpenAsync(); await Issue11CartAcceptance.VerifyFixtureMarkerAsync(c);
        await using var tx = (SqlTransaction)await c.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        foreach (var actor in new[] { s.MemberA, s.MemberB, s.Support })
        {
            await using var removeActor = new SqlCommand("DELETE identity_access.SecurityAudit WHERE UserId=@u; DELETE identity_access.Sessions WHERE UserId=@u; DELETE identity_access.Grants WHERE UserId=@u; DELETE identity_access.Users WHERE UserId=@u;", c, tx);
            removeActor.Parameters.AddWithValue("@u", actor.UserId); await removeActor.ExecuteNonQueryAsync();
        }
        await using (var remove = new SqlCommand("DELETE commerce.Idempotency WHERE Dataset='kohami_local' AND OperationKey IN(" + string.Join(',', s.IdempotencyKeys.Select((_, i) => "@k" + i)) + "); DELETE commerce.Documents WHERE Dataset='kohami_local' AND (JSON_VALUE(Body,'$.orderId')=@order OR Id IN(@movement,@refund,@ret,@shipment,@receipt)); DELETE commerce.RefundUnits WHERE Dataset='kohami_local' AND OrderId=@order; DELETE commerce.Collections WHERE Dataset='kohami_local' AND Id=@collection; DELETE commerce.Inventory WHERE Dataset='kohami_local' AND Sku=@sku;", c, tx))
        {
            for (var i=0;i<s.IdempotencyKeys.Length;i++) remove.Parameters.AddWithValue("@k"+i, s.IdempotencyKeys[i]);
            remove.Parameters.AddWithValue("@movement", s.MovementId); remove.Parameters.AddWithValue("@refund", s.RefundCaseId); remove.Parameters.AddWithValue("@ret", s.ReturnId); remove.Parameters.AddWithValue("@shipment", s.ShipmentId); remove.Parameters.AddWithValue("@receipt", s.ReceiptId); remove.Parameters.AddWithValue("@unit", s.UnitId); remove.Parameters.AddWithValue("@collection", s.CollectionId); remove.Parameters.AddWithValue("@order", s.OrderId); remove.Parameters.AddWithValue("@sku", s.Sku);
            await remove.ExecuteNonQueryAsync();
        }
        await tx.CommitAsync();
        await using var check = new SqlCommand("SELECT (SELECT COUNT(*) FROM identity_access.Users WHERE UserId IN(@a,@b,@s)),(SELECT COUNT(*) FROM identity_access.Grants WHERE UserId IN(@a,@b,@s)),(SELECT COUNT(*) FROM identity_access.Sessions WHERE UserId IN(@a,@b,@s)),(SELECT COUNT(*) FROM identity_access.SecurityAudit WHERE UserId IN(@a,@b,@s)),(SELECT COUNT(*) FROM commerce.Documents WHERE Dataset='kohami_local' AND (Id=@order OR JSON_VALUE(Body,'$.orderId')=@order)),(SELECT COUNT(*) FROM commerce.Collections WHERE Dataset='kohami_local' AND (Id=@collection OR OrderId=@order)),(SELECT COUNT(*) FROM commerce.RefundUnits WHERE Dataset='kohami_local' AND (UnitId=@unit OR OrderId=@order)),(SELECT COUNT(*) FROM commerce.Idempotency WHERE Dataset='kohami_local' AND OperationKey IN(" + string.Join(',', s.IdempotencyKeys.Select((_, i) => "@k" + i)) + ")),(SELECT COUNT(*) FROM commerce.Inventory WHERE Dataset='kohami_local' AND Sku=@sku)", c, tx);
        check.Parameters.AddWithValue("@a", s.MemberA.UserId); check.Parameters.AddWithValue("@b", s.MemberB.UserId); check.Parameters.AddWithValue("@s", s.Support.UserId); check.Parameters.AddWithValue("@order", s.OrderId); check.Parameters.AddWithValue("@collection", s.CollectionId); check.Parameters.AddWithValue("@unit", s.UnitId); check.Parameters.AddWithValue("@sku", s.Sku);
        for (var i=0;i<s.IdempotencyKeys.Length;i++) check.Parameters.AddWithValue("@k"+i, s.IdempotencyKeys[i]);
        await using var readback = await check.ExecuteReaderAsync();
        if (!await readback.ReadAsync() || Enumerable.Range(0, 9).Any(i => readback.GetInt32(i) != 0))
            throw new InvalidOperationException("ISSUE26_CLEANUP_READBACK_FAILED");
        Console.WriteLine("PASS Issue26 exact synthetic rows and grants removed; isolated synthetic inventory SKU deleted by SQL readback");
    }

    private static async Task SavePrivateState(string path, RunState state)
    {
        await using var f = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        await JsonSerializer.SerializeAsync(f, state); await f.WriteAsync("\n"u8.ToArray());
    }

    private static bool ContainsOrder(JsonObject body, string id) => body["items"] is JsonArray items && items.OfType<JsonObject>().Any(x => x["id"]?.GetValue<string>() == id);
    private static bool ContainsReturn(JsonObject body, string id, string state) => body["returns"] is JsonArray returns && returns.OfType<JsonObject>().Any(x => x["id"]?.GetValue<string>() == id && x["state"]?.GetValue<string>() == state);
    private static bool ActionAllowed(JsonObject body, string action) =>
        body["actions"] is JsonArray actions && actions.OfType<JsonObject>().Any(x =>
            x["action"]?.GetValue<string>() == action && x["allowed"]?.GetValue<bool>() == true);
    private static int ReturnableQuantity(JsonObject body, string lineId) =>
        body["actions"] is JsonArray actions && actions.OfType<JsonObject>()
            .SingleOrDefault(x => x["action"]?.GetValue<string>() == "RequestReturn")?["returnableLines"] is JsonArray lines
            && lines.OfType<JsonObject>().SingleOrDefault(x => x["orderLineId"]?.GetValue<string>() == lineId)?["availableQuantity"] is JsonValue quantity
                ? quantity.GetValue<int>() : -1;
    private static string VersionFrom(JsonObject body, string property, string kind, string id, int fallback) => body[property]?.GetValue<string>() ?? CommerceWire.Version(kind, id, fallback);
    private static void RequireStatus(Response r, HttpStatusCode status, string operation) { if (r.Status != status) throw new InvalidOperationException("ISSUE26_EXPECTED_" + operation.Replace(' ', '_').ToUpperInvariant() + "_" + (int)r.Status); }
    private static void Require(bool condition, string code) { if (!condition) throw new InvalidOperationException(code); }

    private static Process StartApi(string root, string config, int port)
    {
        var dll = Path.Combine(root, "apps/api/bin/Release/net10.0/Api.dll"); Require(File.Exists(dll), "ISSUE26_API_BUILD_REQUIRED");
        var start = new ProcessStartInfo("/usr/local/share/dotnet/dotnet", dll) { WorkingDirectory = root, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        start.Environment["KOHAMI_CONFIG"] = config; start.Environment["KOHAMI_API_PORT"] = port.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var process = Process.Start(start) ?? throw new InvalidOperationException("ISSUE26_API_START_FAILED"); process.OutputDataReceived += (_, _) => { }; process.ErrorDataReceived += (_, _) => { }; process.BeginOutputReadLine(); process.BeginErrorReadLine(); return process;
    }
    private static async Task WaitForApi(HttpClient client, Uri health, Process process)
    {
        for (var i=0;i<60;i++) { if (process.HasExited) throw new InvalidOperationException("ISSUE26_API_EXITED"); try { using var response = await client.GetAsync(health); if (response.IsSuccessStatusCode) return; } catch (HttpRequestException) { } await Task.Delay(250); }
        throw new TimeoutException("ISSUE26_API_START_TIMEOUT");
    }
    private static bool PortInUse(int port) { try { using var socket = new System.Net.Sockets.TcpClient(); var pending=socket.ConnectAsync("127.0.0.1",port); return pending.Wait(200)&&socket.Connected; } catch { return false; } }
}
