namespace Kohami.Commerce.Domain;

public sealed record MemberOrderActionAvailability(string Action, bool Allowed, string[] ReasonCodes);

public static class MemberOrderActionPolicy
{
    public static IReadOnlyList<MemberOrderActionAvailability> Availability(string orderState, bool hasDeliveredShipment, int freeRefundUnitCount)
    {
        var cancelAllowed = orderState == "AwaitingPayment";
        var cancelReasons = cancelAllowed ? Array.Empty<string>() : ["ORDER_NOT_AWAITING_PAYMENT"];

        var returnReasons = new List<string>();
        if (orderState != "Confirmed") returnReasons.Add("ORDER_NOT_CONFIRMED");
        if (!hasDeliveredShipment) returnReasons.Add("SHIPMENT_NOT_DELIVERED");
        if (freeRefundUnitCount < 1) returnReasons.Add("NO_RETURNABLE_UNITS");

        return
        [
            new("Cancel", cancelAllowed, cancelReasons),
            new("RequestReturn", returnReasons.Count == 0, returnReasons.ToArray()),
        ];
    }
}
