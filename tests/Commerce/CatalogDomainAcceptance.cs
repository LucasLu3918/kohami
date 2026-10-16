using System.Text.Json.Nodes;
using Kohami.Catalog.Domain;

namespace Kohami.Catalog.Tests;

public static class CatalogDomainAcceptance
{
    public static void Run()
    {
        var valid = Draft();
        var normalized = CatalogDraftRules.Validate(valid);
        Check(normalized["slug"]!.GetValue<string>() == "demo-top", "valid synthetic catalog draft accepted");

        var duplicateSku = Draft();
        duplicateSku["skus"]!.AsArray().Add(duplicateSku["skus"]![0]!.DeepClone());
        Reject(duplicateSku, "duplicate sku rejected");

        var duplicateVariant = Draft();
        duplicateVariant["skus"]!.AsArray()[1]!["id"] = "sku_demo_03";
        duplicateVariant["skus"]!.AsArray()[1]!["colorId"] = "cream";
        duplicateVariant["skus"]!.AsArray()[1]!["garmentSize"] = "M";
        Reject(duplicateVariant, "duplicate color and garment size rejected");

        var missingLocale = Draft();
        missingLocale["translations"]!.AsArray().RemoveAt(2);
        Reject(missingLocale, "incomplete locale set rejected");

        var remoteMedia = Draft();
        remoteMedia["media"]![0]!["media"]!["url"] = "https://example.test/image.png";
        Reject(remoteMedia, "remote media rejected");

        var nonSyntheticMedia = Draft();
        nonSyntheticMedia["media"]![0]!["media"]!["synthetic"] = false;
        Reject(nonSyntheticMedia, "non-synthetic media rejected");

        var zeroPrice = Draft();
        zeroPrice["skus"]![0]!["unitPriceTwd"] = 0;
        Reject(zeroPrice, "zero price rejected");

        SearchQueryRules();
    }

    static void SearchQueryRules()
    {
        var normalized = CatalogSearchQuery.Parse(new Dictionary<string, string?> { ["q"] = "  ＴＯＰ  ", ["colorId"] = "cream", ["garmentSize"] = "M", ["availability"] = "in_stock" });
        Check(normalized.SearchText == "TOP" && normalized.ColorId == "cream" && normalized.GarmentSize == "M" && normalized.InStock, "catalog query normalizes text and accepts same-SKU filters");
        Check(CatalogSearchQuery.Parse([]).Limit == 20, "catalog query defaults to first 20 results");
        Check(CatalogSearchQuery.Parse(new Dictionary<string, string?> { ["limit"] = "100" }).Limit == 100, "catalog query allows up to 100 results");
        RejectQuery(new Dictionary<string, string?> { ["category"] = "tops" }, "unsupported catalog filter rejected");
        RejectQuery(new Dictionary<string, string?> { ["availability"] = "reserved" }, "unknown availability rejected");
        RejectQuery(new Dictionary<string, string?> { ["q"] = new string('x', 101) }, "catalog query length bounded");
        var page = CatalogSearchQuery.Parse(new Dictionary<string, string?> { ["q"] = "top", ["limit"] = "10" });
        var next = page.NextCursor(10)!;
        Check(CatalogSearchQuery.Parse(new Dictionary<string, string?> { ["q"] = "top", ["limit"] = "10", ["cursor"] = next }).Offset == 10, "catalog cursor resumes its matching query");
        RejectQuery(new Dictionary<string, string?> { ["q"] = "dress", ["cursor"] = next }, "catalog cursor cannot be reused for another query");
    }

    static void RejectQuery(Dictionary<string, string?> query, string name)
    {
        try { CatalogSearchQuery.Parse(query); throw new InvalidOperationException("FAILED: " + name); }
        catch (CatalogSearchValidationException) { Console.WriteLine("PASS: " + name); }
    }

    static JsonObject Draft() => JsonNode.Parse("""
    {
      "slug":"demo-top",
      "translations":[
        {"locale":"zh-TW","name":"示意上衣","description":"合成測試資料。","material":"示意棉料"},
        {"locale":"ja","name":"デモトップス","description":"合成データです。","material":"デモ素材"},
        {"locale":"en","name":"Demo top","description":"Synthetic fixture.","material":"Demo fabric"}
      ],
      "skus":[
        {"id":"sku_demo_01","colorId":"cream","garmentSize":"M","unitPriceTwd":500,"active":true},
        {"id":"sku_demo_02","colorId":"cream","garmentSize":"L","unitPriceTwd":500,"active":true}
      ],
      "media":[{"media":{"id":"media_demo_01","url":"/demo-assets/neutral.svg","alt":{"value":"示意占位圖，非實物照片","requestedLocale":"zh-TW","sourceLocale":"zh-TW","revision":"alt_v1","fallback":false},"synthetic":true,"revision":"media_v1"},"sourceRightsRef":"synthetic_owned_demo"}],
      "measurements":[{"name":"chestWidth","valueMm":520,"synthetic":true}]
    }
    """)!.AsObject();

    static void Reject(JsonObject source, string name)
    {
        try { CatalogDraftRules.Validate(source); throw new InvalidOperationException("FAILED: " + name); }
        catch (CatalogValidationException) { Console.WriteLine("PASS: " + name); }
    }

    static void Check(bool pass, string name)
    {
        if (!pass) throw new InvalidOperationException("FAILED: " + name);
        Console.WriteLine("PASS: " + name);
    }
}
