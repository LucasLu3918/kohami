using System.Text.Json.Nodes;

namespace Kohami.Catalog.Domain;

public static class CatalogDraftRules
{
    private static readonly HashSet<string> Locales = ["zh-TW", "ja", "en"];

    public static JsonObject Validate(JsonObject source)
    {
        Fields(source, "slug", "translations", "skus", "media", "measurements");
        var draft = (JsonObject)source.DeepClone();
        var slug = Text(draft, "slug", 1, 100);
        Identifier(slug);

        var translations = Array(draft, "translations", 3, 3);
        var locales = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in translations)
        {
            var item = Object(node);
            Fields(item, "locale", "name", "description", "material");
            var locale = Text(item, "locale", 1, 10);
            if (!Locales.Contains(locale) || !locales.Add(locale)) Invalid();
            Text(item, "name", 1, 200);
            Text(item, "description", 1, 2000);
            Text(item, "material", 1, 500);
        }
        if (!locales.SetEquals(Locales)) Invalid();

        var skus = Array(draft, "skus", 1, 100);
        var skuIds = new HashSet<string>(StringComparer.Ordinal);
        var variants = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in skus)
        {
            var item = Object(node);
            Fields(item, "id", "colorId", "garmentSize", "unitPriceTwd", "active");
            var id = Text(item, "id", 1, 100);
            var color = Text(item, "colorId", 1, 80);
            var size = Text(item, "garmentSize", 1, 40);
            Identifier(id); Identifier(color); Identifier(size);
            if (!skuIds.Add(id) || !variants.Add(color + "\0" + size)) Invalid();
            if (Number(item, "unitPriceTwd") is < 1 or > 9_000_000_000_000_000L) Invalid();
            if (item["active"] is not JsonValue active || !active.TryGetValue<bool>(out _)) Invalid();
        }

        var media = Array(draft, "media", 1, 8);
        foreach (var node in media)
        {
            var item = Object(node);
            Fields(item, "media", "sourceRightsRef");
            var value = Object(item["media"]);
            Fields(value, "id", "url", "alt", "synthetic", "revision");
            Identifier(Text(value, "id", 1, 100));
            var url = Text(value, "url", 1, 200);
            var rightsRef = Text(item, "sourceRightsRef", 1, 100);
            var generatedImageUrl = $"/demo-assets/products/{slug}.png";
            if (!((url == "/demo-assets/neutral.svg" && rightsRef == "synthetic_owned_demo") ||
                  (url == generatedImageUrl && rightsRef == "synthetic_ai_generated_demo"))) Invalid();
            if (value["synthetic"] is not JsonValue synthetic || !synthetic.TryGetValue<bool>(out var isSynthetic) || !isSynthetic) Invalid();
            Text(value, "revision", 1, 100);
            var alt = Object(value["alt"]);
            Fields(alt, "value", "requestedLocale", "sourceLocale", "revision", "fallback");
            Text(alt, "value", 1, 500);
            if (!Locales.Contains(Text(alt, "requestedLocale", 1, 10)) || !Locales.Contains(Text(alt, "sourceLocale", 1, 10))) Invalid();
            Text(alt, "revision", 1, 100);
            if (alt["fallback"] is not JsonValue fallback || !fallback.TryGetValue<bool>(out _)) Invalid();
        }

        var measurements = Array(draft, "measurements", 1, 100);
        var measurementNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in measurements)
        {
            var item = Object(node);
            Fields(item, "name", "valueMm", "synthetic");
            var name = Text(item, "name", 1, 80);
            if (!measurementNames.Add(name) || Number(item, "valueMm") is < 1 or > 10000) Invalid();
            if (item["synthetic"] is not JsonValue synthetic || !synthetic.TryGetValue<bool>(out var isSynthetic) || !isSynthetic) Invalid();
        }
        return draft;
    }

    static void Fields(JsonObject value, params string[] names)
    {
        var allowed = names.ToHashSet(StringComparer.Ordinal);
        if (value.Any(pair => !allowed.Contains(pair.Key)) || names.Any(name => !value.ContainsKey(name))) Invalid();
    }

    static JsonArray Array(JsonObject value, string name, int min, int max)
    {
        if (value[name] is not JsonArray array || array.Count < min || array.Count > max) Invalid();
        return (JsonArray)value[name]!;
    }

    static JsonObject Object(JsonNode? value) => value as JsonObject ?? throw new CatalogValidationException();
    static string Text(JsonObject value, string name, int min, int max)
    {
        var text = value[name]?.GetValue<string>();
        if (text is null || text.Length < min || text.Length > max || text.Trim().Length == 0) Invalid();
        return text!.Trim();
    }
    static long Number(JsonObject value, string name)
    {
        if (value[name] is not JsonValue number || !number.TryGetValue<long>(out var result)) Invalid();
        return ((JsonValue)value[name]!).GetValue<long>();
    }
    static void Identifier(string value)
    {
        if (value.Length > 100 || value.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '_' or '-'))) Invalid();
    }
    static void Invalid() => throw new CatalogValidationException();
}

public sealed class CatalogValidationException() : Exception("VALIDATION_ERROR");
