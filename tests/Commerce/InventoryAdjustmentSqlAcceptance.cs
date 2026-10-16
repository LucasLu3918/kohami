using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Kohami.Commerce.Domain;
using Kohami.M0.Commerce;
using Kohami.M0.Core;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;

internal static class InventoryAdjustmentSqlAcceptance
{
    public static async Task RunAsync(string config)
    {
        using var raw = JsonDocument.Parse(await File.ReadAllTextAsync(config));
        if (!raw.RootElement.TryGetProperty("IdentitySqlTestDedicated", out var dedicated) || !dedicated.GetBoolean())
            throw new InvalidOperationException("DEDICATED_SQL_FIXTURE_REQUIRED");
        var settings = M0Settings.Load(config);
        var admin = new SqlConnectionStringBuilder(raw.RootElement.GetProperty("MigratorConnectionString").GetString())
        {
            Encrypt = SqlConnectionEncryptOption.Strict,
            TrustServerCertificate = false,
            HostNameInCertificate = settings.SqlServerName
        };
        admin["Server Certificate"] = settings.SqlCertificatePath;
        var store = new CommerceStore(new M0Sql(settings));
        var actor = M0Http.Id("issue09_owner");
        var sku = M0Http.Id("issue09_sku");
        var order = M0Http.Id("issue09_reservation");
        var cart = M0Http.Id("issue09_cart");
        var browser = M0Http.Id("issue09_browser");
        var security = new StockFixtureSecurity(actor);
        var protection = new EphemeralDataProtectionProvider();
        var service = new CommerceService(store, new M0Crypto(protection, protection), security, TimeProvider.System);
        bool maintenance;

        await using (var connection = new SqlConnection(admin.ConnectionString))
        {
            await connection.OpenAsync();
            await using (var read = new SqlCommand("SELECT Maintenance FROM m0.SecurityState WHERE Id=1", connection))
                maintenance = Convert.ToBoolean(await read.ExecuteScalarAsync());
            await using var seedTx = (SqlTransaction)await connection.BeginTransactionAsync();
            await using var seed = new SqlCommand(@"
UPDATE m0.SecurityState SET Maintenance=0 WHERE Id=1;
INSERT m0.Users(UserId,Email,PasswordHash,EmailConfirmed,Status,SecurityVersion,EncryptedSeed)
VALUES(@actor,@email,REPLICATE(N'x',100),1,'Active',1,N'synthetic-fixture-only');
INSERT m0.Grants(GrantId,UserId,Role,Permission,ScopeKind,ScopeId,ValidFrom,ValidUntil,GrantedBy,Reason)
VALUES(@grant,@actor,'Owner','stock.adjust','Dataset','demo_m0',DATEADD(minute,-1,SYSDATETIMEOFFSET()),DATEADD(hour,1,SYSDATETIMEOFFSET()),'fixture','dedicated Issue 09 SQL acceptance');
INSERT m0commerce.Inventory(Dataset,Sku,Price,Stock,Active) VALUES('demo_m0',@sku,123,4,1);
INSERT m0commerce.Reservations(Dataset,OrderId,Sku,Quantity,State,ExpiresAt) VALUES('demo_m0',@order,@sku,2,'Held',DATEADD(minute,15,SYSDATETIMEOFFSET()));", connection, seedTx);
            seed.Parameters.AddWithValue("@actor", actor);
            seed.Parameters.AddWithValue("@email", actor + "@example.test");
            seed.Parameters.AddWithValue("@grant", M0Http.Id("issue09_grant"));
            seed.Parameters.AddWithValue("@sku", sku);
            seed.Parameters.AddWithValue("@order", order);
            await seed.ExecuteNonQueryAsync();
            await seedTx.CommitAsync();
        }

        try
        {
            var reason = "dedicated Issue 09 stock acceptance";
            var addKey = M0Http.Id("issue09_key");
            var addBody = CommerceWire.Obj(("skuId", sku), ("quantityDelta", 3), ("reason", reason));
            var added = Body(await service.AdjustStock(Request(addBody, addKey, protection: protection)));
            Check(added.Number("onHand") == 9 && added.Number("reserved") == 2 && added.Number("sellableStock") == 7 && added.Text("availability") == "InStock",
                "authorized stock adjustment preserves held reservations and records sellable stock");

            var replay = Body(await service.AdjustStock(Request(addBody.DeepClone().AsObject(), addKey, protection: protection)));
            var inventoryAndMovements = await store.Transaction(async tx => (
                Convert.ToInt64(await tx.Scalar("SELECT Stock FROM m0commerce.Inventory WHERE Dataset='demo_m0' AND Sku=@sku", ("@sku", sku))),
                Convert.ToInt32(await tx.Scalar("SELECT COUNT(*) FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='InventoryMovement' AND JSON_VALUE(Body,'$.skuId')=@sku", ("@sku", sku)))));
            Check(replay.Text("id") == added.Text("id") && inventoryAndMovements == (7L, 1),
                "same idempotency key replays without applying a second stock movement");

            var floor = Body(await service.AdjustStock(Request(CommerceWire.Obj(("skuId", sku), ("quantityDelta", -7), ("reason", reason)), M0Http.Id("issue09_key"), protection: protection)));
            Check(floor.Number("onHand") == 2 && floor.Number("reserved") == 2 && floor.Number("sellableStock") == 0 && floor.Text("availability") == "OutOfStock",
                "sellable stock may reach zero while held reservation remains intact");
            try
            {
                await service.AdjustStock(Request(CommerceWire.Obj(("skuId", sku), ("quantityDelta", -1), ("reason", reason)), M0Http.Id("issue09_key"), protection: protection));
                throw new InvalidOperationException("FAILED: stock adjustment below reservation floor");
            }
            catch (M0Exception e) { Check(e.Status == 409 && e.Code == "STOCK_CHANGED", "adjustment cannot reduce on-hand below held reservations"); }

            await store.Transaction(async tx => { await tx.Save(new("Cart", cart, browser, 1, CommerceWire.Obj(("id", cart), ("mode", "mock"), ("version", CommerceWire.Version("Cart", cart, 1)), ("items", new JsonArray()), ("promotionCode", null))), true); return true; });
            try
            {
                await service.CartItem(Request(CommerceWire.Obj(("quantity", 1)), M0Http.Id("issue09_key"), cart, browser, CommerceWire.Version("Cart", cart, 1), protection), sku);
                throw new InvalidOperationException("FAILED: out-of-stock SKU entered cart");
            }
            catch (M0Exception e) { Check(e.Status == 409 && e.Code == "PRODUCT_UNAVAILABLE", "out-of-stock SKU cannot enter a new cart commitment"); }
            await store.Transaction(async tx => { var document = await tx.Get("Cart", cart); document.Body["items"] = new JsonArray(CommerceWire.Item(sku, 1, 123, 4)); await tx.Save(document with { Version = document.Version + 1 }); return true; });
            var staleVersion = CommerceWire.Version("Cart", cart, 2);
            try
            {
                await service.Quote(Request(CommerceWire.Obj(("cartVersion", staleVersion), ("deliveryMethod", "mock_home"), ("region", "demo_supported")), M0Http.Id("issue09_key"), cart, browser, staleVersion, protection));
                throw new InvalidOperationException("FAILED: stale out-of-stock cart received a quote");
            }
            catch (M0Exception e) { Check(e.Status == 409 && e.Code == "PRODUCT_UNAVAILABLE", "quote rechecks authoritative stock before creating a purchase commitment"); }

            var view = Body(await service.Stock(Request(null, M0Http.Id("issue09_key"), protection: protection), sku));
            Check(view.Number("onHand") == 2 && view.Number("reserved") == 2 && view.Number("sellableStock") == 0 && view.Text("availability") == "OutOfStock" && view["movements"]!.AsArray().Count == 2,
                "authorized stock view exposes sellable, reserved, availability, and movement history");
            var denied = new CommerceService(store, new M0Crypto(protection, protection), new StockFixtureSecurity(actor, allow: false), TimeProvider.System);
            try
            {
                await denied.Stock(Request(null, M0Http.Id("issue09_key"), protection: protection), sku);
                throw new InvalidOperationException("FAILED: unauthorized stock view");
            }
            catch (M0Exception e) { Check(e.Status == 403, "stock view rejects an unauthorized operator"); }
        }
        finally
        {
            await using var cleanup = new SqlConnection(admin.ConnectionString);
            await cleanup.OpenAsync();
            await using var remove = new SqlCommand(@"
DELETE m0commerce.Idempotency WHERE Dataset='demo_m0' AND ((Actor=@actor AND Scope=@scope) OR (Actor=@browser AND Scope LIKE 'setCartItem/%'));
DELETE m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='Cart' AND Id=@cart;
DELETE m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='InventoryMovement' AND JSON_VALUE(Body,'$.skuId')=@sku;
DELETE m0commerce.Reservations WHERE Dataset='demo_m0' AND OrderId=@order AND Sku=@sku;
DELETE m0commerce.Inventory WHERE Dataset='demo_m0' AND Sku=@sku;
DELETE m0.Grants WHERE UserId=@actor;
DELETE m0.Users WHERE UserId=@actor;
UPDATE m0.SecurityState SET Maintenance=@maintenance WHERE Id=1;", cleanup);
            remove.Parameters.AddWithValue("@actor", actor);
            remove.Parameters.AddWithValue("@browser", browser);
            remove.Parameters.AddWithValue("@cart", cart);
            remove.Parameters.AddWithValue("@scope", "stock-adjustment/" + sku);
            remove.Parameters.AddWithValue("@sku", sku);
            remove.Parameters.AddWithValue("@order", order);
            remove.Parameters.AddWithValue("@maintenance", maintenance);
            await remove.ExecuteNonQueryAsync();
        }
    }

    private static HttpContext Request(JsonObject? body, string key, string? cartId = null, string? browserId = null, string? version = null, IDataProtectionProvider? protection = null)
    {
        var context = new DefaultHttpContext();
        if (body is not null)
        {
            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body.ToJsonString()));
            context.Request.ContentType = "application/json";
        }
        context.Request.Headers["Idempotency-Key"] = key;
        if (version is not null) context.Request.Headers.IfMatch = '"' + version + '"';
        if (cartId is not null && browserId is not null)
        {
            var until = DateTimeOffset.UtcNow.AddHours(1).ToString("O");
            string Cookie(string name, string value) => name + "=" + (protection ?? throw new InvalidOperationException("COOKIE_PROTECTION_REQUIRED")).CreateProtector(name).Protect(CommerceWire.Obj(("value", value), ("until", until)).ToJsonString());
            context.Request.Headers.Cookie = Cookie("kohami.cart", cartId) + "; " + Cookie("kohami.browser", browserId);
        }
        return context;
    }

    private static JsonObject Body(IResult result) => result is IValueHttpResult value
        ? JsonSerializer.SerializeToNode(value.Value)?.AsObject() ?? throw new InvalidOperationException("HTTP_RESULT_BODY_INVALID")
        : throw new InvalidOperationException("HTTP_RESULT_NOT_JSON");

    private static void Check(bool pass, string name)
    {
        if (!pass) throw new InvalidOperationException("FAILED: " + name);
        Console.WriteLine("PASS: " + name);
    }

    private sealed class StockFixtureSecurity(string actor, bool allow = true) : IM0Security
    {
        public Task<M0Principal?> ResolveAsync(HttpContext context, CancellationToken ct = default) => Task.FromResult<M0Principal?>(null);
        public Task<M0Principal> RequirePermissionAsync(HttpContext context, string permission, string resourceKind, string resourceId, string? recentPurpose = null, CancellationToken ct = default) =>
            allow && permission == "stock.adjust"
                ? Task.FromResult(new M0Principal(actor, "synthetic-fixture-session", 1, "AdminMfaVerified", [permission]))
                : Task.FromException<M0Principal>(new M0Exception(403, "FORBIDDEN"));
        public Task<bool> CanDispatchAsync(string actorUserId, long securityVersion, string permission, string resourceKind, string resourceId, CancellationToken ct = default) => Task.FromResult(false);
    }
}
