using System.Text.Json;
using System.Text.Json.Nodes;
using Kohami.M0.Commerce;
using Kohami.M0.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;

public static class Issue23AnalystReportSqlAcceptance {
    static void Check(bool condition, string name) {
        if (!condition) throw new InvalidOperationException("FAILED: " + name);
        Console.WriteLine("PASS: " + name);
    }
    public static async Task RunAsync(CommerceStore store, M0Settings settings, string adminConnectionString) {
        var orderId = M0Http.Id("issue23_order");
        var primaryId = M0Http.Id("issue23_primary");
        var lateId = M0Http.Id("issue23_late");
        var day = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-2));
        var placedAt = new DateTimeOffset(day.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc));
        var body = CommerceWire.Obj(("id", orderId), ("placedAt", placedAt), ("email", "private@example.test"),
            ("snapshot", CommerceWire.Obj(("lines", new JsonArray(CommerceWire.Obj(("skuId", "private-sku"), ("quantity", 3)))))));
        body["version"] = CommerceWire.Version("Order", orderId, 1);
        await using (var seed = new SqlConnection(adminConnectionString)) {
            await seed.OpenAsync();
            await using var transaction = (SqlTransaction)await seed.BeginTransactionAsync();
            await using var insert = new SqlCommand("INSERT m0commerce.Documents(Dataset,Kind,Id,OwnerId,Version,Body) VALUES('demo_m0','Order',@order,'issue23_synthetic',1,@body); INSERT m0commerce.Collections(Dataset,Id,OrderId,Kind,Collected,Pending,Refunded) VALUES('demo_m0',@primary,@order,'Primary',2000,100,300),('demo_m0',@late,@order,'Late',500,0,0)", seed, transaction);
            insert.Parameters.AddWithValue("@order", orderId); insert.Parameters.AddWithValue("@body", body.ToJsonString());
            insert.Parameters.AddWithValue("@primary", primaryId); insert.Parameters.AddWithValue("@late", lateId);
            await insert.ExecuteNonQueryAsync(); await transaction.CommitAsync();
        }

        try {
            var allowedSecurity = new Issue23ReportFixtureSecurity(true);
            var service = new CommerceService(store, new M0Crypto(new EphemeralDataProtectionProvider()), allowedSecurity, TimeProvider.System);
            var context = new DefaultHttpContext();
            context.RequestServices = new ReportResultServices();
            context.Response.Body = new MemoryStream();
            var result = await service.AggregateReport(context, day.ToString("yyyy-MM-dd"), day.ToString("yyyy-MM-dd"));
            await result.ExecuteAsync(context);
            context.Response.Body.Position = 0;
            using var json = await JsonDocument.ParseAsync(context.Response.Body);
            Check(context.Response.StatusCode == 200, "authorized Analyst receives report");
            Check(allowedSecurity.LastPermission == "reports.read" && allowedSecurity.LastKind == "Report" && allowedSecurity.LastId == "operations", "report authorization is fixed to Report:operations");
            var bucket = json.RootElement.GetProperty("rows").EnumerateArray().Single(item => item.GetProperty("date").GetString() == day.ToString("yyyy-MM-dd") && item.GetProperty("sourceKind").GetString() == "Primary");
            Check(bucket.GetProperty("orderCount").GetInt64() >= 1, "report aggregates synthetic orders and collections");
            Check(bucket.GetProperty("collectedTwd").GetInt64() >= 2000 && bucket.GetProperty("pendingTwd").GetInt64() >= 100 && bucket.GetProperty("succeededRefundTwd").GetInt64() >= 300 && bucket.GetProperty("availableTwd").GetInt64() >= 1600 && bucket.GetProperty("netConfirmedTwd").GetInt64() >= 1700, "report computes mock totals without row details");
            var payload = json.RootElement.GetRawText();
            Check(!payload.Contains(orderId, StringComparison.Ordinal) && !payload.Contains(primaryId, StringComparison.Ordinal) && !payload.Contains("private@example.test", StringComparison.Ordinal) && !payload.Contains("private-sku", StringComparison.Ordinal), "report omits order, collection, contact and SKU details");

            foreach (var (from, to) in new[] { ("not-a-date", day.ToString("yyyy-MM-dd")), (day.AddDays(1).ToString("yyyy-MM-dd"), day.ToString("yyyy-MM-dd")), (day.AddDays(-31).ToString("yyyy-MM-dd"), day.ToString("yyyy-MM-dd")) }) {
                try { await service.AggregateReport(new DefaultHttpContext(), from, to); throw new InvalidOperationException("FAILED: invalid date window accepted"); }
                catch (M0Exception e) when (e.Status == 422) { Check(true, "invalid and overlong date window rejected"); }
            }

            var denied = new CommerceService(store, new M0Crypto(new EphemeralDataProtectionProvider()), new Issue23ReportFixtureSecurity(false), TimeProvider.System);
            try { await denied.AggregateReport(new DefaultHttpContext(), day.ToString("yyyy-MM-dd"), day.ToString("yyyy-MM-dd")); throw new InvalidOperationException("FAILED: unauthorized report read"); }
            catch (M0Exception e) when (e.Status == 403) { Check(true, "report read without reports.read is denied"); }

            var resolver = new CommerceResourceResolver(new M0Sql(settings));
            var scopes = await resolver.ResolveScopesAsync("Report", "operations");
            Check(scopes.Contains(new CommerceScope("Dataset", "demo_m0")) && scopes.Contains(new CommerceScope("Report", "operations")), "fixed operations report resolves to the synthetic dataset scope");
            Check((await resolver.ResolveScopesAsync("Report", "other")).Count == 0, "unknown report scope does not resolve");
        }
        finally {
            await using var cleanup = new SqlConnection(adminConnectionString);
            await cleanup.OpenAsync();
            await using var remove = new SqlCommand("DELETE m0commerce.Collections WHERE Dataset='demo_m0' AND OrderId=@order; DELETE m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='Order' AND Id=@order", cleanup);
            remove.Parameters.AddWithValue("@order", orderId);
            await remove.ExecuteNonQueryAsync();
            await using var verifyCleanup = new SqlCommand("SELECT (SELECT COUNT(*) FROM m0commerce.Collections WHERE Dataset='demo_m0' AND OrderId=@order)+(SELECT COUNT(*) FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='Order' AND Id=@order)", cleanup);
            verifyCleanup.Parameters.AddWithValue("@order", orderId);
            Check(Convert.ToInt32(await verifyCleanup.ExecuteScalarAsync()) == 0, "synthetic report rows are removed after acceptance");
        }
    }
}

sealed class Issue23ReportFixtureSecurity(bool allowed) : IM0Security {
    public string? LastPermission { get; private set; }
    public string? LastKind { get; private set; }
    public string? LastId { get; private set; }
    public Task<M0Principal?> ResolveAsync(HttpContext context, CancellationToken ct = default) => Task.FromResult<M0Principal?>(null);
    public Task<M0Principal> RequirePermissionAsync(HttpContext context, string permission, string resourceKind, string resourceId, CancellationToken ct = default) {
        LastPermission = permission; LastKind = resourceKind; LastId = resourceId;
        return allowed && permission == "reports.read" && resourceKind == "Report" && resourceId == "operations"
            ? Task.FromResult(new M0Principal("synthetic_analyst", "fixture-session", 1, "AdminAuthenticated", [permission]))
            : Task.FromException<M0Principal>(new M0Exception(403, "FORBIDDEN"));
    }
    public Task<bool> CanDispatchAsync(string actorUserId, long securityVersion, string permission, string resourceKind, string resourceId, CancellationToken ct = default) => Task.FromResult(false);
}

sealed class ReportResultServices : IServiceProvider {
    public object? GetService(Type serviceType) => serviceType == typeof(Microsoft.Extensions.Options.IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>)
        ? Microsoft.Extensions.Options.Options.Create(new Microsoft.AspNetCore.Http.Json.JsonOptions())
        : serviceType == typeof(Microsoft.Extensions.Logging.ILoggerFactory) ? Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance : null;
}
