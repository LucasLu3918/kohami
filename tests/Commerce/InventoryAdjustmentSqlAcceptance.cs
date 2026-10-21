using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Kohami.Commerce.Domain;
using Kohami.Commerce;
using Kohami.Core;
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
        var settings = RuntimeSettings.Load(config);
        var admin = new SqlConnectionStringBuilder(raw.RootElement.GetProperty("MigratorConnectionString").GetString())
        {
            Encrypt = SqlConnectionEncryptOption.Strict,
            TrustServerCertificate = false,
            HostNameInCertificate = settings.SqlServerName
        };
        admin["Server Certificate"] = settings.SqlCertificatePath;
        var store = new CommerceStore(new SqlRuntime(settings));
        var actor = FoundationHttp.Id("issue09_owner");
        var sku = FoundationHttp.Id("issue09_sku");
        var order = FoundationHttp.Id("issue09_reservation");
        var cart = FoundationHttp.Id("issue09_cart");
        var browser = FoundationHttp.Id("issue09_browser");
        var security = new StockFixtureSecurity(actor);
        var protection = new EphemeralDataProtectionProvider();
        var service = new CommerceService(store, new SessionCrypto(protection), security, TimeProvider.System);
        bool maintenance;

        await using (var connection = new SqlConnection(admin.ConnectionString))
        {
            await connection.OpenAsync();
            await using (var read = new SqlCommand("SELECT Maintenance FROM identity_access.SecurityState WHERE Id=1", connection))
                maintenance = Convert.ToBoolean(await read.ExecuteScalarAsync());
            await using var seedTx = (SqlTransaction)await connection.BeginTransactionAsync();
            await using var seed = new SqlCommand(@"
UPDATE identity_access.SecurityState SET Maintenance=0 WHERE Id=1;
INSERT identity_access.Users(UserId,Email,PasswordHash,EmailConfirmed,Status,SecurityVersion)
VALUES(@actor,@email,REPLICATE(N'x',100),1,'Active',1);
INSERT identity_access.Grants(GrantId,UserId,Role,Permission,ScopeKind,ScopeId,ValidFrom,ValidUntil,GrantedBy,Reason)
VALUES(@grant,@actor,'Owner','stock.adjust','Dataset','kohami_local',DATEADD(minute,-1,SYSDATETIMEOFFSET()),DATEADD(hour,1,SYSDATETIMEOFFSET()),'fixture','dedicated Issue 09 SQL acceptance');
INSERT commerce.Inventory(Dataset,Sku,Price,Stock,Active) VALUES('kohami_local',@sku,123,4,1);
INSERT commerce.Reservations(Dataset,OrderId,Sku,Quantity,State,ExpiresAt) VALUES('kohami_local',@order,@sku,2,'Held',DATEADD(minute,15,SYSDATETIMEOFFSET()));", connection, seedTx);
            seed.Parameters.AddWithValue("@actor", actor);
            seed.Parameters.AddWithValue("@email", actor + "@example.test");
            seed.Parameters.AddWithValue("@grant", FoundationHttp.Id("issue09_grant"));
            seed.Parameters.AddWithValue("@sku", sku);
            seed.Parameters.AddWithValue("@order", order);
            await seed.ExecuteNonQueryAsync();
            await seedTx.CommitAsync();
        }

        try
        {
            var reason = "dedicated Issue 09 stock acceptance";
            var addKey = FoundationHttp.Id("issue09_key");
            var addBody = CommerceWire.Obj(("skuId", sku), ("quantityDelta", 3), ("reason", reason));
            var added = Body(await service.AdjustStock(Request(addBody, addKey, protection: protection)));
            Check(added.Number("onHand") == 9 && added.Number("reserved") == 2 && added.Number("sellableStock") == 7 && added.Text("availability") == "InStock",
                "authorized stock adjustment preserves held reservations and records sellable stock");

            var replay = Body(await service.AdjustStock(Request(addBody.DeepClone().AsObject(), addKey, protection: protection)));
            var inventoryAndMovements = await store.Transaction(async tx => (
                Convert.ToInt64(await tx.Scalar("SELECT Stock FROM commerce.Inventory WHERE Dataset='kohami_local' AND Sku=@sku", ("@sku", sku))),
                Convert.ToInt32(await tx.Scalar("SELECT COUNT(*) FROM commerce.Documents WHERE Dataset='kohami_local' AND Kind='InventoryMovement' AND JSON_VALUE(Body,'$.skuId')=@sku", ("@sku", sku)))));
            Check(replay.Text("id") == added.Text("id") && inventoryAndMovements == (7L, 1),
                "same idempotency key replays without applying a second stock movement");

            var floor = Body(await service.AdjustStock(Request(CommerceWire.Obj(("skuId", sku), ("quantityDelta", -7), ("reason", reason)), FoundationHttp.Id("issue09_key"), protection: protection)));
            Check(floor.Number("onHand") == 2 && floor.Number("reserved") == 2 && floor.Number("sellableStock") == 0 && floor.Text("availability") == "OutOfStock",
                "sellable stock may reach zero while held reservation remains intact");
            try
            {
                await service.AdjustStock(Request(CommerceWire.Obj(("skuId", sku), ("quantityDelta", -1), ("reason", reason)), FoundationHttp.Id("issue09_key"), protection: protection));
                throw new InvalidOperationException("FAILED: stock adjustment below reservation floor");
            }
            catch (KohamiException e) { Check(e.Status == 409 && e.Code == "STOCK_CHANGED", "adjustment cannot reduce on-hand below held reservations"); }

            await store.Transaction(async tx => { await tx.Save(new("Cart", cart, browser, 1, CommerceWire.Obj(("id", cart), ("mode", "mock"), ("version", CommerceWire.Version("Cart", cart, 1)), ("items", new JsonArray()), ("promotionCode", null))), true); return true; });
            try
            {
                await service.CartItem(Request(CommerceWire.Obj(("quantity", 1)), FoundationHttp.Id("issue09_key"), cart, browser, CommerceWire.Version("Cart", cart, 1), protection), sku);
                throw new InvalidOperationException("FAILED: out-of-stock SKU entered cart");
            }
            catch (KohamiException e) { Check(e.Status == 409 && e.Code == "PRODUCT_UNAVAILABLE", "out-of-stock SKU cannot enter a new cart commitment"); }
            await store.Transaction(async tx => { var document = await tx.Get("Cart", cart); document.Body["items"] = new JsonArray(CommerceWire.Item(sku, 1, 123, 4)); await tx.Save(document with { Version = document.Version + 1 }); return true; });
            var staleVersion = CommerceWire.Version("Cart", cart, 2);
            try
            {
                await service.Quote(Request(CommerceWire.Obj(("cartVersion", staleVersion), ("deliveryMethod", "mock_home"), ("region", "demo_supported")), FoundationHttp.Id("issue09_key"), cart, browser, staleVersion, protection));
                throw new InvalidOperationException("FAILED: stale out-of-stock cart received a quote");
            }
            catch (KohamiException e) { Check(e.Status == 409 && e.Code == "PRODUCT_UNAVAILABLE", "quote rechecks authoritative stock before creating a purchase commitment"); }

            var view = Body(await service.Stock(Request(null, FoundationHttp.Id("issue09_key"), protection: protection), sku));
            Check(view.Number("onHand") == 2 && view.Number("reserved") == 2 && view.Number("sellableStock") == 0 && view.Text("availability") == "OutOfStock" && view["movements"]!.AsArray().Count == 2,
                "authorized stock view exposes sellable, reserved, availability, and movement history");
            var denied = new CommerceService(store, new SessionCrypto(protection), new StockFixtureSecurity(actor, allow: false), TimeProvider.System);
            try
            {
                await denied.Stock(Request(null, FoundationHttp.Id("issue09_key"), protection: protection), sku);
                throw new InvalidOperationException("FAILED: unauthorized stock view");
            }
            catch (KohamiException e) { Check(e.Status == 403, "stock view rejects an unauthorized operator"); }
        }
        finally
        {
            await using var cleanup = new SqlConnection(admin.ConnectionString);
            await cleanup.OpenAsync();
            await using var remove = new SqlCommand(@"
DELETE commerce.Idempotency WHERE Dataset='kohami_local' AND ((Actor=@actor AND Scope=@scope) OR (Actor=@browser AND Scope LIKE 'setCartItem/%'));
DELETE commerce.Documents WHERE Dataset='kohami_local' AND Kind='Cart' AND Id=@cart;
DELETE commerce.Documents WHERE Dataset='kohami_local' AND Kind='InventoryMovement' AND JSON_VALUE(Body,'$.skuId')=@sku;
DELETE commerce.Reservations WHERE Dataset='kohami_local' AND OrderId=@order AND Sku=@sku;
DELETE commerce.Inventory WHERE Dataset='kohami_local' AND Sku=@sku;
DELETE identity_access.Grants WHERE UserId=@actor;
DELETE identity_access.Users WHERE UserId=@actor;
UPDATE identity_access.SecurityState SET Maintenance=@maintenance WHERE Id=1;", cleanup);
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

    private sealed class StockFixtureSecurity(string actor, bool allow = true) : ISecurity
    {
        public Task<Principal?> ResolveAsync(HttpContext context, CancellationToken ct = default) => Task.FromResult<Principal?>(null);
        public Task<Principal> RequirePermissionAsync(HttpContext context, string permission, string resourceKind, string resourceId, CancellationToken ct = default) =>
            allow && permission == "stock.adjust"
                ? Task.FromResult(new Principal(actor, "synthetic-fixture-session", 1, "AdminAuthenticated", [permission]))
                : Task.FromException<Principal>(new KohamiException(403, "FORBIDDEN"));
        public Task<bool> CanDispatchAsync(string actorUserId, long securityVersion, string permission, string resourceKind, string resourceId, CancellationToken ct = default) => Task.FromResult(false);
    }
}
