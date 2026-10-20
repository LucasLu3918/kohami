using Kohami.M0.Commerce;

internal static class WalletSafetyContract
{
    public static void Run(string root)
    {
        var migration = File.ReadAllText(Path.Combine(root, "src/Modules/Commerce/Infrastructure/Persistence/MemberWalletMigration.sql"));
        var service = File.ReadAllText(Path.Combine(root, "src/Modules/Commerce/Infrastructure/Legacy/CommerceService.cs"));
        var store = File.ReadAllText(Path.Combine(root, "src/Modules/Commerce/Infrastructure/Persistence/Store.cs"));
        var refund = File.ReadAllText(Path.Combine(root, "src/Modules/Commerce/Infrastructure/Legacy/Aftercare.cs"));
        var routes = File.ReadAllText(Path.Combine(root, "src/Modules/Commerce/Presentation/CommerceEndpoints.cs"));

        Require(migration.Contains("IF @@TRANCOUNT=0 THROW 51000,'WALLET_TRANSACTION_REQUIRED'", StringComparison.Ordinal), "debit requires caller SQL transaction");
        Require(migration.Contains("AvailableTwd>=@AmountTwd", StringComparison.Ordinal), "debit condition prevents negative balance");
        Require(migration.IndexOf("UPDATE m0commerce.WalletAccounts", StringComparison.Ordinal) < migration.IndexOf("INSERT m0commerce.WalletLedger", StringComparison.Ordinal), "ledger entry follows successful conditional debit");
        Require(migration.Contains("GRANT SELECT ON OBJECT::m0commerce.WalletAccounts TO m0_api", StringComparison.Ordinal) && migration.Contains("GRANT EXECUTE ON OBJECT::m0commerce.wallet_debit TO m0_api", StringComparison.Ordinal), "API gets wallet read and debit capability");
        Require(!migration.Contains("GRANT INSERT ON OBJECT::m0commerce.WalletLedger TO m0_api", StringComparison.Ordinal) && !migration.Contains("GRANT UPDATE ON OBJECT::m0commerce.WalletAccounts TO m0_api", StringComparison.Ordinal), "API cannot create credits or edit balance directly");
        Require(migration.Contains("PROCEDURE m0commerce.wallet_synthetic_credit", StringComparison.Ordinal), "dedicated test funding uses a separate SQL procedure");
        Require(migration.Contains("ops.DevelopmentFixtureMarker", StringComparison.Ordinal) && migration.Contains("wallet_synthetic_credit", StringComparison.Ordinal), "synthetic credit requires the marked dedicated fixture");
        Require(migration.Contains("@SourceId NOT LIKE 'synthetic-test-credit:%'", StringComparison.Ordinal) && migration.Contains("WALLET_SYNTHETIC_CREDIT_REPLAY", StringComparison.Ordinal), "synthetic credit source is explicit and replay-safe");
        Require(migration.Contains("GRANT EXECUTE ON OBJECT::m0commerce.wallet_synthetic_credit TO m0_migrator", StringComparison.Ordinal) && !migration.Contains("GRANT EXECUTE ON OBJECT::m0commerce.wallet_synthetic_credit TO m0_api", StringComparison.Ordinal), "only the isolated fixture migrator can create test credits");
        Require(File.ReadAllText(Path.Combine(root, "tests/Commerce/WalletFixtureCredit.cs")).Contains("localhost,14349", StringComparison.Ordinal), "test credit runner accepts only the exact kohami-sql endpoint");
        Require(routes.Contains("/wallet/current", StringComparison.Ordinal) && !routes.Contains("/wallet/fund", StringComparison.Ordinal) && !routes.Contains("/wallet/topup", StringComparison.OrdinalIgnoreCase), "wallet exposes no funding endpoint");
        Require(service.Contains("m0commerce.wallet_debit", StringComparison.Ordinal) && service.Contains("WALLET_INSUFFICIENT_BALANCE", StringComparison.Ordinal), "member payment uses server-side debit and fails on insufficient balance");
        Require(store.Contains("IsolationLevel.Serializable", StringComparison.Ordinal) && store.Contains("kohami.m0.security", StringComparison.Ordinal) && store.Contains("kohami.m0.commerce.demo_m0", StringComparison.Ordinal), "wallet payment shares serialized commerce transaction boundary");
        Require(refund.Contains("WALLET_REFUND_UNSUPPORTED", StringComparison.Ordinal), "wallet-funded order cannot use external-mock refund flow");
        Require(CommerceEndpoints.UsesModuleContract("GET", "/api/v1/wallet/current"), "wallet route is classified as a module contract");
        Console.WriteLine("PASS wallet safety contracts: atomic debit, no funding API, no direct balance write, refund guard");
    }

    private static void Require(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("FAILED: wallet safety contract: " + name);
    }
}
