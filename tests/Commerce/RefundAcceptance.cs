using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Kohami.M0.Commerce;
using Kohami.M0.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.DataProtection;

static class RefundAcceptance
{
    sealed record Unit(string Id, string LineId, long Paid, int Ordinal);
    sealed record Case(string Id, string LineId, string[] UnitIds, bool Shipping);
    sealed record Fixture(string Actor, string OrderId, string CollectionId, Case[] Cases);
    static string[] CaseIds(Fixture fixture) => fixture.Cases.Select(x => x.Id).ToArray();
    sealed record Attempt(string? OperationId, int? Status, int? ErrorStatus);

    public static async Task RunAsync(string configPath, string adminConnectionString)
    {
        var settings = M0Settings.Load(configPath);
        var store = new CommerceStore(new M0Sql(settings));
        var worker = new MockWorker(store, TimeProvider.System, NullLogger<MockWorker>.Instance);
        var suffix = Guid.NewGuid().ToString("N");

        var sameLine = await SeedAsync(store, adminConnectionString,
            560, 60, [new("unit_same_" + suffix, "line_same_" + suffix, 500, 0)],
            [new("case_same_a_" + suffix, "line_same_" + suffix, ["unit_same_" + suffix], false),
             new("case_same_b_" + suffix, "line_same_" + suffix, ["unit_same_" + suffix], false)]);
        try
        {
            var races = await RaceAsync(
                () => StartRefundAsync(store, sameLine, CaseIds(sameLine)[0], 500, false, 0),
                () => StartRefundAsync(store, sameLine, CaseIds(sameLine)[1], 500, false, 1));
            Check(races.Count(x => x.Status == 202) == 1 && races.Count(x => x.ErrorStatus == 409) == 1,
                "same original line/unit raced through separate refund cases has one reservation winner");
            var state = await StateAsync(store, sameLine);
            Check(state.Pending == 500 && state.Refunded == 0 && state.ShippingSlot == "Free" && state.Operations == 1 && state.UnknownUnits == 0,
                "same-unit race reserves one original allocation and creates one refund operation");
        }
        finally { await CleanupAsync(adminConnectionString, sameLine); }

        var differentLines = await SeedAsync(store, adminConnectionString,
            1590, 60,
            [new("unit_l1_" + suffix, "line_l1_" + suffix, 425, 0),
             new("unit_l2_" + suffix, "line_l2_" + suffix, 552, 0),
             new("unit_l3_" + suffix, "line_l3_" + suffix, 553, 0)],
            [new("case_l1_" + suffix, "line_l1_" + suffix, ["unit_l1_" + suffix], false),
             new("case_l2_" + suffix, "line_l2_" + suffix, ["unit_l2_" + suffix], false),
             new("case_ship_a_" + suffix, "line_l3_" + suffix, ["unit_l3_" + suffix], true),
             new("case_ship_b_" + suffix, "line_l3_" + suffix, ["unit_l3_" + suffix], true)]);
        try
        {
            var independent = await RaceAsync(
                () => StartRefundAsync(store, differentLines, CaseIds(differentLines)[0], 425, false, 0),
                () => StartRefundAsync(store, differentLines, CaseIds(differentLines)[1], 552, false, 1));
            Check(independent.All(x => x.Status == 202) && independent.Select(x => x.OperationId).Distinct().Count() == 2,
                "different original lines may reserve their own paid units concurrently");

            var shippingRace = await RaceAsync(
                () => StartRefundAsync(store, differentLines, CaseIds(differentLines)[2], 613, true, 2),
                () => StartRefundAsync(store, differentLines, CaseIds(differentLines)[3], 613, true, 3));
            Check(shippingRace.Count(x => x.Status == 202) == 1 && shippingRace.Count(x => x.ErrorStatus == 409) == 1,
                "original shipping allowance can be reserved only once across competing eligible units");
            var winner = shippingRace.Single(x => x.Status == 202);
            var pending = await StateAsync(store, differentLines);
            Check(pending.Pending == 1590 && pending.Refunded == 0 && pending.ShippingSlot == "Processing" && pending.Operations == 3,
                "different-line refunds plus one original shipping refund remain within the 1590 source collection");

            var operationIds = independent.Select(x => x.OperationId!).Append(winner.OperationId!).ToArray();
            foreach (var operationId in operationIds)
            {
                var job = await JobForAsync(store, operationId);
                Check(await worker.DispatchSpecificOnce(job) == 1, "refund operation enters the mock worker once");
                var operation = await store.Transaction(tx => tx.Get("RefundOperation", operationId));
                Check(operation.Body.Text("state") == "Unknown", "Unknown refund result retains its source reservations");
            }
            var unknown = await StateAsync(store, differentLines);
            Check(unknown.Pending == 1590 && unknown.Refunded == 0 && unknown.ShippingSlot == "Unknown" && unknown.UnknownUnits == 3,
                "Unknown retains every pending amount, original unit, and shipping slot");

            var unknownOperation = await store.Transaction(tx => tx.Get("RefundOperation", winner.OperationId!));
            var checkVersion = CommerceWire.Version("RefundOperation", unknownOperation.Id, unknownOperation.Version);
            var query = await new CommerceService(store, new M0Crypto(new EphemeralDataProtectionProvider()),
                new RefundFixtureSecurity(differentLines.Actor), TimeProvider.System)
                .CheckRefund(Request(CommerceWire.Obj(("expectedVersion", checkVersion), ("reason", "query original synthetic refund")),
                    checkVersion, M0Http.Id("ac07_check")), unknownOperation.Id);
            var queryBody = Body(query);
            Check(queryBody.Text("resourceUrl") == "/api/v1/admin/refund-operations/" + winner.OperationId,
                "refund check addresses the original Unknown operation and does not create a second refund");

            await worker.RecordTrustedResult("RefundOperation", winner.OperationId!, "success");
            await worker.RecordTrustedResult("RefundOperation", winner.OperationId!, "success");
            var reconciled = await StateAsync(store, differentLines);
            Check(reconciled.Pending == 977 && reconciled.Refunded == 613 && reconciled.ShippingSlot == "Succeeded" && reconciled.SucceededUnits == 1,
                "trusted query result settles the original refund once while other Unknown operations remain reserved");
        }
        finally { await CleanupAsync(adminConnectionString, differentLines); }

        var revokedBeforeStart = await SeedAsync(store, adminConnectionString,
            560, 60, [new("unit_pre_" + suffix, "line_pre_" + suffix, 500, 0)],
            [new("case_pre_" + suffix, "line_pre_" + suffix, ["unit_pre_" + suffix], true)]);
        try
        {
            var started = await StartRefundAsync(store, revokedBeforeStart, CaseIds(revokedBeforeStart)[0], 560, true, 4);
            Check(started.Status == 202, "refund request reserves the eligible original unit and shipping before dispatch");
            await RevokeAsync(adminConnectionString, revokedBeforeStart.Actor);
            var job = await JobForAsync(store, started.OperationId!);
            Check(await worker.DispatchSpecificOnce(job) == 0, "revoked Owner is blocked before mock refund dispatch starts");
            var failed = await store.Transaction(tx => tx.Get("RefundOperation", started.OperationId!));
            var released = await StateAsync(store, revokedBeforeStart);
            Check(failed.Body.Text("state") == "Failed" && failed.Body.Text("dispatchState") == "NotStarted" &&
                  released.Pending == 0 && released.Refunded == 0 && released.ShippingSlot == "Free" && released.FreeUnits == 1,
                "definite pre-Started denial releases the original amount, unit, and shipping reservations");
        }
        finally { await CleanupAsync(adminConnectionString, revokedBeforeStart); }

        var revokedAfterStart = await SeedAsync(store, adminConnectionString,
            560, 60, [new("unit_post_" + suffix, "line_post_" + suffix, 500, 0)],
            [new("case_post_" + suffix, "line_post_" + suffix, ["unit_post_" + suffix], true)]);
        try
        {
            var started = await StartRefundAsync(store, revokedAfterStart, CaseIds(revokedAfterStart)[0], 560, true, 5);
            Check(started.Status == 202, "refund operation is durably queued before worker start");
            var job = await JobForAsync(store, started.OperationId!);
            Check(await worker.DispatchSpecificOnce(job) == 1, "worker records Started and persists the in-flight Unknown outcome");
            await RevokeAsync(adminConnectionString, revokedAfterStart.Actor);
            await worker.RecordTrustedResult("RefundOperation", started.OperationId!, "success");
            await worker.RecordTrustedResult("RefundOperation", started.OperationId!, "success");
            var operation = await store.Transaction(tx => tx.Get("RefundOperation", started.OperationId!));
            var final = await StateAsync(store, revokedAfterStart);
            Check(operation.Body.Text("state") == "Succeeded" && operation.Body.Text("dispatchState") == "Started" &&
                  final.Pending == 0 && final.Refunded == 560 && final.ShippingSlot == "Succeeded" && final.SucceededUnits == 1,
                "system tracks and settles a Started refund after actor revocation without a duplicate effect");
        }
        finally { await CleanupAsync(adminConnectionString, revokedAfterStart); }

        var inspected = await SeedAsync(store, adminConnectionString,
            560, 60, [new("unit_inspected_" + suffix, "line_inspected_" + suffix, 500, 0)],
            [new("case_inspected_" + suffix, "line_inspected_" + suffix, ["unit_inspected_" + suffix], false)]);
        var inspectedReturnId = "return_inspected_" + suffix;
        var inspectedMovementId = "movement_inspected_" + suffix;
        try
        {
            await store.Transaction(async tx =>
            {
                var refundCase = await tx.Get("RefundCase", inspected.Cases[0].Id);
                refundCase.Body["returnId"] = inspectedReturnId;
                await tx.Save(refundCase with { Version = refundCase.Version + 1 });
                var now = DateTimeOffset.UtcNow;
                var returnBody = CommerceWire.Obj(("id", inspectedReturnId), ("orderId", inspected.OrderId),
                    ("orderLineId", inspected.Cases[0].LineId), ("quantity", 1), ("mode", "mock"),
                    ("state", "Inspected"), ("inspection", CommerceWire.Obj(("refundCaseId", refundCase.Id), ("inventoryMovementId", inspectedMovementId))),
                    ("history", new JsonArray(CommerceWire.Obj(("state", "Requested"), ("at", now.AddMinutes(-1)), ("actor", inspected.Actor)),
                        CommerceWire.Obj(("state", "Inspected"), ("at", now), ("actor", inspected.Actor)))));
                await tx.Save(new("Return", inspectedReturnId, inspected.Actor, 1, returnBody), true);
                await tx.Save(new("InventoryMovement", inspectedMovementId, inspected.Actor, 1,
                    CommerceWire.Obj(("id", inspectedMovementId), ("returnId", inspectedReturnId),
                        ("orderId", inspected.OrderId), ("state", "Applied"), ("quantity", 1))), true);
                return true;
            });
            var started = await StartRefundAsync(store, inspected, inspected.Cases[0].Id, 500, false, 6, 2);
            Check(started.Status == 202, "inspected return refund is queued before its closure can be recorded");
            var job = await JobForAsync(store, started.OperationId!);
            Check(await worker.DispatchSpecificOnce(job) == 1, "inspected return refund enters worker processing");
            var beforeSuccess = await store.Transaction(tx => tx.Get("Return", inspectedReturnId));
            Check(beforeSuccess.Body["closure"] is null, "pending refund does not close an inspected return");
            await worker.RecordTrustedResult("RefundOperation", started.OperationId!, "success");
            var closed = await store.Transaction(tx => tx.Get("Return", inspectedReturnId));
            var closedAt = closed.Body["closure"]?["closedAt"]?.GetValue<string>();
            Check(closed.Body["closure"]?["status"]?.GetValue<string>() == "Closed" &&
                  closed.Body["closure"]?["basis"]?.GetValue<string>() == "RefundSucceeded" &&
                  closed.Body["closure"]?["refundCaseId"]?.GetValue<string>() == inspected.Cases[0].Id &&
                  closed.Body["closure"]?["refundOperationId"]?.GetValue<string>() == started.OperationId &&
                  DateTimeOffset.TryParse(closedAt, out var closureAt) && closureAt.Offset == TimeSpan.Zero,
                "successful linked refund records a minimal UTC closure with case and operation evidence");
            await worker.RecordTrustedResult("RefundOperation", started.OperationId!, "success");
            var duplicate = await store.Transaction(tx => tx.Get("Return", inspectedReturnId));
            Check(duplicate.Version == closed.Version && duplicate.Body["closure"]?["closedAt"]?.GetValue<string>() == closedAt,
                "duplicate successful refund preserves the original aftercare closure event");
        }
        finally { await CleanupAsync(adminConnectionString, inspected); }
    }

    static async Task<Fixture> SeedAsync(CommerceStore store, string adminConnectionString,
        long collected, long shipping, Unit[] units, Case[] cases)
    {
        var actor = M0Http.Id("ac07_owner");
        var orderId = M0Http.Id("ac07_order");
        var collectionId = M0Http.Id("ac07_collection");
        var fixture = new Fixture(actor, orderId, collectionId, cases);
        await using (var connection = new SqlConnection(adminConnectionString))
        {
            await connection.OpenAsync();
            await using var user = new SqlCommand(@"
INSERT m0.Users(UserId,Email,PasswordHash,EmailConfirmed,Status,SecurityVersion)
VALUES(@actor,@email,REPLICATE(N'x',100),1,'Active',1);
INSERT m0.Grants(GrantId,UserId,Role,Permission,ScopeKind,ScopeId,ValidFrom,ValidUntil,GrantedBy,Reason)
VALUES(@grant,@actor,'Owner','refund.execute','Dataset','demo_m0',DATEADD(minute,-1,SYSDATETIMEOFFSET()),DATEADD(hour,1,SYSDATETIMEOFFSET()),'fixture','dedicated AC07 synthetic refund');
INSERT m0commerce.Collections(Dataset,Id,OrderId,Kind,Collected,ShippingPaid)
VALUES('demo_m0',@collection,@order,'Primary',@collected,@shipping);", connection);
            user.Parameters.AddWithValue("@actor", actor);
            user.Parameters.AddWithValue("@email", actor + "@example.test");
            user.Parameters.AddWithValue("@grant", M0Http.Id("ac07_grant"));
            user.Parameters.AddWithValue("@collection", collectionId);
            user.Parameters.AddWithValue("@order", orderId);
            user.Parameters.AddWithValue("@collected", collected);
            user.Parameters.AddWithValue("@shipping", shipping);
            await user.ExecuteNonQueryAsync();
            foreach (var unit in units)
            {
                await using var insert = new SqlCommand(@"
INSERT m0commerce.RefundUnits(Dataset,UnitId,OrderId,LineId,CollectionId,Ordinal,Paid)
VALUES('demo_m0',@unit,@order,@line,@collection,@ordinal,@paid);", connection);
                insert.Parameters.AddWithValue("@unit", unit.Id);
                insert.Parameters.AddWithValue("@order", orderId);
                insert.Parameters.AddWithValue("@line", unit.LineId);
                insert.Parameters.AddWithValue("@collection", collectionId);
                insert.Parameters.AddWithValue("@ordinal", unit.Ordinal);
                insert.Parameters.AddWithValue("@paid", unit.Paid);
                await insert.ExecuteNonQueryAsync();
            }
        }
        await store.Transaction(async tx =>
        {
            await tx.Save(new("Order", orderId, actor, 1,
                CommerceWire.Obj(("id", orderId), ("orderId", orderId), ("orderState", "Confirmed"), ("paymentState", "Succeeded"))), true);
            foreach (var item in cases)
            {
                var body = CommerceWire.Obj(("id", item.Id), ("orderId", orderId), ("basis", "Merchandise"),
                    ("kind", "OrderRefund"), ("state", "Requested"), ("sourceCollectionId", collectionId),
                    ("shippingEligible", item.Shipping), ("eligibleUnitIds", item.UnitIds));
                await tx.Save(new("RefundCase", item.Id, actor, 1, body), true);
            }
            return true;
        });
        return fixture;
    }

    static async Task<Attempt> StartRefundAsync(CommerceStore store, Fixture fixture, string caseId, long amount,
        bool shipping, int ordinal, long caseVersion = 1)
    {
        var commerce = new CommerceService(store, new M0Crypto(new EphemeralDataProtectionProvider()),
            new RefundFixtureSecurity(fixture.Actor), TimeProvider.System);
        var version = CommerceWire.Version("RefundCase", caseId, caseVersion);
        var caseLine = fixture.Cases.Single(x => x.Id == caseId).LineId;
        var body = CommerceWire.Obj(("lines", new[] { CommerceWire.Obj(("orderLineId", caseLine), ("quantity", 1)) }),
            ("refundShipping", shipping), ("amountTwd", amount), ("reason", "synthetic AC07 refund"), ("expectedVersion", version));
        try
        {
            var result = await commerce.Refund(Request(body, version, M0Http.Id("ac07_key_" + ordinal)), caseId, "Merchandise");
            var response = Body(result);
            return new(response.Text("id"), Status(result), null);
        }
        catch (M0Exception exception) { return new(null, null, exception.Status); }
    }

    static async Task<Attempt[]> RaceAsync(params Func<Task<Attempt>>[] attempts)
    {
        using var ready = new CountdownEvent(attempts.Length);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = attempts.Select(async attempt =>
        {
            ready.Signal();
            await release.Task;
            return await attempt();
        }).ToArray();
        if (!ready.Wait(TimeSpan.FromSeconds(5))) throw new InvalidOperationException("AC07_RACE_BARRIER_TIMEOUT");
        release.SetResult();
        return await Task.WhenAll(tasks);
    }

    static HttpContext Request(JsonObject body, string version, string key)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.ContentType = "application/json";
        context.Request.Headers.IfMatch = '"' + version + '"';
        context.Request.Headers["Idempotency-Key"] = key;
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body.ToJsonString()));
        context.Response.Body = new MemoryStream();
        return context;
    }

    static JsonObject Body(IResult result) => result is IValueHttpResult value
        ? JsonSerializer.SerializeToNode(value.Value)!.AsObject()
        : throw new InvalidOperationException("AC07_EXPECTED_JSON_RESULT");
    static int? Status(IResult result) => (result as IStatusCodeHttpResult)?.StatusCode;

    static async Task<string> JobForAsync(CommerceStore store, string operationId) =>
        Convert.ToString(await store.Transaction(tx => tx.Scalar("SELECT Id FROM m0commerce.Jobs WHERE Dataset='demo_m0' AND Kind='Refund' AND TargetId=@id", ("@id", operationId))))
        ?? throw new InvalidOperationException("AC07_REFUND_JOB_MISSING");

    static async Task<(long Pending, long Refunded, string ShippingSlot, int Operations, int UnknownUnits, int SucceededUnits, int FreeUnits)> StateAsync(CommerceStore store, Fixture fixture) =>
        await store.Transaction(async tx =>
        {
            var collection = await tx.Rows("SELECT Pending,Refunded,ShippingSlot FROM m0commerce.Collections WHERE Dataset='demo_m0' AND Id=@id", ("@id", fixture.CollectionId));
            var operations = Convert.ToInt32(await tx.Scalar("SELECT COUNT(*) FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='RefundOperation' AND JSON_VALUE(Body,'$.orderId')=@order", ("@order", fixture.OrderId)));
            var units = await tx.Rows("SELECT Slot FROM m0commerce.RefundUnits WHERE Dataset='demo_m0' AND OrderId=@order", ("@order", fixture.OrderId));
            return ((long)collection[0][0], (long)collection[0][1], (string)collection[0][2], operations,
                units.Count(x => (string)x[0] == "Unknown"), units.Count(x => (string)x[0] == "Succeeded"), units.Count(x => (string)x[0] == "Free"));
        });

    static async Task RevokeAsync(string adminConnectionString, string actor)
    {
        await using var connection = new SqlConnection(adminConnectionString);
        await connection.OpenAsync();
        await using var revoke = new SqlCommand("UPDATE m0.Grants SET RevokedAt=SYSDATETIMEOFFSET() WHERE UserId=@actor AND Permission='refund.execute' AND RevokedAt IS NULL", connection);
        revoke.Parameters.AddWithValue("@actor", actor);
        if (await revoke.ExecuteNonQueryAsync() != 1) throw new InvalidOperationException("AC07_GRANT_REVOCATION_MISSING");
    }

    static async Task CleanupAsync(string adminConnectionString, Fixture fixture)
    {
        await using var connection = new SqlConnection(adminConnectionString);
        await connection.OpenAsync();
        await using var cleanup = new SqlCommand(@"
DECLARE @sources TABLE(Id varchar(100) PRIMARY KEY);
INSERT @sources SELECT Id FROM m0commerce.Jobs WHERE Dataset='demo_m0' AND Actor=@actor
UNION SELECT Id+'_terminal' FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='RefundOperation' AND JSON_VALUE(Body,'$.orderId')=@order
UNION SELECT Id+'_unknown' FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='RefundOperation' AND JSON_VALUE(Body,'$.orderId')=@order;
DELETE m0commerce.Inbox WHERE Dataset='demo_m0' AND SourceId IN (SELECT Id FROM @sources);
DELETE m0commerce.Jobs WHERE Dataset='demo_m0' AND Actor=@actor;
DELETE m0commerce.Idempotency WHERE Dataset='demo_m0' AND Actor=@actor;
DELETE m0commerce.RefundUnits WHERE Dataset='demo_m0' AND OrderId=@order;
DELETE m0commerce.Collections WHERE Dataset='demo_m0' AND Id=@collection;
DELETE m0commerce.Documents WHERE Dataset='demo_m0' AND JSON_VALUE(Body,'$.orderId')=@order;
DELETE m0.Grants WHERE UserId=@actor;
DELETE m0.Users WHERE UserId=@actor;
SELECT (SELECT COUNT(*) FROM m0commerce.Collections WHERE Dataset='demo_m0' AND Id=@collection)+
       (SELECT COUNT(*) FROM m0commerce.RefundUnits WHERE Dataset='demo_m0' AND OrderId=@order)+
       (SELECT COUNT(*) FROM m0commerce.Documents WHERE Dataset='demo_m0' AND JSON_VALUE(Body,'$.orderId')=@order)+
       (SELECT COUNT(*) FROM m0commerce.Jobs WHERE Dataset='demo_m0' AND Actor=@actor)+
       (SELECT COUNT(*) FROM m0commerce.Idempotency WHERE Dataset='demo_m0' AND Actor=@actor)+
       (SELECT COUNT(*) FROM m0commerce.Inbox WHERE Dataset='demo_m0' AND SourceId IN (SELECT Id FROM @sources))+
       (SELECT COUNT(*) FROM m0.Grants WHERE UserId=@actor)+
       (SELECT COUNT(*) FROM m0.Users WHERE UserId=@actor);", connection);
        cleanup.Parameters.AddWithValue("@order", fixture.OrderId);
        cleanup.Parameters.AddWithValue("@collection", fixture.CollectionId);
        cleanup.Parameters.AddWithValue("@actor", fixture.Actor);
        var leftovers = Convert.ToInt32(await cleanup.ExecuteScalarAsync());
        Check(leftovers == 0, "AC07 synthetic refund fixture is completely removed");
    }

    static void Check(bool pass, string message)
    {
        if (!pass) throw new InvalidOperationException("FAILED: " + message);
        Console.WriteLine("PASS: " + message);
    }

    sealed class RefundFixtureSecurity(string actor) : IM0Security
    {
        public Task<M0Principal?> ResolveAsync(HttpContext context, CancellationToken ct = default) => Task.FromResult<M0Principal?>(null);
        public Task<M0Principal> RequirePermissionAsync(HttpContext context, string permission, string resourceKind, string resourceId,
            CancellationToken ct = default) =>
            permission is "refund.execute" or "refund.check"
                ? Task.FromResult(new M0Principal(actor, "synthetic-ac07-session", 1, "AdminAuthenticated", [permission]))
                : Task.FromException<M0Principal>(new M0Exception(403, "FORBIDDEN"));
        public Task<bool> CanDispatchAsync(string actorUserId, long securityVersion, string permission, string resourceKind,
            string resourceId, CancellationToken ct = default) => Task.FromResult(actorUserId == actor && permission == "refund.execute");
    }
}
