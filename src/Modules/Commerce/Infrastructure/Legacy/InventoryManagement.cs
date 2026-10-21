using System.Text.Json.Nodes;
using Kohami.Core;
using Kohami.Commerce.Domain;
using Microsoft.AspNetCore.Http;

namespace Kohami.Commerce;

public sealed partial class CommerceService
{
    public async Task<IResult> Stock(HttpContext h, string skuId)
    {
        CommerceWire.Identifier(skuId);
        await security.RequirePermissionAsync(h, "stock.adjust", "Sku", skuId, ct: h.RequestAborted);
        var view = await store.Transaction(async tx =>
        {
            var inventory = await tx.Rows("SELECT Stock,Active FROM commerce.Inventory WHERE Dataset='kohami_local' AND Sku=@sku", ("@sku", skuId));
            if (inventory.Count != 1) throw new KohamiException(404, "RESOURCE_NOT_FOUND");
            var sellable = Convert.ToInt32(inventory[0][0]);
            var reserved = Convert.ToInt64(await tx.Scalar("SELECT COALESCE(SUM(Quantity),0) FROM commerce.Reservations WHERE Dataset='kohami_local' AND Sku=@sku AND State='Held'", ("@sku", skuId)));
            var movements = await tx.Rows("SELECT TOP (20) Id,OwnerId,Body FROM commerce.Documents WHERE Dataset='kohami_local' AND Kind='InventoryMovement' AND JSON_VALUE(Body,'$.movementType')='StockAdjustment' AND JSON_VALUE(Body,'$.skuId')=@sku ORDER BY JSON_VALUE(Body,'$.createdAt') DESC,Id DESC", ("@sku", skuId));
            var history = movements.Select(row =>
            {
                var body = JsonNode.Parse((string)row[2])!.AsObject();
                return CommerceWire.Obj(("id", row[0]), ("actorId", row[1]), ("quantityDelta", body["quantityDelta"]!.DeepClone()),
                    ("reason", body["reason"]!.DeepClone()), ("createdAt", body["createdAt"]!.DeepClone()));
            }).ToArray();
            return CommerceWire.Obj(("skuId", skuId), ("warehouseId", "demo_default"), ("active", inventory[0][1]),
                ("onHand", checked((long)sellable + reserved)), ("reserved", reserved), ("sellableStock", sellable),
                ("availability", (bool)inventory[0][1] && sellable > 0 ? "InStock" : "OutOfStock"),
                ("movements", history));
        }, h.RequestAborted);
        return FoundationHttp.Json(h, view);
    }

    public async Task<IResult> AdjustStock(HttpContext h)
    {
        var b = await FoundationHttp.BodyAsync(h, h.RequestAborted);
        CommerceWire.Fields(b, ["skuId", "quantityDelta", "reason"]);
        var skuId = b.Text("skuId");
        CommerceWire.Identifier(skuId);
        var delta = b.Number("quantityDelta");
        var reason = b.Text("reason").Trim();
        if (delta is < int.MinValue or > int.MaxValue || delta == 0 || reason.Length is < 1 or > 500 || reason.Contains('\r') || reason.Contains('\n'))
            throw new KohamiException(422, "VALIDATION_ERROR");

        var actor = await security.RequirePermissionAsync(h, "stock.adjust", "Sku", skuId, ct: h.RequestAborted);
        var r = await Write(h, actor.UserId, "stock-adjustment/" + skuId, b, async tx =>
        {
            await RequireCurrentGrant(tx, actor, "stock.adjust", "Sku", skuId, clock.GetUtcNow());
            var inventory = await tx.Rows("SELECT Stock FROM commerce.Inventory WITH(UPDLOCK,HOLDLOCK) WHERE Dataset='kohami_local' AND Sku=@sku", ("@sku", skuId));
            if (inventory.Count != 1) throw new KohamiException(404, "RESOURCE_NOT_FOUND");
            var sellableBefore = Convert.ToInt32(inventory[0][0]);
            var reserved = Convert.ToInt64(await tx.Scalar("SELECT COALESCE(SUM(Quantity),0) FROM commerce.Reservations WITH(UPDLOCK,HOLDLOCK) WHERE Dataset='kohami_local' AND Sku=@sku AND State='Held'", ("@sku", skuId)));
            StockAdjustmentResult adjustment;
            try { adjustment = StockAdjustmentRules.Calculate(sellableBefore, reserved, (int)delta); }
            catch (StockAdjustmentRejectedException) { throw new KohamiException(409, "STOCK_CHANGED"); }

            if (await tx.Execute("UPDATE commerce.Inventory SET Stock=@stock WHERE Dataset='kohami_local' AND Sku=@sku AND Stock=@expected",
                    ("@stock", adjustment.SellableStockAfter), ("@sku", skuId), ("@expected", sellableBefore)) != 1)
                throw new KohamiException(409, "STOCK_CHANGED");

            var movementId = FoundationHttp.Id("movement");
            var now = clock.GetUtcNow();
            var movement = CommerceWire.Obj(("id", movementId), ("movementType", "StockAdjustment"), ("warehouseId", "demo_default"),
                ("skuId", skuId), ("quantityDelta", delta), ("reason", reason), ("onHandBefore", adjustment.OnHandBefore), ("onHandAfter", adjustment.OnHandAfter),
                ("reserved", adjustment.Reserved), ("sellableStockBefore", adjustment.SellableStockBefore), ("sellableStockAfter", adjustment.SellableStockAfter),
                ("mode", "mock"), ("state", "Applied"), ("createdAt", now));
            await tx.Save(new("InventoryMovement", movementId, actor.UserId, 1, movement), true);
            return new(CommerceWire.Obj(("id", movementId), ("skuId", skuId), ("warehouseId", "demo_default"),
                ("quantityDelta", delta), ("reason", reason), ("onHand", adjustment.OnHandAfter), ("reserved", adjustment.Reserved),
                ("sellableStock", adjustment.SellableStockAfter), ("availability", adjustment.SellableStockAfter > 0 ? "InStock" : "OutOfStock"),
                ("mode", "mock"), ("state", "Applied"), ("createdAt", now), ("version", CommerceWire.Version("InventoryMovement", movementId, 1))), 201, 1);
        });
        return Result(h, r);
    }
}
