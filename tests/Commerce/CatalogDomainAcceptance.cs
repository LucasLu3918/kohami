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
