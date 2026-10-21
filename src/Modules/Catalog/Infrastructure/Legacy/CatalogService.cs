using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Kohami.Catalog.Domain;
using Kohami.M0.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;

namespace Kohami.Catalog.Infrastructure;

public sealed class CatalogService(M0Sql sql, ICatalogOwnerCreationAuthorization ownerAuthorization, IM0Security security, TimeProvider clock)
{
    const string Dataset = "demo_m0";
    static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    static string Id(string prefix) => prefix + "_" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(12));
    static string Json(JsonNode value) => value.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    static JsonObject Parse(string value) => JsonNode.Parse(value)!.AsObject();

    public async Task<IResult> Products(HttpContext h)
    {
        CatalogSearchQuery query;
        try
        {
            query = CatalogSearchQuery.Parse(h.Request.Query.Select(pair =>
                new KeyValuePair<string, string?>(pair.Key, pair.Value.Count == 1 ? pair.Value.ToString() : null)));
        }
        catch (CatalogSearchValidationException) { throw new M0Exception(422, "VALIDATION_ERROR"); }

        await using var c = await sql.OpenAsync(h.RequestAborted);
        const string sqlText = """
            SELECT p.PublicJson FROM m0.CatalogProducts p
            WHERE p.Dataset=@d AND p.State='Published'
              AND (@q IS NULL OR EXISTS (
                    SELECT 1 FROM OPENJSON(p.PublicJson, '$.translations') WITH(locale nvarchar(10) '$.locale', name nvarchar(200) '$.name', description nvarchar(2000) '$.description') t
                    WHERE t.locale=@locale AND (t.name LIKE @pattern ESCAPE '~' OR t.description LIKE @pattern ESCAPE '~'))
                  OR EXISTS (SELECT 1 FROM m0.CatalogProductSkus sq WHERE sq.Dataset=p.Dataset AND sq.ProductId=p.ProductId AND sq.PublicIncluded=1 AND sq.PublicActive=1 AND sq.SkuId LIKE @pattern ESCAPE '~'))
              AND EXISTS (
                    SELECT 1 FROM m0.CatalogProductSkus s JOIN m0commerce.Inventory i ON i.Dataset=s.Dataset AND i.Sku=s.SkuId
                    WHERE s.Dataset=p.Dataset AND s.ProductId=p.ProductId AND s.PublicIncluded=1 AND s.PublicActive=1 AND i.Active=1
                      AND (@color IS NULL OR s.ColorId=@color) AND (@size IS NULL OR s.GarmentSize=@size)
                      AND (@inStock=0 OR i.Stock>0))
            ORDER BY
              CASE WHEN @sort='name' THEN COALESCE((SELECT TOP(1) t.name FROM OPENJSON(p.PublicJson, '$.translations') WITH(locale nvarchar(10) '$.locale', name nvarchar(200) '$.name') t WHERE t.locale=@locale), p.PublicSlug) END,
              CASE WHEN @sort='price' THEN (SELECT MIN(i.Price) FROM m0.CatalogProductSkus s JOIN m0commerce.Inventory i ON i.Dataset=s.Dataset AND i.Sku=s.SkuId WHERE s.Dataset=p.Dataset AND s.ProductId=p.ProductId AND s.PublicIncluded=1 AND s.PublicActive=1 AND i.Active=1) END,
              p.ProductId
            OFFSET @offset ROWS FETCH NEXT @take ROWS ONLY
            """;
        await using var cmd = M0Sql.Command(c, sqlText, null, ("@d", Dataset), ("@q", query.SearchText), ("@locale", query.Locale),
            ("@pattern", query.SearchPattern), ("@color", query.ColorId), ("@size", query.GarmentSize), ("@inStock", query.InStock),
            ("@sort", query.Sort), ("@offset", query.Offset), ("@take", query.Limit + 1));
        await using var reader = await cmd.ExecuteReaderAsync(h.RequestAborted);
        var products = new List<JsonObject>(); while (await reader.ReadAsync(h.RequestAborted)) products.Add(Parse(reader.GetString(0)));
        await reader.CloseAsync();
        var hasMore = products.Count > query.Limit;
        if (hasMore) products.RemoveAt(products.Count - 1);
        var items = new List<JsonObject>();
        foreach (var item in products)
        {
            var product = await HydrateProduct(c, item, h.RequestAborted, query);
            foreach (var sku in product["skus"]!.AsArray().OfType<JsonObject>()) items.Add(new JsonObject
            {
                ["productId"] = product["id"]!.DeepClone(), ["slug"] = product["slug"]!.DeepClone(),
                ["translations"] = product["translations"]!.DeepClone(), ["skuId"] = sku["skuId"]!.DeepClone(),
                ["media"] = product["media"]!.DeepClone(), ["colorId"] = sku["colorId"]!.DeepClone(),
                ["garmentSize"] = sku["garmentSize"]!.DeepClone(),
                ["priceTwd"] = sku["priceTwd"]!.DeepClone(), ["availability"] = sku["availability"]!.DeepClone(), ["mode"] = "mock"
            });
        }
        return M0Http.Json(h, new { mode = "mock", items, nextCursor = hasMore ? query.NextCursor(query.Offset + products.Count) : null });
    }

    public async Task<IResult> Product(HttpContext h, string slug)
    {
        await using var c = await sql.OpenAsync(h.RequestAborted);
        await using var cmd = M0Sql.Command(c, "SELECT PublicJson FROM m0.CatalogProducts WHERE Dataset=@d AND PublicSlug=@s AND State='Published'", null, ("@d", Dataset), ("@s", slug));
        var value = await cmd.ExecuteScalarAsync(h.RequestAborted);
        if (value is not string raw) throw new M0Exception(404, "RESOURCE_NOT_FOUND");
        return M0Http.Json(h, await HydrateProduct(c, Parse(raw), h.RequestAborted));
    }

    public async Task<IResult> AdminProducts(HttpContext h)
    {
        var actor = await security.RequirePermissionAsync(h, "catalog.read", "Dataset", Dataset, ct: h.RequestAborted);
        var products = await ReadAdminProducts(h, actor, null);
        return M0Http.Json(h, new { mode = "mock", items = products });
    }

    public async Task<IResult> GetAdminProductOrDraft(HttpContext h, string id)
    {
        M0Principal actor;
        try
        {
            actor = await security.RequirePermissionAsync(h, "catalog.read", "Dataset", Dataset, ct: h.RequestAborted);
        }
        catch (M0Exception error) when (error.Status == 403)
        {
            // Keep the pre-existing catalog.write draft read behavior for authorized
            // editors; catalog.read users only receive the published projection below.
            return await GetDraft(h, id);
        }

        var products = await ReadAdminProducts(h, actor, id);
        if (products.Count == 0) throw new M0Exception(404, "RESOURCE_NOT_FOUND");
        return M0Http.Json(h, products[0]);
    }

    async Task<List<JsonObject>> ReadAdminProducts(HttpContext h, M0Principal actor, string? productId)
    {
        await using var c = await sql.OpenAsync(h.RequestAborted);
        await using var tx = (SqlTransaction)await c.BeginTransactionAsync(IsolationLevel.Serializable, h.RequestAborted);
        await LockSecurity(c, tx, h.RequestAborted);
        await using (var authorization = M0Sql.Command(c, "m0.security_catalog_read_allowed", tx,
            ("@SessionId", actor.SessionId), ("@UserId", actor.UserId),
            ("@SecurityVersion", actor.SecurityVersion), ("@DatasetId", Dataset), ("@Now", clock.GetUtcNow())))
        {
            authorization.CommandType = CommandType.StoredProcedure;
            if (await authorization.ExecuteScalarAsync(h.RequestAborted) is not true)
                throw new M0Exception(403, "PERMISSION_CHANGED");
        }
        await using var cmd = M0Sql.Command(c,
            "SELECT PublicJson FROM m0.CatalogProducts WHERE Dataset=@d AND State='Published' AND PublicJson IS NOT NULL AND (@id IS NULL OR ProductId=@id) ORDER BY ProductId",
            tx, ("@d", Dataset), ("@id", (object?)productId));
        await using var reader = await cmd.ExecuteReaderAsync(h.RequestAborted);
        var products = new List<JsonObject>();
        while (await reader.ReadAsync(h.RequestAborted))
            products.Add(Parse(reader.GetString(0)));
        await reader.CloseAsync();
        for (var index = 0; index < products.Count; index++)
            products[index] = await HydrateProduct(c, products[index], h.RequestAborted, transaction: tx);
        await tx.CommitAsync(h.RequestAborted);
        return products;
    }

    static async Task<JsonObject> HydrateProduct(SqlConnection c, JsonObject product, CancellationToken ct, CatalogSearchQuery? filter = null, SqlTransaction? transaction = null)
    {
        var id = product.Text("id");
        await using var cmd = M0Sql.Command(c, "SELECT s.SkuId,i.Price,s.ColorId,s.GarmentSize,i.Stock FROM m0.CatalogProductSkus s JOIN m0commerce.Inventory i ON i.Dataset=s.Dataset AND i.Sku=s.SkuId WHERE s.Dataset=@d AND s.ProductId=@p AND s.PublicIncluded=1 AND s.PublicActive=1 AND i.Active=1 AND (@color IS NULL OR s.ColorId=@color) AND (@size IS NULL OR s.GarmentSize=@size) AND (@inStock=0 OR i.Stock>0) ORDER BY s.SkuId", transaction,
            ("@d", Dataset), ("@p", id), ("@color", filter?.ColorId), ("@size", filter?.GarmentSize), ("@inStock", filter?.InStock ?? false));
        await using var reader = await cmd.ExecuteReaderAsync(ct); var skus = new JsonArray();
        while (await reader.ReadAsync(ct)) skus.Add(new JsonObject { ["skuId"] = reader.GetString(0), ["priceTwd"] = reader.GetInt64(1), ["colorId"] = reader.GetString(2), ["garmentSize"] = reader.GetString(3), ["stock"] = reader.GetInt32(4), ["availability"] = reader.GetInt32(4) > 0 ? "InStock" : "OutOfStock" });
        product["skus"] = skus; return product;
    }

    public async Task<IResult> Create(HttpContext h)
    {
        var actor = await ownerAuthorization.RequireCatalogOwnerCreateAsync(h, h.RequestAborted);
        var body = CatalogDraftRules.Validate(await M0Http.BodyAsync(h, h.RequestAborted));
        var key = M0Http.Key(h); var hash = Hash(Json(body)); var id = Id("product"); var now = clock.GetUtcNow();
        await using var c = await sql.OpenAsync(h.RequestAborted);
        await using var tx = (SqlTransaction)await c.BeginTransactionAsync(IsolationLevel.Serializable, h.RequestAborted);
        await LockSecurity(c, tx, h.RequestAborted);
        await RequireActor(c, tx, actor, true, id, h.RequestAborted);
        var replay = await Replay(c, tx, actor.UserId, "catalog.create", key, hash, h.RequestAborted);
        if (replay is not null) { await tx.CommitAsync(h.RequestAborted); return M0Http.Json(h, replay, 201, replay.Number("version")); }
        if (Convert.ToInt32(await Scalar(c, tx, "SELECT COUNT(*) FROM m0.CatalogProducts WHERE Dataset=@d AND Slug=@slug", h.RequestAborted, ("@d", Dataset), ("@slug", body.Text("slug")))) != 0)
            throw new M0Exception(409, "KEY_CONFLICT");
        await using (var cmd = M0Sql.Command(c, "INSERT m0.CatalogProducts(Dataset,ProductId,Slug,State,Version,DraftJson,OwnerId,CreatedAt,UpdatedAt) VALUES(@d,@id,@slug,'Draft',1,@body,@actor,@now,@now)", tx,
            ("@d", Dataset), ("@id", id), ("@slug", body.Text("slug")), ("@body", Json(body)), ("@actor", actor.UserId), ("@now", now)))
            await cmd.ExecuteNonQueryAsync(h.RequestAborted);
        foreach (var sku in body["skus"]!.AsArray().OfType<JsonObject>())
        {
            if (Convert.ToInt32(await Scalar(c, tx, "SELECT COUNT(*) FROM m0commerce.Inventory WHERE Dataset=@d AND Sku=@sku", h.RequestAborted, ("@d", Dataset), ("@sku", sku.Text("id")))) != 0) throw new M0Exception(409, "KEY_CONFLICT");
            await using var inventory = M0Sql.Command(c, "INSERT m0commerce.Inventory(Dataset,Sku,Price,Stock,Active) VALUES(@d,@sku,0,0,0)", tx, ("@d", Dataset), ("@sku", sku.Text("id")));
            await inventory.ExecuteNonQueryAsync(h.RequestAborted);
            await using var row = M0Sql.Command(c, "INSERT m0.CatalogProductSkus(Dataset,SkuId,ProductId,ColorId,GarmentSize,DraftPriceTwd,DraftActive,DraftIncluded) VALUES(@d,@sku,@p,@color,@size,@price,@active,1)", tx,
                ("@d", Dataset), ("@sku", sku.Text("id")), ("@p", id), ("@color", sku.Text("colorId")), ("@size", sku.Text("garmentSize")), ("@price", sku.Number("unitPriceTwd")), ("@active", sku["active"]!.GetValue<bool>()));
            await row.ExecuteNonQueryAsync(h.RequestAborted);
        }
        await Audit(c, tx, id, actor.UserId, "ProductCreated", 1, now, new JsonObject { ["slug"] = body.Text("slug"), ["skuIds"] = new JsonArray(body["skus"]!.AsArray().OfType<JsonObject>().Select(s => (JsonNode?)JsonValue.Create(s.Text("id"))).ToArray()) }, h.RequestAborted);
        body["id"] = id; body["state"] = "Draft"; body["version"] = 1;
        await Record(c, tx, actor.UserId, "catalog.create", key, hash, Json(body), 201, h.RequestAborted);
        await tx.CommitAsync(h.RequestAborted);
        return M0Http.Json(h, body, 201, 1);
    }

    public async Task<IResult> GetDraft(HttpContext h, string id)
    {
        var actor = await security.RequirePermissionAsync(h, "catalog.write", "Product", id, ct: h.RequestAborted);
        await using var c = await sql.OpenAsync(h.RequestAborted);
        await using var cmd = M0Sql.Command(c, "SELECT DraftJson,State,Version FROM m0.CatalogProducts WHERE Dataset=@d AND ProductId=@id", null, ("@d", Dataset), ("@id", id));
        await using var reader = await cmd.ExecuteReaderAsync(h.RequestAborted);
        if (!await reader.ReadAsync(h.RequestAborted)) throw new M0Exception(404, "RESOURCE_NOT_FOUND");
        var body = Parse(reader.GetString(0)); body["id"] = id; body["state"] = reader.GetString(1); body["version"] = reader.GetInt64(2);
        h.Response.Headers.ETag = M0Http.ETag(reader.GetInt64(2));
        _ = actor;
        return M0Http.Json(h, body);
    }

    public async Task<IResult> Update(HttpContext h, string id)
    {
        var actor = await security.RequirePermissionAsync(h, "catalog.write", "Product", id, ct: h.RequestAborted);
        var body = CatalogDraftRules.Validate(await M0Http.BodyAsync(h, h.RequestAborted));
        var key = M0Http.Key(h); var hash = Hash(Json(body) + "\0" + h.Request.Headers.IfMatch);
        await using var c = await sql.OpenAsync(h.RequestAborted);
        await using var tx = (SqlTransaction)await c.BeginTransactionAsync(IsolationLevel.Serializable, h.RequestAborted);
        await LockSecurity(c, tx, h.RequestAborted); await RequireActor(c, tx, actor, false, id, h.RequestAborted);
        await using var read = M0Sql.Command(c, "SELECT DraftJson,State,Version FROM m0.CatalogProducts WITH(UPDLOCK,HOLDLOCK) WHERE Dataset=@d AND ProductId=@id", tx, ("@d", Dataset), ("@id", id));
        await using var reader = await read.ExecuteReaderAsync(h.RequestAborted);
        if (!await reader.ReadAsync(h.RequestAborted)) throw new M0Exception(404, "RESOURCE_NOT_FOUND");
        var version = reader.GetInt64(2); var state = reader.GetString(1); reader.Close();
        var replay = await Replay(c, tx, actor.UserId, "catalog.update/" + id, key, hash, h.RequestAborted);
        if (replay is not null) { await tx.CommitAsync(h.RequestAborted); return M0Http.Json(h, replay, version: replay.Number("version")); }
        M0Http.Match(h, version);
        if (Convert.ToInt32(await Scalar(c, tx, "SELECT COUNT(*) FROM m0.CatalogProducts WHERE Dataset=@d AND Slug=@slug AND ProductId<>@id", h.RequestAborted, ("@d", Dataset), ("@slug", body.Text("slug")), ("@id", id))) != 0) throw new M0Exception(409, "KEY_CONFLICT");
        var updated = await Exec(c, tx, "UPDATE m0.CatalogProducts SET Slug=@slug,DraftJson=@body,Version=Version+1,UpdatedAt=@now WHERE Dataset=@d AND ProductId=@id AND Version=@v", h.RequestAborted,
            ("@slug", body.Text("slug")), ("@body", Json(body)), ("@now", clock.GetUtcNow()), ("@d", Dataset), ("@id", id), ("@v", version));
        if (updated != 1) throw new M0Exception(412, "VERSION_CHANGED");
        await Exec(c, tx, "UPDATE m0.CatalogProductSkus SET DraftIncluded=0 WHERE Dataset=@d AND ProductId=@id", h.RequestAborted, ("@d", Dataset), ("@id", id));
        foreach (var sku in body["skus"]!.AsArray().OfType<JsonObject>())
        {
            var exists = Convert.ToInt32(await Scalar(c, tx, "SELECT COUNT(*) FROM m0.CatalogProductSkus WHERE Dataset=@d AND SkuId=@sku AND ProductId=@id", h.RequestAborted, ("@d", Dataset), ("@sku", sku.Text("id")), ("@id", id)));
            if (exists == 1) await Exec(c, tx, "UPDATE m0.CatalogProductSkus SET ColorId=@color,GarmentSize=@size,DraftPriceTwd=@price,DraftActive=@active,DraftIncluded=1 WHERE Dataset=@d AND ProductId=@id AND SkuId=@sku", h.RequestAborted, ("@color", sku.Text("colorId")), ("@size", sku.Text("garmentSize")), ("@price", sku.Number("unitPriceTwd")), ("@active", sku["active"]!.GetValue<bool>()), ("@d", Dataset), ("@id", id), ("@sku", sku.Text("id")));
            else { if (Convert.ToInt32(await Scalar(c, tx, "SELECT COUNT(*) FROM m0commerce.Inventory WHERE Dataset=@d AND Sku=@sku", h.RequestAborted, ("@d", Dataset), ("@sku", sku.Text("id")))) != 0) throw new M0Exception(409, "KEY_CONFLICT"); await Exec(c, tx, "INSERT m0commerce.Inventory(Dataset,Sku,Price,Stock,Active) VALUES(@d,@sku,0,0,0)", h.RequestAborted, ("@d", Dataset), ("@sku", sku.Text("id"))); await Exec(c, tx, "INSERT m0.CatalogProductSkus(Dataset,SkuId,ProductId,ColorId,GarmentSize,DraftPriceTwd,DraftActive,DraftIncluded) VALUES(@d,@sku,@id,@color,@size,@price,@active,1)", h.RequestAborted, ("@d", Dataset), ("@sku", sku.Text("id")), ("@id", id), ("@color", sku.Text("colorId")), ("@size", sku.Text("garmentSize")), ("@price", sku.Number("unitPriceTwd")), ("@active", sku["active"]!.GetValue<bool>())); }
        }
        await Audit(c, tx, id, actor.UserId, "ProductDraftUpdated", version + 1, clock.GetUtcNow(), new JsonObject { ["slug"] = body.Text("slug"), ["skuPricesTwd"] = new JsonArray(body["skus"]!.AsArray().OfType<JsonObject>().Select(s => (JsonNode?)new JsonObject { ["skuId"] = s.Text("id"), ["priceTwd"] = s.Number("unitPriceTwd") }).ToArray()) }, h.RequestAborted);
        var result = (JsonObject)body.DeepClone(); result["id"] = id; result["state"] = state; result["version"] = version + 1;
        await Record(c, tx, actor.UserId, "catalog.update/" + id, key, hash, Json(result), 200, h.RequestAborted);
        await tx.CommitAsync(h.RequestAborted); return M0Http.Json(h, result, version: version + 1);
    }

    public async Task<IResult> Publish(HttpContext h, string id)
    {
        var actor = await security.RequirePermissionAsync(h, "catalog.write", "Product", id, ct: h.RequestAborted);
        var body = await M0Http.BodyAsync(h, h.RequestAborted); if (body.Count != 1 || body["action"]?.GetValue<string>() is not ("publish" or "unpublish")) throw new M0Exception(422, "VALIDATION_ERROR");
        var publish = body.Text("action") == "publish";
        var key = M0Http.Key(h); var hash = Hash((publish ? "publish" : "unpublish") + "\0" + h.Request.Headers.IfMatch);
        await using var c = await sql.OpenAsync(h.RequestAborted);
        await using var tx = (SqlTransaction)await c.BeginTransactionAsync(IsolationLevel.Serializable, h.RequestAborted);
        await LockSecurity(c, tx, h.RequestAborted); await RequireActor(c, tx, actor, false, id, h.RequestAborted);
        var rows = await Rows(c, tx, "SELECT Slug,DraftJson,PublicJson,State,Version FROM m0.CatalogProducts WITH(UPDLOCK,HOLDLOCK) WHERE Dataset=@d AND ProductId=@id", h.RequestAborted, ("@d", Dataset), ("@id", id));
        if (rows.Count != 1) throw new M0Exception(404, "RESOURCE_NOT_FOUND");
        var current = rows[0]; var version = (long)current[4];
        var replay = await Replay(c, tx, actor.UserId, "catalog.publish/" + id, key, hash, h.RequestAborted);
        if (replay is not null) { await tx.CommitAsync(h.RequestAborted); return M0Http.Json(h, replay, version: replay.Number("version")); }
        M0Http.Match(h, version);
        var draft = Parse((string)current[1]); var nextState = publish ? "Published" : "Unpublished";
        var auditDetails = new JsonObject { ["state"] = nextState };
        JsonObject? publicJson = current[2] is string oldPublic ? Parse(oldPublic) : null;
        if (publish)
        {
            await Exec(c, tx, "UPDATE m0.CatalogProductSkus SET PublicIncluded=0,PublicActive=0 WHERE Dataset=@d AND ProductId=@id", h.RequestAborted, ("@d", Dataset), ("@id", id));
            await Exec(c, tx, "UPDATE m0commerce.Inventory SET Active=0 WHERE Dataset=@d AND Sku IN (SELECT SkuId FROM m0.CatalogProductSkus WHERE Dataset=@d AND ProductId=@id)", h.RequestAborted, ("@d", Dataset), ("@id", id));
            var skuRows = await Rows(c, tx, "SELECT SkuId,DraftPriceTwd,DraftActive,DraftIncluded,ColorId,GarmentSize FROM m0.CatalogProductSkus WHERE Dataset=@d AND ProductId=@id AND DraftIncluded=1 ORDER BY SkuId", h.RequestAborted, ("@d", Dataset), ("@id", id));
            if (skuRows.Count == 0) throw new M0Exception(422, "VALIDATION_ERROR");
            var skus = new JsonArray();
            foreach (var row in skuRows) { await Exec(c, tx, "UPDATE m0.CatalogProductSkus SET PublicPriceTwd=DraftPriceTwd,PublicActive=DraftActive,PublicIncluded=1 WHERE Dataset=@d AND SkuId=@sku", h.RequestAborted, ("@d", Dataset), ("@sku", row[0])); await Exec(c, tx, "UPDATE m0commerce.Inventory SET Price=@price,Active=@active WHERE Dataset=@d AND Sku=@sku", h.RequestAborted, ("@price", row[1]), ("@active", row[2]), ("@d", Dataset), ("@sku", row[0])); skus.Add(new JsonObject { ["skuId"] = (string)row[0], ["priceTwd"] = (long)row[1], ["availability"] = "InStock", ["colorId"] = (string)row[4], ["garmentSize"] = (string)row[5] }); }
            auditDetails["skuPricesTwd"] = new JsonArray(skuRows.Select(row => (JsonNode?)new JsonObject { ["skuId"] = (string)row[0], ["priceTwd"] = (long)row[1] }).ToArray());
            var publicMedia = new JsonArray();
            foreach (var item in draft["media"]!.AsArray().OfType<JsonObject>())
            {
                var media = item["media"]!.AsObject();
                publicMedia.Add(new JsonObject
                {
                    ["media"] = new JsonObject
                    {
                        ["id"] = media["id"]?.DeepClone(),
                        ["url"] = media["url"]?.DeepClone(),
                        ["alt"] = media["alt"]?.DeepClone(),
                    }
                });
            }
            publicJson = new JsonObject { ["id"] = id, ["slug"] = (string)current[0], ["translations"] = draft["translations"]!.DeepClone(), ["media"] = publicMedia, ["measurements"] = draft["measurements"]!.DeepClone(), ["skus"] = skus, ["mode"] = "mock" };
        }
        else
        {
            await Exec(c, tx, "UPDATE m0.CatalogProductSkus SET PublicActive=0 WHERE Dataset=@d AND ProductId=@id", h.RequestAborted, ("@d", Dataset), ("@id", id));
            await Exec(c, tx, "UPDATE m0commerce.Inventory SET Active=0 WHERE Dataset=@d AND Sku IN (SELECT SkuId FROM m0.CatalogProductSkus WHERE Dataset=@d AND ProductId=@id)", h.RequestAborted, ("@d", Dataset), ("@id", id));
        }
        await Exec(c, tx, "UPDATE m0.CatalogProducts SET State=@state,PublicSlug=CASE WHEN @publish=1 THEN Slug ELSE NULL END,PublicRevision=CASE WHEN @publish=1 THEN CONVERT(varchar(100),NEWID()) ELSE PublicRevision END,PublicJson=CASE WHEN @publish=1 THEN @public ELSE PublicJson END,Version=Version+1,UpdatedAt=@now WHERE Dataset=@d AND ProductId=@id", h.RequestAborted, ("@state", nextState), ("@publish", publish), ("@public", publicJson is null ? DBNull.Value : Json(publicJson)), ("@now", clock.GetUtcNow()), ("@d", Dataset), ("@id", id));
        var result = new JsonObject { ["id"] = id, ["state"] = nextState, ["version"] = version + 1 }; await Audit(c, tx, id, actor.UserId, publish ? "ProductPublished" : "ProductUnpublished", version + 1, clock.GetUtcNow(), auditDetails, h.RequestAborted);
        await Record(c, tx, actor.UserId, "catalog.publish/" + id, key, hash, Json(result), 200, h.RequestAborted);
        await tx.CommitAsync(h.RequestAborted); return M0Http.Json(h, result, version: version + 1);
    }

    async Task LockSecurity(SqlConnection c, SqlTransaction tx, CancellationToken ct) { await using var cmd = M0Sql.Command(c, "m0.security_lock", tx); cmd.CommandType = CommandType.StoredProcedure; await cmd.ExecuteNonQueryAsync(ct); }
    async Task RequireActor(SqlConnection c, SqlTransaction tx, M0Principal actor, bool ownerOnly, string productId, CancellationToken ct)
    {
        await using var cmd = M0Sql.Command(c, "m0.security_catalog_actor_allowed", tx,
            ("@SessionId", actor.SessionId), ("@UserId", actor.UserId), ("@SecurityVersion", actor.SecurityVersion),
            ("@Now", clock.GetUtcNow()), ("@OwnerOnly", ownerOnly), ("@Dataset", Dataset), ("@ProductId", productId));
        cmd.CommandType = CommandType.StoredProcedure;
        if (await cmd.ExecuteScalarAsync(ct) is not true) throw new M0Exception(403, "PERMISSION_CHANGED");
    }
    async Task Audit(SqlConnection c, SqlTransaction tx, string product, string actor, string action, long version, DateTimeOffset now, JsonObject details, CancellationToken ct) => await Exec(c, tx, "INSERT m0.CatalogAuditEvents(Dataset,AuditId,ProductId,ActorId,Action,Outcome,Version,At,Details) VALUES(@d,@a,@p,@actor,@action,'Succeeded',@v,@now,@details)", ct, ("@d", Dataset), ("@a", Id("audit")), ("@p", product), ("@actor", actor), ("@action", action), ("@v", version), ("@now", now), ("@details", Json(details)));
    async Task<JsonObject?> Replay(SqlConnection c, SqlTransaction tx, string actor, string scope, string key, string hash, CancellationToken ct) { var rows = await Rows(c, tx, "SELECT RequestHash,Response FROM m0commerce.Idempotency WHERE Dataset=@d AND Actor=@a AND Scope=@s AND OperationKey=@k", ct, ("@d", Dataset), ("@a", actor), ("@s", scope), ("@k", key)); if (rows.Count == 0) return null; if (!StringComparer.Ordinal.Equals((string)rows[0][0], hash)) throw new M0Exception(409, "KEY_CONFLICT"); return Parse((string)rows[0][1]); }
    async Task Record(SqlConnection c, SqlTransaction tx, string actor, string scope, string key, string hash, string response, int status, CancellationToken ct) => await Exec(c, tx, "INSERT m0commerce.Idempotency(Dataset,Actor,Scope,OperationKey,RequestHash,Response,StatusCode) VALUES(@d,@a,@s,@k,@h,@r,@status)", ct, ("@d", Dataset), ("@a", actor), ("@s", scope), ("@k", key), ("@h", hash), ("@r", response), ("@status", status));
    static async Task<int> Exec(SqlConnection c, SqlTransaction tx, string sql, CancellationToken ct, params (string Name, object? Value)[] values) { await using var cmd = M0Sql.Command(c, sql, tx, values); return await cmd.ExecuteNonQueryAsync(ct); }
    static async Task<object?> Scalar(SqlConnection c, SqlTransaction tx, string sql, CancellationToken ct, params (string Name, object? Value)[] values) { await using var cmd = M0Sql.Command(c, sql, tx, values); return await cmd.ExecuteScalarAsync(ct); }
    static async Task<List<object[]>> Rows(SqlConnection c, SqlTransaction tx, string sql, CancellationToken ct, params (string Name, object? Value)[] values) { await using var cmd = M0Sql.Command(c, sql, tx, values); await using var r = await cmd.ExecuteReaderAsync(ct); var rows = new List<object[]>(); while (await r.ReadAsync(ct)) { var row = new object[r.FieldCount]; r.GetValues(row); rows.Add(row); } return rows; }
}
