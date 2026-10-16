using Kohami.Commerce.Domain;

internal static class InventoryAdjustmentRulesAcceptance
{
    public static void Run()
    {
        var increase = StockAdjustmentRules.Calculate(sellableStock: 4, reserved: 2, quantityDelta: 3);
        Check(increase.OnHandBefore == 6 && increase.OnHandAfter == 9 && increase.Reserved == 2 && increase.SellableStockAfter == 7,
            "stock increase preserves held reservation and records on-hand result");

        var exactReservedFloor = StockAdjustmentRules.Calculate(sellableStock: 4, reserved: 2, quantityDelta: -4);
        Check(exactReservedFloor.OnHandAfter == exactReservedFloor.Reserved && exactReservedFloor.SellableStockAfter == 0,
            "stock may be reduced to the held reservation floor");

        Reject(() => StockAdjustmentRules.Calculate(sellableStock: 4, reserved: 2, quantityDelta: -5),
            "stock cannot be reduced below held reservations");
        Reject(() => StockAdjustmentRules.Calculate(sellableStock: 0, reserved: 0, quantityDelta: -1),
            "stock cannot become negative");
        Reject(() => StockAdjustmentRules.Calculate(sellableStock: int.MaxValue, reserved: 0, quantityDelta: 1),
            "stock cannot overflow SQL inventory range");
        var largeHeldReservation = StockAdjustmentRules.Calculate(sellableStock: 0, reserved: (long)int.MaxValue + 1, quantityDelta: 1);
        Check(largeHeldReservation.OnHandAfter == (long)int.MaxValue + 2 && largeHeldReservation.Reserved == (long)int.MaxValue + 1,
            "on-hand arithmetic supports a reservation aggregate larger than one SQL inventory row");
        Reject(() => StockAdjustmentRules.Calculate(sellableStock: 1, reserved: 0, quantityDelta: 0),
            "zero quantity adjustment is rejected");
    }

    private static void Reject(Action action, string name)
    {
        try { action(); throw new InvalidOperationException("FAILED: " + name); }
        catch (StockAdjustmentRejectedException) { Console.WriteLine("PASS: " + name); }
    }

    private static void Check(bool pass, string name)
    {
        if (!pass) throw new InvalidOperationException("FAILED: " + name);
        Console.WriteLine("PASS: " + name);
    }
}
