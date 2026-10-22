using Kohami.Commerce.Domain;

namespace Kohami.Commerce.Tests;

public static class Issue26OrderActionAvailabilityContract
{
    public static void Run()
    {
        var cases = new[]
        {
            new { State = "AwaitingPayment", Delivered = false, FreeUnits = 0, Cancel = true, Return = false, ReturnReason = "ORDER_NOT_CONFIRMED" },
            new { State = "Confirmed", Delivered = true, FreeUnits = 2, Cancel = false, Return = true, ReturnReason = "" },
            new { State = "Confirmed", Delivered = false, FreeUnits = 2, Cancel = false, Return = false, ReturnReason = "SHIPMENT_NOT_DELIVERED" },
            new { State = "Confirmed", Delivered = true, FreeUnits = 0, Cancel = false, Return = false, ReturnReason = "NO_RETURNABLE_UNITS" },
            new { State = "AwaitingPayment", Delivered = true, FreeUnits = 2, Cancel = true, Return = false, ReturnReason = "ORDER_NOT_CONFIRMED" },
        };

        foreach (var item in cases)
        {
            var actions = MemberOrderActionPolicy.Availability(item.State, item.Delivered, item.FreeUnits);
            var cancel = actions.Single(action => action.Action == "Cancel");
            var requestReturn = actions.Single(action => action.Action == "RequestReturn");
            Require(cancel.Allowed == item.Cancel, $"ISSUE26_CANCEL_{item.State}_{item.Delivered}_{item.FreeUnits}");
            Require(requestReturn.Allowed == item.Return, $"ISSUE26_RETURN_{item.State}_{item.Delivered}_{item.FreeUnits}");
            if (item.ReturnReason.Length > 0)
                Require(requestReturn.ReasonCodes.Contains(item.ReturnReason), $"ISSUE26_RETURN_REASON_{item.ReturnReason}");
        }

        Console.WriteLine($"PASS Issue26 order action availability policy ({cases.Length} cases)");
    }

    private static void Require(bool condition, string code)
    {
        if (!condition) throw new InvalidOperationException(code);
    }
}
