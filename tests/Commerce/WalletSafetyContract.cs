using Kohami.Commerce;

internal static class WalletSafetyContract
{
    public static void Run(string root)
    {
        var migration = File.ReadAllText(Path.Combine(root, "src/Modules/Commerce/Infrastructure/Persistence/MemberWalletMigration.sql"));
        var service = File.ReadAllText(Path.Combine(root, "src/Modules/Commerce/Infrastructure/Legacy/CommerceService.cs"));
        var store = File.ReadAllText(Path.Combine(root, "src/Modules/Commerce/Infrastructure/Persistence/Store.cs"));
        var refund = File.ReadAllText(Path.Combine(root, "src/Modules/Commerce/Infrastructure/Legacy/Aftercare.cs"));
        var routes = File.ReadAllText(Path.Combine(root, "src/Modules/Commerce/Presentation/CommerceEndpoints.cs"));
        var runtimeContract = File.ReadAllText(Path.Combine(root, "docs/contracts/runtime/Core.openapi.json"));

        Require(migration.Contains("IF @@TRANCOUNT=0 THROW 51000,'WALLET_TRANSACTION_REQUIRED'", StringComparison.Ordinal), "debit requires caller SQL transaction");
        Require(migration.Contains("AvailableTwd>=@AmountTwd", StringComparison.Ordinal), "debit condition prevents negative balance");
        Require(migration.IndexOf("UPDATE commerce.WalletAccounts", StringComparison.Ordinal) < migration.IndexOf("INSERT commerce.WalletLedger", StringComparison.Ordinal), "ledger entry follows successful conditional debit");
        Require(migration.Contains("GRANT SELECT ON OBJECT::commerce.WalletAccounts TO kohami_api", StringComparison.Ordinal) && migration.Contains("GRANT EXECUTE ON OBJECT::commerce.wallet_debit TO kohami_api", StringComparison.Ordinal), "API gets wallet read and debit capability");
        Require(!migration.Contains("GRANT INSERT ON OBJECT::commerce.WalletLedger TO kohami_api", StringComparison.Ordinal) && !migration.Contains("GRANT UPDATE ON OBJECT::commerce.WalletAccounts TO kohami_api", StringComparison.Ordinal), "API cannot create credits or edit balance directly");
        Require(migration.Contains("PROCEDURE commerce.wallet_synthetic_credit", StringComparison.Ordinal), "dedicated test funding uses a separate SQL procedure");
        Require(migration.Contains("ops.DevelopmentFixtureMarker", StringComparison.Ordinal) && migration.Contains("wallet_synthetic_credit", StringComparison.Ordinal), "synthetic credit requires the marked dedicated fixture");
        Require(migration.Contains("@SourceId NOT LIKE 'synthetic-test-credit:%'", StringComparison.Ordinal) && migration.Contains("WALLET_SYNTHETIC_CREDIT_REPLAY", StringComparison.Ordinal), "synthetic credit source is explicit and replay-safe");
        Require(migration.Contains("GRANT EXECUTE ON OBJECT::commerce.wallet_synthetic_credit TO kohami_migrator", StringComparison.Ordinal) && !migration.Contains("GRANT EXECUTE ON OBJECT::commerce.wallet_synthetic_credit TO kohami_api", StringComparison.Ordinal), "only the isolated fixture migrator can create test credits");
        Require(File.ReadAllText(Path.Combine(root, "tests/Commerce/WalletFixtureCredit.cs")).Contains("localhost,14349", StringComparison.Ordinal), "test credit runner accepts only the exact kohami-sql endpoint");
        Require(routes.Contains("/wallet/current", StringComparison.Ordinal) && !routes.Contains("/wallet/fund", StringComparison.Ordinal) && !routes.Contains("/wallet/topup", StringComparison.OrdinalIgnoreCase), "wallet exposes no funding endpoint");
        var checkoutStart = service.IndexOf("public async Task<IResult> Checkout", StringComparison.Ordinal);
        var payStart = service.IndexOf("public async Task<IResult> Pay", checkoutStart, StringComparison.Ordinal);
        if (checkoutStart < 0 || payStart < 0) throw new InvalidOperationException("FAILED: wallet safety contract: checkout/pay handlers are missing");
        var checkoutHandler = service[checkoutStart..payStart]; var payHandler = service[payStart..];
        Require(checkoutHandler.Contains("commerce.wallet_debit", StringComparison.Ordinal) && checkoutHandler.Contains("WALLET_INSUFFICIENT_BALANCE", StringComparison.Ordinal) && checkoutHandler.Contains("walletEntryId", StringComparison.Ordinal), "wallet debit and order writes share the checkout transaction");
        Require(!payHandler.Contains("commerce.wallet_debit", StringComparison.Ordinal), "wallet debit cannot bypass the atomic checkout API");
        Require(store.Contains("IsolationLevel.Serializable", StringComparison.Ordinal) && store.Contains("kohami.identity_access.security", StringComparison.Ordinal) && store.Contains("kohami.identity_access.commerce.kohami_local", StringComparison.Ordinal), "wallet checkout shares serialized commerce transaction boundary");
        Require(refund.Contains("WALLET_REFUND_UNSUPPORTED", StringComparison.Ordinal), "wallet-funded order cannot use external-mock refund flow");
        Require(CommerceEndpoints.UsesModuleContract("GET", "/api/v1/wallet/current"), "wallet route is classified as a module contract");
        Require(runtimeContract.Contains("\"paymentMethod\": {\n            \"const\": \"wallet\"", StringComparison.Ordinal) && runtimeContract.Contains("WALLET_INSUFFICIENT_BALANCE", StringComparison.Ordinal), "runtime checkout contract declares the optional wallet method and insufficient-balance error");
        var contract = new Kohami.Core.RuntimeContract(Path.Combine(root, "docs/contracts/runtime/Core.openapi.json"));
        var context = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/api/v1/checkouts";
        context.Response.StatusCode = 201;
        context.Response.ContentType = "application/json";
        using var runtimeDocument = System.Text.Json.JsonDocument.Parse(runtimeContract);
        var checkoutOperation = runtimeDocument.RootElement.GetProperty("paths").GetProperty("/checkouts").GetProperty("post");
        var allowsMemberSession = checkoutOperation.GetProperty("security").EnumerateArray()
            .Any(scheme => scheme.TryGetProperty("SessionCookie", out _));
        var walletAuthorization = checkoutOperation.GetProperty("x-canonical-policy").GetProperty("sourceAuthorization");
        var checkoutRequestSchema = runtimeDocument.RootElement.GetProperty("components").GetProperty("schemas").GetProperty("CommerceCheckoutRequest");
        var paymentIsOptional = checkoutRequestSchema.GetProperty("properties").TryGetProperty("paymentMethod", out _)
            && !checkoutRequestSchema.GetProperty("required").EnumerateArray().Any(field => field.GetString() == "paymentMethod");
        Require(allowsMemberSession && walletAuthorization.GetProperty("walletPayment").GetString() == "optional-member-owner-only"
            && paymentIsOptional, "runtime checkout contract binds the optional wallet method to member-session authorization");
        var response = runtimeDocument.RootElement.GetProperty("components").GetProperty("schemas").GetProperty("CommerceCheckoutCreated").GetProperty("examples")[0];
        Require(contract.ValidResponse(context, response), "runtime validator accepts the existing checkout response shape used by atomic wallet checkout");
        Console.WriteLine("PASS wallet safety contracts: atomic checkout debit, no funding API, no direct balance write, refund guard");
    }

    private static void Require(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("FAILED: wallet safety contract: " + name);
    }
}
