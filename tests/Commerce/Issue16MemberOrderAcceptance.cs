using System.Text;
using System.Text.Json.Nodes;
using Kohami.M0.Commerce;
using Kohami.M0.Core;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

internal static class Issue16MemberOrderAcceptance
{
    public static async Task RunAsync(CommerceStore store, string adminConnectionString)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var memberA = "issue16_member_a_" + suffix;
        var memberB = "issue16_member_b_" + suffix;
        var browser = "issue16_browser_" + suffix;
        var orderA = "order_issue16_a_" + suffix;
        var orderB = "order_issue16_b_" + suffix;
        var guestOrder = "order_issue16_guest_" + suffix;
        var raceGuestOrder = "order_issue16_race_" + suffix;
        var checkoutCart = "cart_issue16_member_" + suffix;
        var capability = "cap_issue16_" + suffix;
        var raceCapability = "cap_issue16_race_" + suffix;
        string? memberCheckoutId = null;
        string? memberCheckoutOrderId = null;
        string? memberCheckoutQuoteId = null;
        int? originalStock = null;
        var email = memberA + "@example.test";
        var crypto = new M0Crypto(new EphemeralDataProtectionProvider());
        var memberAService = new CommerceService(store, crypto, new MemberSecurity(memberA), TimeProvider.System);
        var memberBService = new CommerceService(store, crypto, new MemberSecurity(memberB), TimeProvider.System);

        try
        {
            await using (var connection = new SqlConnection(adminConnectionString))
            {
                await connection.OpenAsync();
                await using var seed = new SqlCommand("""
                    INSERT m0.Users(UserId,Email,PasswordHash,EmailConfirmed,Status,SecurityVersion)
                    VALUES(@a,@email,REPLICATE(N'x',100),1,'Active',1),(@b,@emailB,REPLICATE(N'x',100),1,'Active',1);
                    INSERT m0commerce.Documents(Dataset,Kind,Id,OwnerId,Version,Body)
                    VALUES('demo_m0','Order',@orderA,@a,1,@bodyA),
                          ('demo_m0','Order',@orderB,@b,1,@bodyB),
                          ('demo_m0','Order',@guestOrder,@browser,1,@guestBody),
                          ('demo_m0','Order',@raceGuestOrder,@browser,1,@raceGuestBody);
                    INSERT m0commerce.GuestCapabilities(Dataset,Id,OrderId,BrowserId,Intent,ExpiresAt,Consumed)
                    VALUES('demo_m0',@cap,@guestOrder,@browser,'claim',DATEADD(hour,1,SYSDATETIMEOFFSET()),0),
                          ('demo_m0',@raceCap,@raceGuestOrder,@browser,'claim',DATEADD(hour,1,SYSDATETIMEOFFSET()),0);
                    """, connection);
                seed.Parameters.AddWithValue("@a", memberA);
                seed.Parameters.AddWithValue("@b", memberB);
                seed.Parameters.AddWithValue("@email", email);
                seed.Parameters.AddWithValue("@emailB", memberB + "@example.test");
                seed.Parameters.AddWithValue("@orderA", orderA);
                seed.Parameters.AddWithValue("@orderB", orderB);
                seed.Parameters.AddWithValue("@guestOrder", guestOrder);
                seed.Parameters.AddWithValue("@raceGuestOrder", raceGuestOrder);
                seed.Parameters.AddWithValue("@browser", browser);
                seed.Parameters.AddWithValue("@cap", capability);
                seed.Parameters.AddWithValue("@raceCap", raceCapability);
                seed.Parameters.AddWithValue("@bodyA", OrderBody(orderA, email));
                seed.Parameters.AddWithValue("@bodyB", OrderBody(orderB, memberB + "@example.test"));
                seed.Parameters.AddWithValue("@guestBody", OrderBody(guestOrder, email));
                seed.Parameters.AddWithValue("@raceGuestBody", OrderBody(raceGuestOrder, email));
                await seed.ExecuteNonQueryAsync();
            }

            long cartVersion;
            long itemPrice;
            await using (var connection = new SqlConnection(adminConnectionString))
            {
                await connection.OpenAsync();
                await using var inventory = new SqlCommand("SELECT Price,Stock FROM m0commerce.Inventory WHERE Dataset='demo_m0' AND Sku='sku_demo_01' AND Active=1", connection);
                await using var reader = await inventory.ExecuteReaderAsync();
                if (!await reader.ReadAsync()) throw new InvalidOperationException("ISSUE16_CHECKOUT_FIXTURE_SKU_MISSING");
                itemPrice = reader.GetInt64(0);
                originalStock = reader.GetInt32(1);
            }
            cartVersion = 1;
            var seededCart = new CommerceDocument("Cart", checkoutCart, browser, cartVersion, new JsonObject
            {
                ["id"] = checkoutCart,
                ["mode"] = "mock",
                ["version"] = CommerceWire.Version("Cart", checkoutCart, cartVersion),
                ["items"] = new JsonArray(CommerceWire.Item("sku_demo_01", 1, itemPrice, originalStock.Value)),
                ["promotionCode"] = null
            });
            await store.Transaction(async tx => { await tx.Save(seededCart, true); return true; });
            var quoteRequest = NewRequest("POST", "/checkout-quotes");
            AddProtectedCookie(quoteRequest, crypto, "kohami.command", new JsonObject { ["value"] = browser, ["until"] = DateTimeOffset.UtcNow.AddMinutes(10).ToString("O") }.ToJsonString());
            AddProtectedCookie(quoteRequest, crypto, "kohami.cart", new JsonObject { ["value"] = checkoutCart, ["until"] = DateTimeOffset.UtcNow.AddMinutes(10).ToString("O") }.ToJsonString());
            quoteRequest.Request.Headers.IfMatch = '"' + CommerceWire.Version("Cart", checkoutCart, cartVersion) + '"';
            quoteRequest.Request.Headers["Idempotency-Key"] = "issue16-member-quote-" + suffix;
            quoteRequest.Request.ContentType = "application/json";
            quoteRequest.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(new JsonObject { ["cartVersion"] = CommerceWire.Version("Cart", checkoutCart, cartVersion), ["deliveryMethod"] = "mock_home", ["region"] = "demo_supported" }.ToJsonString()));
            var quoteResult = await ReadJsonAsync(await memberAService.Quote(quoteRequest), quoteRequest);
            memberCheckoutQuoteId = quoteResult.Text("id");
            var checkoutRequest = NewRequest("POST", "/checkouts");
            AddProtectedCookie(checkoutRequest, crypto, "kohami.command", new JsonObject { ["value"] = browser, ["until"] = DateTimeOffset.UtcNow.AddMinutes(10).ToString("O") }.ToJsonString());
            AddProtectedCookie(checkoutRequest, crypto, "kohami.cart", new JsonObject { ["value"] = checkoutCart, ["until"] = DateTimeOffset.UtcNow.AddMinutes(10).ToString("O") }.ToJsonString());
            checkoutRequest.Request.Headers.IfMatch = '"' + CommerceWire.Version("Cart", checkoutCart, cartVersion) + '"';
            checkoutRequest.Request.Headers["Idempotency-Key"] = "issue16-member-checkout-" + suffix;
            checkoutRequest.Request.ContentType = "application/json";
            checkoutRequest.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(new JsonObject
            {
                ["quoteId"] = memberCheckoutQuoteId,
                ["cartVersion"] = CommerceWire.Version("Cart", checkoutCart, cartVersion),
                ["contact"] = new JsonObject { ["email"] = email },
                ["delivery"] = new JsonObject { ["method"] = "mock_home", ["region"] = "demo_supported" },
                ["confirmed"] = true
            }.ToJsonString()));
            var checkoutResult = await ReadJsonAsync(await memberAService.Checkout(checkoutRequest), checkoutRequest);
            memberCheckoutId = checkoutResult.Text("checkoutId");
            memberCheckoutOrderId = checkoutResult.Text("orderId");
            await using (var connection = new SqlConnection(adminConnectionString))
            {
                await connection.OpenAsync();
                await using var ownerQuery = new SqlCommand("SELECT OwnerId FROM m0commerce.Documents WHERE Dataset='demo_m0' AND Kind='Order' AND Id=@order", connection);
                ownerQuery.Parameters.AddWithValue("@order", memberCheckoutOrderId);
                Check((string?)await ownerQuery.ExecuteScalarAsync() == memberA, "Issue16 member checkout creates an order owned by the authenticated database account");
            }

            var listContext = NewRequest("GET", "/orders");
            var listA = await ReadJsonAsync(await memberAService.Orders(listContext), listContext);
            var listedA = listA["items"]!.AsArray().OfType<JsonObject>().Select(x => x.Text("id")).ToArray();
            Check(listedA.Length == 2 && listedA.Contains(orderA) && listedA.Contains(memberCheckoutOrderId, StringComparer.Ordinal), "Issue16 member order list returns the current member's orders, including member checkout, without auto-claiming same-email guest orders");

            var ownContext = NewRequest("GET", "/orders/" + orderA);
            var own = await ReadJsonAsync(await memberAService.Order(ownContext, orderA), ownContext);
            Check(own.Text("id") == orderA, "Issue16 member can read own full order projection");
            await ExpectStatusAsync(() => memberAService.Order(NewRequest("GET", "/orders/" + orderB), orderB), 404, "Issue16 member cannot read another member's order");
            var listBContext = NewRequest("GET", "/orders");
            var listB = await ReadJsonAsync(await memberBService.Orders(listBContext), listBContext);
            Check(listB["items"]!.AsArray().OfType<JsonObject>().Select(x => x.Text("id")).SequenceEqual(new[] { orderB }), "Issue16 order history remains isolated after switching accounts");
            await ExpectStatusAsync(() => new CommerceService(store, crypto, new MemberSecurity(null), TimeProvider.System).Orders(NewRequest("GET", "/orders")), 401, "Issue16 anonymous request cannot enumerate orders");

            var claim = NewRequest("POST", "/orders/" + guestOrder + "/member-claims");
            AddProtectedCookie(claim, crypto, "kohami.browser", new JsonObject { ["value"] = browser, ["until"] = DateTimeOffset.UtcNow.AddMinutes(10).ToString("O") }.ToJsonString());
            AddProtectedCookie(claim, crypto, "kohami.guest.action", new JsonObject { ["value"] = capability, ["until"] = DateTimeOffset.UtcNow.AddMinutes(10).ToString("O") }.ToJsonString());
            claim.Request.Headers["Idempotency-Key"] = "issue16-claim-" + suffix;
            claim.Request.ContentType = "application/json";
            claim.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{}"));
            var claimed = await ReadJsonAsync(await memberAService.ClaimGuestOrder(claim, guestOrder), claim);
            Check(claimed.Text("orderId") == guestOrder && claimed.Text("state") == "Claimed", "Issue16 original guest proof explicitly claims one order for the signed-in member");

            var replay = NewRequest("POST", "/orders/" + guestOrder + "/member-claims");
            AddProtectedCookie(replay, crypto, "kohami.browser", new JsonObject { ["value"] = browser, ["until"] = DateTimeOffset.UtcNow.AddMinutes(10).ToString("O") }.ToJsonString());
            AddProtectedCookie(replay, crypto, "kohami.guest.action", new JsonObject { ["value"] = capability, ["until"] = DateTimeOffset.UtcNow.AddMinutes(10).ToString("O") }.ToJsonString());
            replay.Request.Headers["Idempotency-Key"] = "issue16-claim-" + suffix;
            replay.Request.ContentType = "application/json";
            replay.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{}"));
            var repeated = await ReadJsonAsync(await memberAService.ClaimGuestOrder(replay, guestOrder), replay);
            Check(repeated.ToJsonString() == claimed.ToJsonString(), "Issue16 identical claim replay returns the original result");

            var afterClaimContext = NewRequest("GET", "/orders");
            var afterClaim = await memberAService.Orders(afterClaimContext);
            var afterList = await ReadJsonAsync(afterClaim, afterClaimContext);
            Check(afterList["items"]!.AsArray().OfType<JsonObject>().Any(x => x.Text("id") == guestOrder), "Issue16 claimed guest order appears only in the winning member's history");
            var otherClaim = NewRequest("POST", "/orders/" + guestOrder + "/member-claims");
            AddProtectedCookie(otherClaim, crypto, "kohami.browser", new JsonObject { ["value"] = browser, ["until"] = DateTimeOffset.UtcNow.AddMinutes(10).ToString("O") }.ToJsonString());
            AddProtectedCookie(otherClaim, crypto, "kohami.guest.action", new JsonObject { ["value"] = capability, ["until"] = DateTimeOffset.UtcNow.AddMinutes(10).ToString("O") }.ToJsonString());
            otherClaim.Request.Headers["Idempotency-Key"] = "issue16-second-claim-" + suffix;
            otherClaim.Request.ContentType = "application/json";
            otherClaim.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{}"));
            await ExpectStatusAsync(() => memberBService.ClaimGuestOrder(otherClaim, guestOrder), 404, "Issue16 a different member cannot claim the already claimed guest order");

            var raceA = NewClaimRequest(crypto, browser, raceCapability, "issue16-race-a-" + suffix);
            var raceB = NewClaimRequest(crypto, browser, raceCapability, "issue16-race-b-" + suffix);
            async Task<bool> TryClaimAsync(CommerceService service, HttpContext request)
            {
                try { await service.ClaimGuestOrder(request, raceGuestOrder); return true; }
                catch (M0Exception error) when (error.Status == 404) { return false; }
            }
            var raceWinners = await Task.WhenAll(TryClaimAsync(memberAService, raceA), TryClaimAsync(memberBService, raceB));
            Check(raceWinners.Count(x => x) == 1, "Issue16 concurrent members competing for one guest capability have exactly one winner");
            await using (var connection = new SqlConnection(adminConnectionString))
            {
                await connection.OpenAsync();
                await using var verifyRace = new SqlCommand("SELECT d.OwnerId,c.Consumed FROM m0commerce.Documents d JOIN m0commerce.GuestCapabilities c ON c.Dataset=d.Dataset AND c.OrderId=d.Id WHERE d.Dataset='demo_m0' AND d.Kind='Order' AND d.Id=@order AND c.Id=@cap", connection);
                verifyRace.Parameters.AddWithValue("@order", raceGuestOrder);
                verifyRace.Parameters.AddWithValue("@cap", raceCapability);
                await using var reader = await verifyRace.ExecuteReaderAsync();
                if (!await reader.ReadAsync()) throw new InvalidOperationException("ISSUE16_RACE_SQL_ROW_MISSING");
                Check(reader.GetString(0) is var winner && (winner == memberA || winner == memberB) && reader.GetBoolean(1), "Issue16 concurrent claim SQL records one member and consumes the shared capability");
            }

            await using (var connection = new SqlConnection(adminConnectionString))
            {
                await connection.OpenAsync();
                await using var verify = new SqlCommand("""
                    SELECT d.OwnerId,JSON_VALUE(d.Body,'$.email'),c.Consumed,c.OperationKey
                    FROM m0commerce.Documents d JOIN m0commerce.GuestCapabilities c ON c.Dataset=d.Dataset AND c.OrderId=d.Id
                    WHERE d.Dataset='demo_m0' AND d.Kind='Order' AND d.Id=@order AND c.Id=@cap
                    """, connection);
                verify.Parameters.AddWithValue("@order", guestOrder);
                verify.Parameters.AddWithValue("@cap", capability);
                await using var reader = await verify.ExecuteReaderAsync();
                if (!await reader.ReadAsync()) throw new InvalidOperationException("ISSUE16_CLAIM_SQL_ROW_MISSING");
                Check(reader.GetString(0) == memberA && reader.GetString(1) == email, "Issue16 SQL transfers ownership only and preserves original contact email");
                Check(reader.GetBoolean(2) && reader.GetString(3) == "issue16-claim-" + suffix, "Issue16 claim capability is consumed with its exact idempotency operation");
            }
        }
        finally
        {
            await using var connection = new SqlConnection(adminConnectionString);
            await connection.OpenAsync();
            await using var cleanup = new SqlCommand("""
                DELETE m0commerce.Idempotency WHERE Dataset='demo_m0' AND Actor IN(@a,@b);
                DELETE m0commerce.Idempotency WHERE Dataset='demo_m0' AND Actor=@browser;
                DELETE m0commerce.GuestCapabilities WHERE Dataset='demo_m0' AND Id IN(@cap,@raceCap);
                DELETE m0commerce.Reservations WHERE Dataset='demo_m0' AND OrderId=@memberCheckoutOrder;
                DELETE m0commerce.Documents WHERE Dataset='demo_m0' AND ((Kind='Order' AND Id IN(@orderA,@orderB,@guestOrder,@raceGuestOrder,@memberCheckoutOrder)) OR (Kind='Checkout' AND Id=@memberCheckout) OR (Kind='Quote' AND Id=@memberCheckoutQuote) OR (Kind='Cart' AND Id=@cart));
                IF @stock IS NOT NULL UPDATE m0commerce.Inventory SET Stock=@stock WHERE Dataset='demo_m0' AND Sku='sku_demo_01';
                DELETE m0.Users WHERE UserId IN(@a,@b);
                """, connection);
            cleanup.Parameters.AddWithValue("@a", memberA);
            cleanup.Parameters.AddWithValue("@b", memberB);
            cleanup.Parameters.AddWithValue("@cap", capability);
            cleanup.Parameters.AddWithValue("@raceCap", raceCapability);
            cleanup.Parameters.AddWithValue("@orderA", orderA);
            cleanup.Parameters.AddWithValue("@orderB", orderB);
            cleanup.Parameters.AddWithValue("@guestOrder", guestOrder);
            cleanup.Parameters.AddWithValue("@raceGuestOrder", raceGuestOrder);
            cleanup.Parameters.AddWithValue("@browser", browser);
            cleanup.Parameters.AddWithValue("@memberCheckout", (object?)memberCheckoutId ?? DBNull.Value);
            cleanup.Parameters.AddWithValue("@memberCheckoutOrder", (object?)memberCheckoutOrderId ?? DBNull.Value);
            cleanup.Parameters.AddWithValue("@memberCheckoutQuote", (object?)memberCheckoutQuoteId ?? DBNull.Value);
            cleanup.Parameters.AddWithValue("@cart", checkoutCart);
            cleanup.Parameters.AddWithValue("@stock", (object?)originalStock ?? DBNull.Value);
            await cleanup.ExecuteNonQueryAsync();
        }
    }

    static string OrderBody(string id, string email) => new JsonObject
    {
        ["id"] = id,
        ["mode"] = "mock",
        ["placedAt"] = DateTimeOffset.UtcNow.ToString("O"),
        ["orderState"] = "Confirmed",
        ["paymentState"] = "Succeeded",
        ["compensationRequired"] = false,
        ["email"] = email,
        ["snapshot"] = new JsonObject { ["currency"] = "TWD", ["totalTwd"] = 1200, ["lines"] = new JsonArray() }
    }.ToJsonString();

    static DefaultHttpContext NewRequest(string method, string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        context.RequestServices = new TestServices();
        return context;
    }

    static DefaultHttpContext NewClaimRequest(M0Crypto crypto, string browser, string capability, string idempotencyKey)
    {
        var context = NewRequest("POST", "/orders/claim/member-claims");
        AddProtectedCookie(context, crypto, "kohami.browser", new JsonObject { ["value"] = browser, ["until"] = DateTimeOffset.UtcNow.AddMinutes(10).ToString("O") }.ToJsonString());
        AddProtectedCookie(context, crypto, "kohami.guest.action", new JsonObject { ["value"] = capability, ["until"] = DateTimeOffset.UtcNow.AddMinutes(10).ToString("O") }.ToJsonString());
        context.Request.Headers["Idempotency-Key"] = idempotencyKey;
        context.Request.ContentType = "application/json";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{}"));
        return context;
    }

    static void AddProtectedCookie(HttpContext context, M0Crypto crypto, string name, string value)
    {
        var protectedValue = crypto.Sessions.CreateProtector(name).Protect(value);
        var existing = context.Request.Headers.Cookie.ToString();
        context.Request.Headers.Cookie = string.IsNullOrEmpty(existing) ? name + "=" + protectedValue : existing + "; " + name + "=" + protectedValue;
    }

    static async Task<JsonObject> ReadJsonAsync(IResult result, HttpContext context)
    {
        await result.ExecuteAsync(context);
        context.Response.Body.Position = 0;
        return (JsonNode.Parse(await new StreamReader(context.Response.Body).ReadToEndAsync()) as JsonObject)
            ?? throw new InvalidOperationException("ISSUE16_RESPONSE_INVALID");
    }

    static async Task ExpectStatusAsync(Func<Task<IResult>> operation, int status, string name)
    {
        try { _ = await operation(); throw new InvalidOperationException("FAILED: " + name); }
        catch (M0Exception error) when (error.Status == status) { Check(true, name); }
    }

    static void Check(bool pass, string name)
    {
        if (!pass) throw new InvalidOperationException("FAILED: " + name);
        Console.WriteLine("PASS: " + name);
    }

    sealed class MemberSecurity(string? userId) : IM0Security
    {
        public Task<M0Principal?> ResolveAsync(HttpContext context, CancellationToken ct = default) =>
            Task.FromResult(userId is null ? null : new M0Principal(userId, "issue16-session", 1, "MemberAuthenticated", Array.Empty<string>()));
        public Task<M0Principal> RequirePermissionAsync(HttpContext context, string permission, string resourceKind, string resourceId, CancellationToken ct = default) =>
            Task.FromException<M0Principal>(new M0Exception(403, "PERMISSION_CHANGED"));
        public Task<bool> CanDispatchAsync(string actorUserId, long securityVersion, string permission, string resourceKind, string resourceId, CancellationToken ct = default) => Task.FromResult(false);
    }

    sealed class TestServices : IServiceProvider
    {
        public object? GetService(Type serviceType) => serviceType == typeof(IOptions<JsonOptions>)
            ? Options.Create(new JsonOptions())
            : serviceType == typeof(Microsoft.Extensions.Logging.ILoggerFactory)
                ? NullLoggerFactory.Instance
                : null;
    }
}
