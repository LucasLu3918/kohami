using System.Text;
using System.Text.Json.Nodes;
using Kohami.Commerce;
using Kohami.Core;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

internal static class Issue16MemberOrderAcceptance
{
    public static async Task RunAsync(CommerceStore store, string adminConnectionString, bool includeWallet = true)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var memberA = "issue16_member_a_" + suffix;
        var memberB = "issue16_member_b_" + suffix;
        var browser = "issue16_browser_" + suffix;
        var orderA = "order_issue16_a_" + suffix;
        var orderB = "order_issue16_b_" + suffix;
        var guestOrder = "order_issue16_guest_" + suffix;
        var checkoutCart = "cart_issue16_member_" + suffix;
        var walletCart = "cart_issue16_wallet_" + suffix;
        string? memberCheckoutId = null;
        string? memberCheckoutOrderId = null;
        string? memberCheckoutQuoteId = null;
        string? walletCheckoutId = null;
        string? walletCheckoutOrderId = null;
        string? walletCheckoutQuoteId = null;
        int? originalStock = null;
        var email = memberA + "@example.test";
        var crypto = new SessionCrypto(new EphemeralDataProtectionProvider());
        var memberAService = new CommerceService(store, crypto, new MemberSecurity(memberA, email), TimeProvider.System);
        var memberBService = new CommerceService(store, crypto, new MemberSecurity(memberB, memberB + "@example.test"), TimeProvider.System);

        try
        {
            await using (var connection = new SqlConnection(adminConnectionString))
            {
                await connection.OpenAsync();
                await using var seed = new SqlCommand("""
                    INSERT identity_access.Users(UserId,Email,PasswordHash,EmailConfirmed,Status,SecurityVersion)
                    VALUES(@a,@email,REPLICATE(N'x',100),1,'Active',1),(@b,@emailB,REPLICATE(N'x',100),1,'Active',1);
                    INSERT commerce.Documents(Dataset,Kind,Id,OwnerId,Version,Body)
                    VALUES('kohami_local','Order',@orderA,@a,1,@bodyA),
                          ('kohami_local','Order',@orderB,@b,1,@bodyB),
                          ('kohami_local','Order',@guestOrder,@browser,1,@guestBody);
                    """, connection);
                seed.Parameters.AddWithValue("@a", memberA);
                seed.Parameters.AddWithValue("@b", memberB);
                seed.Parameters.AddWithValue("@email", email);
                seed.Parameters.AddWithValue("@emailB", memberB + "@example.test");
                seed.Parameters.AddWithValue("@orderA", orderA);
                seed.Parameters.AddWithValue("@orderB", orderB);
                seed.Parameters.AddWithValue("@guestOrder", guestOrder);
                seed.Parameters.AddWithValue("@browser", browser);
                seed.Parameters.AddWithValue("@bodyA", OrderBody(orderA, email));
                seed.Parameters.AddWithValue("@bodyB", OrderBody(orderB, memberB + "@example.test"));
                seed.Parameters.AddWithValue("@guestBody", OrderBody(guestOrder, email));
                await seed.ExecuteNonQueryAsync();
            }

            long cartVersion;
            long itemPrice;
            await using (var connection = new SqlConnection(adminConnectionString))
            {
                await connection.OpenAsync();
                await using var inventory = new SqlCommand("SELECT Price,Stock FROM commerce.Inventory WHERE Dataset='kohami_local' AND Sku='sku_demo_01' AND Active=1", connection);
                await using var reader = await inventory.ExecuteReaderAsync();
                if (!await reader.ReadAsync()) throw new InvalidOperationException("ISSUE16_CHECKOUT_FIXTURE_SKU_MISSING");
                itemPrice = reader.GetInt64(0);
                originalStock = reader.GetInt32(1);
            }
            await using (var connection = new SqlConnection(adminConnectionString))
            {
                await connection.OpenAsync();
                await using var prepareInventory = new SqlCommand(
                    "UPDATE commerce.Inventory SET Stock=2 WHERE Dataset='kohami_local' AND Sku='sku_demo_01' AND Active=1",
                    connection);
                if (await prepareInventory.ExecuteNonQueryAsync() != 1)
                    throw new InvalidOperationException("ISSUE16_CHECKOUT_FIXTURE_INVENTORY_PREPARE_FAILED");
            }
            cartVersion = 1;
            var seededCart = new CommerceDocument("Cart", checkoutCart, browser, cartVersion, new JsonObject
            {
                ["id"] = checkoutCart,
                ["mode"] = "mock",
                ["version"] = CommerceWire.Version("Cart", checkoutCart, cartVersion),
                ["items"] = new JsonArray(CommerceWire.Item("sku_demo_01", 1, itemPrice, 1)),
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
            DefaultHttpContext CheckoutRequest(string contactEmail, string? paymentMethod = null)
            {
                var context = NewRequest("POST", "/checkouts");
                AddProtectedCookie(context, crypto, "kohami.command", new JsonObject { ["value"] = browser, ["until"] = DateTimeOffset.UtcNow.AddMinutes(10).ToString("O") }.ToJsonString());
                AddProtectedCookie(context, crypto, "kohami.cart", new JsonObject { ["value"] = checkoutCart, ["until"] = DateTimeOffset.UtcNow.AddMinutes(10).ToString("O") }.ToJsonString());
                context.Request.Headers.IfMatch = '"' + CommerceWire.Version("Cart", checkoutCart, cartVersion) + '"';
                context.Request.Headers["Idempotency-Key"] = "issue16-member-checkout-" + suffix;
                context.Request.ContentType = "application/json";
                var checkoutBody = new JsonObject
                {
                    ["quoteId"] = memberCheckoutQuoteId,
                    ["cartVersion"] = CommerceWire.Version("Cart", checkoutCart, cartVersion),
                    ["contact"] = new JsonObject { ["email"] = contactEmail },
                    ["delivery"] = new JsonObject { ["method"] = "mock_home", ["region"] = "demo_supported" },
                    ["confirmed"] = true
                };
                if (paymentMethod is not null) checkoutBody["paymentMethod"] = paymentMethod;
                context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(checkoutBody.ToJsonString()));
                return context;
            }

            var anonymousService = new CommerceService(store, crypto, new MemberSecurity(null, ""), TimeProvider.System);
            await ExpectStatusAsync(() => anonymousService.Checkout(CheckoutRequest(email)), 401, "Issue16 anonymous checkout cannot create an order");
            await ExpectStatusAsync(() => memberAService.Checkout(CheckoutRequest(memberB + "@example.test")), 422, "Issue16 checkout rejects an email different from the authenticated member account");

            var checkoutRequest = CheckoutRequest(email, "card");
            var checkoutResult = await ReadJsonAsync(await memberAService.Checkout(checkoutRequest), checkoutRequest);
            memberCheckoutId = checkoutResult.Text("checkoutId");
            memberCheckoutOrderId = checkoutResult.Text("orderId");
            await using (var connection = new SqlConnection(adminConnectionString))
            {
                await connection.OpenAsync();
                await using var ownerQuery = new SqlCommand("SELECT OwnerId,JSON_VALUE(Body,'$.email') FROM commerce.Documents WHERE Dataset='kohami_local' AND Kind='Order' AND Id=@order", connection);
                ownerQuery.Parameters.AddWithValue("@order", memberCheckoutOrderId);
                await using var ownerReader = await ownerQuery.ExecuteReaderAsync();
                if (!await ownerReader.ReadAsync()) throw new InvalidOperationException("ISSUE16_CHECKOUT_ORDER_MISSING");
                Check(ownerReader.GetString(0) == memberA && ownerReader.GetString(1) == email, "Issue16 member checkout creates an order owned by the authenticated account and stores its account email");
            }

            if (includeWallet)
            {
            var walletCartDocument = new CommerceDocument("Cart", walletCart, browser, 1, new JsonObject
            {
                ["id"] = walletCart,
                ["mode"] = "mock",
                ["version"] = CommerceWire.Version("Cart", walletCart, 1),
                ["items"] = new JsonArray(CommerceWire.Item("sku_demo_01", 1, itemPrice, 1)),
                ["promotionCode"] = null
            });
            await store.Transaction(async tx => { await tx.Save(walletCartDocument, true); return true; });
            var walletQuoteRequest = NewRequest("POST", "/checkout-quotes");
            AddProtectedCookie(walletQuoteRequest, crypto, "kohami.command", new JsonObject { ["value"] = browser, ["until"] = DateTimeOffset.UtcNow.AddMinutes(10).ToString("O") }.ToJsonString());
            AddProtectedCookie(walletQuoteRequest, crypto, "kohami.cart", new JsonObject { ["value"] = walletCart, ["until"] = DateTimeOffset.UtcNow.AddMinutes(10).ToString("O") }.ToJsonString());
            walletQuoteRequest.Request.Headers.IfMatch = '"' + CommerceWire.Version("Cart", walletCart, 1) + '"';
            walletQuoteRequest.Request.Headers["Idempotency-Key"] = "issue16-wallet-quote-" + suffix;
            walletQuoteRequest.Request.ContentType = "application/json";
            walletQuoteRequest.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(new JsonObject { ["cartVersion"] = CommerceWire.Version("Cart", walletCart, 1), ["deliveryMethod"] = "mock_home", ["region"] = "demo_supported" }.ToJsonString()));
            var walletQuote = await ReadJsonAsync(await memberAService.Quote(walletQuoteRequest), walletQuoteRequest);
            walletCheckoutQuoteId = walletQuote.Text("id");
            var walletTotal = walletQuote["snapshot"]!.AsObject().Number("totalTwd");
            await using (var connection = new SqlConnection(adminConnectionString))
            {
                await connection.OpenAsync();
                await using var creditTransaction = (SqlTransaction)await connection.BeginTransactionAsync();
                await using var credit = new SqlCommand("EXEC commerce.wallet_synthetic_credit @UserId,@SourceId,@AmountTwd", connection, creditTransaction);
                credit.Parameters.AddWithValue("@UserId", memberA);
                credit.Parameters.AddWithValue("@SourceId", "synthetic-test-credit:issue16:" + suffix);
                credit.Parameters.AddWithValue("@AmountTwd", walletTotal);
                await credit.ExecuteNonQueryAsync();
                await creditTransaction.CommitAsync();
            }
            var walletRequest = NewRequest("POST", "/checkouts");
            AddProtectedCookie(walletRequest, crypto, "kohami.command", new JsonObject { ["value"] = browser, ["until"] = DateTimeOffset.UtcNow.AddMinutes(10).ToString("O") }.ToJsonString());
            AddProtectedCookie(walletRequest, crypto, "kohami.cart", new JsonObject { ["value"] = walletCart, ["until"] = DateTimeOffset.UtcNow.AddMinutes(10).ToString("O") }.ToJsonString());
            walletRequest.Request.Headers.IfMatch = '"' + CommerceWire.Version("Cart", walletCart, 1) + '"';
            walletRequest.Request.Headers["Idempotency-Key"] = "issue16-wallet-checkout-" + suffix;
            walletRequest.Request.ContentType = "application/json";
            walletRequest.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(new JsonObject
            {
                ["quoteId"] = walletCheckoutQuoteId,
                ["cartVersion"] = CommerceWire.Version("Cart", walletCart, 1),
                ["contact"] = new JsonObject { ["email"] = email },
                ["delivery"] = new JsonObject { ["method"] = "mock_home", ["region"] = "demo_supported" },
                ["confirmed"] = true,
                ["paymentMethod"] = "wallet"
            }.ToJsonString()));
            var walletResult = await ReadJsonAsync(await memberAService.Checkout(walletRequest), walletRequest);
            walletCheckoutId = walletResult.Text("checkoutId");
            walletCheckoutOrderId = walletResult.Text("orderId");
            await using (var connection = new SqlConnection(adminConnectionString))
            {
                await connection.OpenAsync();
                await using var walletOrder = new SqlCommand("SELECT OwnerId,JSON_VALUE(Body,'$.email'),JSON_VALUE(Body,'$.paymentMethod'),JSON_VALUE(Body,'$.paymentState') FROM commerce.Documents WHERE Dataset='kohami_local' AND Kind='Order' AND Id=@order", connection);
                walletOrder.Parameters.AddWithValue("@order", walletCheckoutOrderId);
                await using var walletReader = await walletOrder.ExecuteReaderAsync();
                if (!await walletReader.ReadAsync()) throw new InvalidOperationException("ISSUE16_WALLET_CHECKOUT_ORDER_MISSING");
                Check(walletReader.GetString(0) == memberA && walletReader.GetString(1) == email
                    && walletReader.GetString(2) == "wallet" && walletReader.GetString(3) == "Succeeded",
                    "Issue16 signed-in member can pay from wallet using a browser-owned pre-login cart");
            }
            }

            var listContext = NewRequest("GET", "/orders");
            var listA = await ReadJsonAsync(await memberAService.Orders(listContext), listContext);
            var listedA = listA["items"]!.AsArray().OfType<JsonObject>().Select(x => x.Text("id")).ToArray();
            Check(listedA.Length == (includeWallet ? 3 : 2) && listedA.Contains(orderA)
                && listedA.Contains(memberCheckoutOrderId, StringComparer.Ordinal)
                && (!includeWallet || listedA.Contains(walletCheckoutOrderId, StringComparer.Ordinal)),
                "Issue16 member order list includes only this member's card and wallet orders");

            var ownContext = NewRequest("GET", "/orders/" + orderA);
            var own = await ReadJsonAsync(await memberAService.Order(ownContext, orderA), ownContext);
            Check(own.Text("id") == orderA, "Issue16 member can read own full order projection");
            await ExpectStatusAsync(() => memberAService.Order(NewRequest("GET", "/orders/" + orderB), orderB), 404, "Issue16 member cannot read another member's order");
            var listBContext = NewRequest("GET", "/orders");
            var listB = await ReadJsonAsync(await memberBService.Orders(listBContext), listBContext);
            Check(listB["items"]!.AsArray().OfType<JsonObject>().Select(x => x.Text("id")).SequenceEqual(new[] { orderB }), "Issue16 order history remains isolated after switching accounts");
            await ExpectStatusAsync(() => new CommerceService(store, crypto, new MemberSecurity(null, ""), TimeProvider.System).Orders(NewRequest("GET", "/orders")), 401, "Issue16 anonymous request cannot enumerate orders");

        }
        finally
        {
            await using var connection = new SqlConnection(adminConnectionString);
            await connection.OpenAsync();
            await using var cleanup = new SqlCommand("""
                DELETE commerce.Idempotency WHERE Dataset='kohami_local' AND Actor IN(@a,@b);
                DELETE commerce.Idempotency WHERE Dataset='kohami_local' AND Actor=@browser;
                DELETE commerce.Reservations WHERE Dataset='kohami_local' AND OrderId IN(@memberCheckoutOrder,@walletCheckoutOrder);
                DELETE commerce.RefundUnits WHERE Dataset='kohami_local' AND OrderId=@walletCheckoutOrder;
                DELETE commerce.Collections WHERE Dataset='kohami_local' AND OrderId=@walletCheckoutOrder;
                DELETE commerce.Documents WHERE Dataset='kohami_local' AND ((Kind='Order' AND Id IN(@orderA,@orderB,@guestOrder,@memberCheckoutOrder,@walletCheckoutOrder)) OR (Kind='Checkout' AND Id IN(@memberCheckout,@walletCheckout)) OR (Kind='Quote' AND Id IN(@memberCheckoutQuote,@walletCheckoutQuote)) OR (Kind='Cart' AND Id IN(@cart,@walletCart)) OR (Kind='WalletPayment' AND JSON_VALUE(Body,'$.orderId')=@walletCheckoutOrder));
                DELETE commerce.WalletLedger WHERE Dataset='kohami_local' AND UserId=@a;
                DELETE commerce.WalletAccounts WHERE Dataset='kohami_local' AND UserId=@a;
                DELETE identity_access.SecurityAudit WHERE UserId=@a AND Action='SyntheticWalletCredit';
                IF @stock IS NOT NULL UPDATE commerce.Inventory SET Stock=@stock WHERE Dataset='kohami_local' AND Sku='sku_demo_01';
                DELETE identity_access.Users WHERE UserId IN(@a,@b);
                """, connection);
            cleanup.Parameters.AddWithValue("@a", memberA);
            cleanup.Parameters.AddWithValue("@b", memberB);
            cleanup.Parameters.AddWithValue("@orderA", orderA);
            cleanup.Parameters.AddWithValue("@orderB", orderB);
            cleanup.Parameters.AddWithValue("@guestOrder", guestOrder);
            cleanup.Parameters.AddWithValue("@browser", browser);
            cleanup.Parameters.AddWithValue("@memberCheckout", (object?)memberCheckoutId ?? DBNull.Value);
            cleanup.Parameters.AddWithValue("@memberCheckoutOrder", (object?)memberCheckoutOrderId ?? DBNull.Value);
            cleanup.Parameters.AddWithValue("@memberCheckoutQuote", (object?)memberCheckoutQuoteId ?? DBNull.Value);
            cleanup.Parameters.AddWithValue("@cart", checkoutCart);
            cleanup.Parameters.AddWithValue("@walletCheckout", (object?)walletCheckoutId ?? DBNull.Value);
            cleanup.Parameters.AddWithValue("@walletCheckoutOrder", (object?)walletCheckoutOrderId ?? DBNull.Value);
            cleanup.Parameters.AddWithValue("@walletCheckoutQuote", (object?)walletCheckoutQuoteId ?? DBNull.Value);
            cleanup.Parameters.AddWithValue("@walletCart", walletCart);
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

    static void AddProtectedCookie(HttpContext context, SessionCrypto crypto, string name, string value)
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
        catch (KohamiException error) when (error.Status == status) { Check(true, name); }
    }

    static void Check(bool pass, string name)
    {
        if (!pass) throw new InvalidOperationException("FAILED: " + name);
        Console.WriteLine("PASS: " + name);
    }

    sealed class MemberSecurity(string? userId, string email) : ISecurity
    {
        public Task<Principal?> ResolveAsync(HttpContext context, CancellationToken ct = default) =>
            Task.FromResult(userId is null ? null : new Principal(userId, "issue16-session", 1, "MemberAuthenticated", Array.Empty<string>(), email));
        public Task<Principal> RequirePermissionAsync(HttpContext context, string permission, string resourceKind, string resourceId, CancellationToken ct = default) =>
            Task.FromException<Principal>(new KohamiException(403, "PERMISSION_CHANGED"));
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
