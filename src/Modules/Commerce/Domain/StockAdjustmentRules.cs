namespace Kohami.Commerce.Domain;

public sealed record StockAdjustmentResult(
    int SellableStockBefore,
    int SellableStockAfter,
    long Reserved,
    long OnHandBefore,
    long OnHandAfter);

public sealed class StockAdjustmentRejectedException : Exception { }

public static class StockAdjustmentRules
{
    public static StockAdjustmentResult Calculate(int sellableStock, long reserved, int quantityDelta)
    {
        if (sellableStock < 0 || reserved < 0 || quantityDelta == 0)
            throw new StockAdjustmentRejectedException();

        var sellableAfter = (long)sellableStock + quantityDelta;
        if (sellableAfter is < 0 or > int.MaxValue)
            throw new StockAdjustmentRejectedException();

        var onHandBefore = (long)sellableStock + reserved;
        var onHandAfter = onHandBefore + quantityDelta;
        if (onHandAfter < reserved)
            throw new StockAdjustmentRejectedException();

        return new(sellableStock, (int)sellableAfter, reserved, onHandBefore, onHandAfter);
    }
}
