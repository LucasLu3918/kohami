using Kohami.M0.Core;
namespace Kohami.M0.Security;
public static class SecurityRules
{
    public static string ETag(string aggregate, string id, long version) => '"' + aggregate + "_" + id + "_" + Version(version) + '"';
    public static void Match(Microsoft.AspNetCore.Http.HttpContext context, string aggregate, string id, long version)
    {
        var value = context.Request.Headers.IfMatch.ToString();
        if (value.Length == 0) throw new M0Exception(428, "PRECONDITION_REQUIRED");
        if (value != ETag(aggregate, id, version)) throw new M0Exception(412, "VERSION_CHANGED");
    }
    public static IReadOnlyList<string> CorePermissions { get; } = Array.AsReadOnly(new[] { "access.read", "access.manage", "user.read", "user.manage", "orders.read", "refund.request", "refund.execute", "refund.check", "shipment.manage", "return.inspect", "catalog.write", "stock.adjust", "shipping.configure", "audit.read", "reconciliation.read", "reconciliation.run", "reconciliation.check", "reconciliation.resolve", "reports.read", "support.respond", "data.handle", "demo.control", "demo.reset" });
    public static bool IsCorePermission(string permission) => CorePermissions.Contains(permission, StringComparer.Ordinal);
    public static bool RoleAllows(string role, string permission, string scopeKind) => role switch
    {
        "Owner" => IsCorePermission(permission) && new[] { "Dataset", "User", "Product", "Sku", "Order", "Case", "Report" }.Contains(scopeKind),
        "Merchandiser" => permission == "catalog.write" && scopeKind is "Product" or "Sku" || permission == "stock.adjust" && scopeKind == "Sku",
        "CustomerSupport" => new[] { "orders.read", "refund.request", "refund.check", "shipment.manage", "return.inspect", "support.respond", "reconciliation.read" }.Contains(permission) && scopeKind is "Order" or "Case",
        "Analyst" => permission == "reports.read" && scopeKind == "Report",
        _ => false
    };
    public static string PurposeKind(string purpose) => purpose switch { "refund.execute" => "RefundCase", "access.manage" or "email.change" or "user.manage" => "User", "demo.reset" or "shipping.configure" or "demo.control" => "Dataset", _ => throw new M0Exception(422, "VALIDATION_ERROR") };
    public static bool ScopeMatches(string scopeKind, string scopeId, string kind, string id, string dataset) => scopeKind == "Dataset" ? scopeId == dataset : (scopeKind == kind || scopeKind == "Case" && kind == "RefundCase") && scopeId == id;
    public static string Version(long value) => "v" + value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    public static void Nonce(string value) { if (value.Length is < 16 or > 160) throw new M0Exception(422, "VALIDATION_ERROR"); }
    public static void Identifier(string value) { if (value.Length is < 1 or > 100 || value.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '_' or '-'))) throw new M0Exception(422, "VALIDATION_ERROR"); }
    public static string Email(string value) { if (value.Length > 254 || !value.EndsWith("@example.test", StringComparison.Ordinal) || value.Count(c => c == '@') != 1 || value.Any(char.IsWhiteSpace)) throw new M0Exception(422, "VALIDATION_ERROR"); return value.ToLowerInvariant(); }
    public static void Password(string value) { if (value.Length is < 15 or > 256 || new[] { "passwordpassword", "123456789012345", "kohamikohamikohami" }.Contains(value.ToLowerInvariant())) throw new M0Exception(422, "VALIDATION_ERROR"); }
}
