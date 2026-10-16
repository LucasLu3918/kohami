using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Kohami.Catalog.Domain;

public sealed record CatalogSearchQuery(
    string? SearchText,
    string Locale,
    string? ColorId,
    string? GarmentSize,
    bool InStock,
    string Sort,
    int Limit,
    int Offset)
{
    static readonly HashSet<string> Allowed = ["q", "locale", "colorId", "garmentSize", "availability", "sort", "limit", "cursor"];
    static readonly HashSet<string> Locales = ["zh-TW", "ja", "en"];

    public static CatalogSearchQuery Parse(IEnumerable<KeyValuePair<string, string?>> values)
    {
        var query = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in values)
        {
            if (!Allowed.Contains(key) || value is null || !query.TryAdd(key, value)) Invalid();
        }

        var q = query.GetValueOrDefault("q")?.Normalize(NormalizationForm.FormKC).Trim();
        if (q?.Length > 100 || q?.Any(char.IsControl) == true) Invalid();
        if (q?.Length == 0) q = null;
        var locale = query.GetValueOrDefault("locale") ?? "zh-TW";
        if (!Locales.Contains(locale)) Invalid();
        var color = OptionalIdentifier(query.GetValueOrDefault("colorId"), 80);
        var size = OptionalIdentifier(query.GetValueOrDefault("garmentSize"), 40);
        var availability = query.GetValueOrDefault("availability");
        if (availability is not null and not "all" and not "in_stock") Invalid();
        var sort = query.GetValueOrDefault("sort") ?? "name";
        if (sort is not "name" and not "price") Invalid();
        var limit = 20;
        if (query.TryGetValue("limit", out var rawLimit) && (!int.TryParse(rawLimit, out limit) || limit is < 1 or > 100)) Invalid();

        var parsed = new CatalogSearchQuery(q, locale, color, size, availability == "in_stock", sort, limit, 0);
        var offset = query.TryGetValue("cursor", out var cursor) ? DecodeCursor(cursor, parsed.Fingerprint()) : 0;
        return parsed with { Offset = offset };
    }

    public string? NextCursor(int offset) => EncodeCursor(offset, Fingerprint());

    public string SearchPattern => SearchText is null ? "" : "%" + SearchText.Replace("~", "~~").Replace("%", "~%").Replace("_", "~_").Replace("[", "~[") + "%";

    string Fingerprint() => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
        $"{SearchText}\0{Locale}\0{ColorId}\0{GarmentSize}\0{InStock}\0{Sort}")))[..24];

    static string EncodeCursor(int offset, string fingerprint) => Convert.ToBase64String(Encoding.UTF8.GetBytes($"{offset}:{fingerprint}"))
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    static int DecodeCursor(string cursor, string fingerprint)
    {
        try
        {
            var base64 = cursor.Replace('-', '+').Replace('_', '/');
            base64 += new string('=', (4 - base64.Length % 4) % 4);
            var parts = Encoding.UTF8.GetString(Convert.FromBase64String(base64)).Split(':', 2);
            if (parts.Length != 2 || parts[1] != fingerprint || !int.TryParse(parts[0], out var offset) || offset is < 0 or > 1_000_000)
                throw new CatalogSearchValidationException();
            return offset;
        }
        catch (FormatException) { Invalid(); return 0; }
    }

    static string? OptionalIdentifier(string? value, int max)
    {
        if (value is null) return null;
        if (value.Length is < 1 || value.Length > max || value.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '_' or '-'))) Invalid();
        return value;
    }

    static void Invalid() => throw new CatalogSearchValidationException();
}

public sealed class CatalogSearchValidationException() : Exception("VALIDATION_ERROR");
