using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Kohami.M0.Commerce;
using Kohami.M0.Core;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;

static class Issue15ReconciliationResolutionSqlAcceptance
{
    public static async Task RunAsync(CommerceStore store, string adminConnectionString)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var actor = "issue15_resolver_" + suffix;
        var assignee = "issue15_assignee_" + suffix;
        var orderId = "order_issue15_resolve_" + suffix;
        var collectionId = "collection_issue15_resolve_" + suffix;
        var protection = DataProtectionProvider.Create("kohami.issue15.reconciliation.tests");
        var service = new CommerceService(store, new M0Crypto(protection), new FixtureSecurity(actor), TimeProvider.System, new FixtureAssigneeResolver(assignee));
        try
        {
            await using (var connection = new SqlConnection(adminConnectionString))
            {
                await connection.OpenAsync();
                await using var seed = new SqlCommand(@"
INSERT m0.Users(UserId,Email,PasswordHash,EmailConfirmed,SecurityHold,Status,SecurityVersion)
VALUES(@actor,@email,N'synthetic-only',1,0,'Active',1),(@assignee,@assigneeEmail,N'synthetic-only',1,0,'Active',1);
INSERT m0.Grants(GrantId,UserId,Role,Permission,ScopeKind,ScopeId,ValidFrom,ValidUntil,GrantedBy,Reason)
VALUES(@grant,@actor,'Owner','reconciliation.resolve','Dataset','demo_m0',DATEADD(day,-1,SYSDATETIMEOFFSET()),DATEADD(day,1,SYSDATETIMEOFFSET()),@actor,N'Issue15 SQL acceptance'),
       (@assigneeGrant,@assignee,'Owner','reconciliation.resolve','Case',@case,DATEADD(day,-1,SYSDATETIMEOFFSET()),DATEADD(day,1,SYSDATETIMEOFFSET()),@actor,N'Issue15 notification acceptance');
INSERT m0commerce.Documents(Dataset,Kind,Id,OwnerId,Version,Body)
VALUES('demo_m0','Order',@order,@actor,1,N'{""id"":""synthetic"",""orderState"":""Confirmed"",""paymentState"":""Succeeded"",""compensationRequired"":false}');
INSERT m0commerce.Collections(Dataset,Id,OrderId,Kind,Collected,Pending,Refunded,ShippingPaid,ShippingSlot)
VALUES('demo_m0',@collection,@order,'Late',100,0,0,0,'Free');", connection);
                seed.Parameters.AddWithValue("@order", orderId);
                seed.Parameters.AddWithValue("@actor", actor);
                seed.Parameters.AddWithValue("@email", actor + "@example.test");
                seed.Parameters.AddWithValue("@assignee", assignee);
                seed.Parameters.AddWithValue("@assigneeEmail", assignee + "@example.test");
                seed.Parameters.AddWithValue("@grant", "grant_" + suffix);
                seed.Parameters.AddWithValue("@assigneeGrant", "grant_assignee_" + suffix);
                seed.Parameters.AddWithValue("@case", "recon_" + M0Http.Hash(string.Join("\0", new[] { "NON_PRIMARY_COLLECTION_REQUIRES_REVIEW", collectionId, "", "" }))[..32]);
                seed.Parameters.AddWithValue("@collection", collectionId);
                await seed.ExecuteNonQueryAsync();
            }

            var report = await ReadJsonAsync(await service.ReconciliationReport(NewRequest("GET", "/admin/reconciliation-report")));
            var finding = report["findings"]!.AsArray().OfType<JsonObject>().Single(x => OptionalText(x, "sourceId") == collectionId && OptionalText(x, "code") == "NON_PRIMARY_COLLECTION_REQUIRES_REVIEW");
            var caseId = finding.Text("caseId");
            var version = finding.Text("version");
            Check(!string.IsNullOrWhiteSpace(caseId) && finding.Text("disposition") == "Open", "Issue15 report exposes stable open reconciliation case and version");

            var assignmentBody = CommerceWire.Obj(("assigneeEmail", assignee + "@example.test"), ("expectedVersion", version), ("reason", "Assign major reconciliation exception"));
            var assignmentKey = "issue15-assignment-" + suffix;
            var assignment = await AssignAsync(service, caseId, assignmentBody, version, assignmentKey);
            var assignmentReplay = await AssignAsync(service, caseId, assignmentBody, version, assignmentKey);
            Check(assignment.ToJsonString() == assignmentReplay.ToJsonString(), "Issue15 identical assignment retry returns the original result");
            var assignedVersion = assignment.Text("version");
            var recipientService = new CommerceService(store, new M0Crypto(protection), new FixtureSecurity(assignee), TimeProvider.System);
            var recipientInbox = await ReadJsonAsync(await recipientService.ReconciliationNotifications(NewRequest("GET", "/admin/reconciliation-notifications")));
            Check(recipientInbox["items"]!.AsArray().Count == 1 && recipientInbox["items"]![0]!["caseId"]!.GetValue<string>() == caseId && recipientInbox["items"]![0]!["channel"]!.GetValue<string>() == "InApp", "Issue15 major exception assignment creates an assigned-user in-app notification");
            var actorInbox = await ReadJsonAsync(await service.ReconciliationNotifications(NewRequest("GET", "/admin/reconciliation-notifications")));
            Check(actorInbox["items"]!.AsArray().Count == 0, "Issue15 reconciliation notification is hidden from other actors");
            await using (var connection = new SqlConnection(adminConnectionString))
            {
                await connection.OpenAsync();
                await using var revoke = new SqlCommand("UPDATE m0.Grants SET RevokedAt=SYSDATETIMEOFFSET() WHERE UserId=@assignee AND ScopeKind='Case' AND ScopeId=@case", connection);
                revoke.Parameters.AddWithValue("@assignee", assignee);
                revoke.Parameters.AddWithValue("@case", caseId);
                await revoke.ExecuteNonQueryAsync();
            }
            var revokedInbox = await ReadJsonAsync(await recipientService.ReconciliationNotifications(NewRequest("GET", "/admin/reconciliation-notifications")));
            Check(revokedInbox["items"]!.AsArray().Count == 0, "Issue15 notification becomes inaccessible after case permission revocation");

            var queueBody = CommerceWire.Obj(("action", "QueueOriginalCheck"), ("expectedVersion", assignedVersion), ("reason", "Review against source ledger"), ("evidenceRef", "evidence_issue15_" + suffix));
            var queueKey = "issue15-resolution-queue-" + suffix;
            var queued = await ResolveAsync(service, caseId, queueBody, assignedVersion, queueKey);
            Check(queued.Text("disposition") == "CheckRequested" && queued.Text("responsibleActor") == actor, "Issue15 original-check request records responsible actor");
            var replay = await ResolveAsync(service, caseId, queueBody, assignedVersion, queueKey);
            Check(replay.ToJsonString() == queued.ToJsonString(), "Issue15 identical resolution retry returns original SQL result");

            var queuedVersion = queued.Text("version");
            var prematureClose = CommerceWire.Obj(("action", "CloseReconciled"), ("expectedVersion", queuedVersion), ("reason", "Attempt closure before source correction"), ("evidenceRef", "evidence_issue15_" + suffix));
            try
            {
                await ResolveAsync(service, caseId, prematureClose, queuedVersion, "issue15-resolution-premature-" + suffix);
                throw new InvalidOperationException("FAILED: active reconciliation finding was closed");
            }
            catch (M0Exception exception) when (exception.Status == 409)
            {
                Check(true, "Issue15 cannot close a case while its financial finding remains current");
            }

            await using (var connection = new SqlConnection(adminConnectionString))
            {
                await connection.OpenAsync();
                await using var correctSource = new SqlCommand("UPDATE m0commerce.Collections SET Kind='Primary' WHERE Dataset='demo_m0' AND Id=@id", connection);
                correctSource.Parameters.AddWithValue("@id", collectionId);
                if (await correctSource.ExecuteNonQueryAsync() != 1) throw new InvalidOperationException("ISSUE15_FIXTURE_SOURCE_MISSING");
            }

            var closedBody = CommerceWire.Obj(("action", "CloseReconciled"), ("expectedVersion", queuedVersion), ("reason", "Source classification has been corrected"), ("evidenceRef", "evidence_issue15_" + suffix));
            var closed = await ResolveAsync(service, caseId, closedBody, queuedVersion, "issue15-resolution-close-" + suffix);
            Check(closed.Text("disposition") == "Closed", "Issue15 permits closure after the current finding clears");

            var after = await ReadJsonAsync(await service.ReconciliationReport(NewRequest("GET", "/admin/reconciliation-report")));
            Check(!after["findings"]!.AsArray().OfType<JsonObject>().Any(x => x.Text("caseId") == caseId), "Issue15 closed case no longer appears among active findings");
            await using (var connection = new SqlConnection(adminConnectionString))
            {
                await connection.OpenAsync();
                await using var recurrence = new SqlCommand("UPDATE m0commerce.Collections SET Kind='Late' WHERE Dataset='demo_m0' AND Id=@id", connection);
                recurrence.Parameters.AddWithValue("@id", collectionId);
                if (await recurrence.ExecuteNonQueryAsync() != 1) throw new InvalidOperationException("ISSUE15_FIXTURE_RECURRENCE_SOURCE_MISSING");
            }
            var reopenedReport = await ReadJsonAsync(await service.ReconciliationReport(NewRequest("GET", "/admin/reconciliation-report")));
            var reopenedFinding = reopenedReport["findings"]!.AsArray().OfType<JsonObject>().Single(x => OptionalText(x, "caseId") == caseId);
            Check(reopenedFinding.Text("disposition") == "Reopened", "Issue15 recurring finding is not hidden by an earlier closure");
            var reopenedBody = CommerceWire.Obj(("action", "QueueOriginalCheck"), ("expectedVersion", reopenedFinding.Text("version")), ("reason", "Review recurring source difference"), ("evidenceRef", "evidence_issue15_" + suffix));
            var reopened = await ResolveAsync(service, caseId, reopenedBody, reopenedFinding.Text("version"), "issue15-resolution-reopen-" + suffix);
            Check(reopened.Text("disposition") == "CheckRequested", "Issue15 reopened case accepts a new versioned resolution");
            await using (var connection = new SqlConnection(adminConnectionString))
            {
                await connection.OpenAsync();
                await using var verify = new SqlCommand(@"
SELECT d.Version,JSON_VALUE(d.Body,'$.state'),JSON_VALUE(d.Body,'$.responsibleActor'),
       (SELECT COUNT(*) FROM OPENJSON(d.Body,'$.history')),
       (SELECT COUNT(*) FROM m0commerce.Idempotency WHERE Dataset='demo_m0' AND Actor=@actor AND OperationKey=@key),
       c.Kind,c.Collected,c.Pending,c.Refunded
FROM m0commerce.Documents d JOIN m0commerce.Collections c ON c.Dataset=d.Dataset AND c.Id=@collection
WHERE d.Dataset='demo_m0' AND d.Kind='ReconciliationCase' AND d.Id=@case", connection);
                verify.Parameters.AddWithValue("@actor", actor);
                verify.Parameters.AddWithValue("@key", queueKey);
                verify.Parameters.AddWithValue("@collection", collectionId);
                verify.Parameters.AddWithValue("@case", caseId);
                await using var reader = await verify.ExecuteReaderAsync();
                if (!await reader.ReadAsync()) throw new InvalidOperationException("ISSUE15_CASE_PERSISTENCE_MISSING");
                Check(reader.GetInt64(0) == 4 && reader.GetString(1) == "CheckRequested" && reader.GetString(2) == actor && reader.GetInt32(3) == 4, "Issue15 SQL preserves assignment and disposition history through recurrence");
                Check(reader.GetInt32(4) == 1 && reader.GetString(5) == "Late" && reader.GetInt64(6) == 100 && reader.GetInt64(7) == 0 && reader.GetInt64(8) == 0, "Issue15 replay is unique and recurrence resolution does not alter financial balances");
            }
        }
        finally
        {
            await using var connection = new SqlConnection(adminConnectionString);
            await connection.OpenAsync();
            await using var cleanup = new SqlCommand(@"
DELETE m0commerce.Idempotency WHERE Dataset='demo_m0' AND Actor=@actor;
DELETE m0commerce.Documents WHERE Dataset='demo_m0' AND Kind IN('ReconciliationCase','ReconciliationNotification','Order') AND (Id=@case OR Id=@order OR OwnerId=@assignee);
DELETE m0commerce.Collections WHERE Dataset='demo_m0' AND Id=@collection;
DELETE m0.Grants WHERE UserId IN(@actor,@assignee);
DELETE m0.Users WHERE UserId IN(@actor,@assignee);", connection);
            cleanup.Parameters.AddWithValue("@actor", actor);
            cleanup.Parameters.AddWithValue("@assignee", assignee);
            cleanup.Parameters.AddWithValue("@case", "recon_" + M0Http.Hash(string.Join("\0", new[] { "NON_PRIMARY_COLLECTION_REQUIRES_REVIEW", collectionId, "", "" }))[..32]);
            cleanup.Parameters.AddWithValue("@order", orderId);
            cleanup.Parameters.AddWithValue("@collection", collectionId);
            await cleanup.ExecuteNonQueryAsync();
        }
    }

    static DefaultHttpContext NewRequest(string method, string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        context.RequestServices = new ReconciliationResultServices();
        return context;
    }

    static async Task<JsonObject> ResolveAsync(CommerceService service, string id, JsonObject body, string version, string key)
    {
        var context = NewRequest("POST", "/admin/reconciliation-cases/" + id + "/resolutions");
        context.Request.Headers.IfMatch = '"' + version + '"';
        context.Request.Headers["Idempotency-Key"] = key;
        context.Request.ContentType = "application/json";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body.ToJsonString()));
        var result = await service.ResolveReconciliationCase(context, id);
        await result.ExecuteAsync(context);
        context.Response.Body.Position = 0;
        return (await JsonNode.ParseAsync(context.Response.Body))!.AsObject();
    }

    static async Task<JsonObject> AssignAsync(CommerceService service, string id, JsonObject body, string version, string key)
    {
        var context = NewRequest("POST", "/admin/reconciliation-cases/" + id + "/assignments");
        context.Request.Headers.IfMatch = '"' + version + '"';
        context.Request.Headers["Idempotency-Key"] = key;
        context.Request.ContentType = "application/json";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body.ToJsonString()));
        var result = await service.AssignReconciliationCase(context, id);
        await result.ExecuteAsync(context);
        context.Response.Body.Position = 0;
        return (await JsonNode.ParseAsync(context.Response.Body))!.AsObject();
    }

    static async Task<JsonObject> ReadJsonAsync(IResult result)
    {
        var context = NewRequest("GET", "/admin/reconciliation-report");
        await result.ExecuteAsync(context);
        context.Response.Body.Position = 0;
        return (await JsonNode.ParseAsync(context.Response.Body))!.AsObject();
    }

    static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException("FAILED: " + label);
        Console.WriteLine("PASS " + label);
    }

    static string OptionalText(JsonObject value, string key) => value[key] is JsonValue node && node.TryGetValue<string>(out var text) ? text : "";

    sealed class FixtureSecurity(string actor) : IM0Security
    {
        public Task<M0Principal?> ResolveAsync(HttpContext context, CancellationToken ct = default) => Task.FromResult<M0Principal?>(new M0Principal(actor,"issue15-fixture-session",1,"AdminAuthenticated",["reconciliation.resolve"]));
        public Task<M0Principal> RequirePermissionAsync(HttpContext context, string permission, string resourceKind, string resourceId, CancellationToken ct = default)
            => permission is "reconciliation.read" or "reconciliation.resolve" && resourceKind is "Dataset" or "Case"
                ? Task.FromResult(new M0Principal(actor, "issue15-fixture-session", 1, "AdminAuthenticated", [permission]))
                : Task.FromException<M0Principal>(new M0Exception(403, "FORBIDDEN"));
        public Task<bool> CanDispatchAsync(string actorUserId, long securityVersion, string permission, string resourceKind, string resourceId, CancellationToken ct = default)
            => Task.FromResult(actorUserId == actor && securityVersion == 1 && permission == "reconciliation.resolve" && resourceKind == "Case");
    }

    sealed class FixtureAssigneeResolver(string assignee) : IReconciliationAssigneeResolver
    {
        public Task<ReconciliationAssignee?> ResolveAsync(string email, string resourceId, CancellationToken ct = default)
            => Task.FromResult<ReconciliationAssignee?>(new ReconciliationAssignee(assignee, 1));
    }

    sealed class ReconciliationResultServices : IServiceProvider
    {
        public object? GetService(Type serviceType) => serviceType == typeof(Microsoft.Extensions.Options.IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>)
            ? Microsoft.Extensions.Options.Options.Create(new Microsoft.AspNetCore.Http.Json.JsonOptions())
            : serviceType == typeof(Microsoft.Extensions.Logging.ILoggerFactory) ? Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance : null;
    }
}
