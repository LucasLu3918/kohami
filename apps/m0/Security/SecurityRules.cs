using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Kohami.M0.Core;
namespace Kohami.M0.Security;

public static class Totp
{
    const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
    public static string Encode(byte[] bytes) { var result = new StringBuilder(); int buffer = 0, bits = 0; foreach (var value in bytes) { buffer = (buffer << 8) | value; bits += 8; while (bits >= 5) { bits -= 5; result.Append(Alphabet[(buffer >> bits) & 31]); } } if (bits > 0) result.Append(Alphabet[(buffer << (5 - bits)) & 31]); return result.ToString(); }
    public static byte[] Decode(string value) { var result = new List<byte>(); int buffer = 0, bits = 0; foreach (var c in value) { var n = Alphabet.IndexOf(c); if (n < 0) throw new FormatException("BASE32_INVALID"); buffer = (buffer << 5) | n; bits += 5; if (bits >= 8) { bits -= 8; result.Add((byte)(buffer >> bits)); } } return result.ToArray(); }
    public static string Code(byte[] seed, long step) { Span<byte> counter = stackalloc byte[8]; BinaryPrimitives.WriteInt64BigEndian(counter, step); var digest = HMACSHA1.HashData(seed, counter); var offset = digest[^1] & 15; var n = BinaryPrimitives.ReadUInt32BigEndian(digest.AsSpan(offset, 4)) & 0x7fffffff; return (n % 1000000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture); }
    public static long? Match(byte[] seed, string code, DateTimeOffset now, long lastAccepted) { if (code.Length != 6 || code.Any(c => c is < '0' or > '9')) return null; var step = now.ToUnixTimeSeconds() / 30; foreach (var match in new[] { step, step - 1, step + 1 }) if (match > lastAccepted && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(code), Encoding.ASCII.GetBytes(Code(seed, match)))) return match; return null; }
}
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
    public static string PurposeKind(string purpose) => purpose switch { "refund.execute" => "RefundCase", "access.manage" or "email.change" or "user.manage" => "User", "demo.reset" or "shipping.configure" or "demo.control" => "Dataset", "authenticator.replace" or "recovery.rotate" => "Credential", _ => throw new M0Exception(422, "VALIDATION_ERROR") };
    public static bool ScopeMatches(string scopeKind, string scopeId, string kind, string id, string dataset) => scopeKind == "Dataset" ? scopeId == dataset : (scopeKind == kind || scopeKind == "Case" && kind == "RefundCase") && scopeId == id;
    public static string Version(long value) => "v" + value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    public static void Nonce(string value) { if (value.Length is < 16 or > 160) throw new M0Exception(422, "VALIDATION_ERROR"); }
    public static void Identifier(string value) { if (value.Length is < 1 or > 100 || value.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '_' or '-'))) throw new M0Exception(422, "VALIDATION_ERROR"); }
    public static string Email(string value) { if (value.Length > 254 || !value.EndsWith("@example.test", StringComparison.Ordinal) || value.Count(c => c == '@') != 1 || value.Any(char.IsWhiteSpace)) throw new M0Exception(422, "VALIDATION_ERROR"); return value.ToLowerInvariant(); }
    public static void Password(string value) { if (value.Length is < 15 or > 256 || new[] { "passwordpassword", "123456789012345", "kohamikohamikohami" }.Contains(value.ToLowerInvariant())) throw new M0Exception(422, "VALIDATION_ERROR"); }
}
