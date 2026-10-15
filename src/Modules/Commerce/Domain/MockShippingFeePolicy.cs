using System.Collections.Frozen;

namespace Kohami.Commerce.Domain;

public sealed record MockShippingFee(
    string DeliveryMethod,
    string Region,
    long AmountTwd,
    string Version,
    string Notice);

public static class MockShippingFeePolicy
{
    public const string CurrentVersion = "fee_demo_v1";

    private static readonly FrozenDictionary<(string DeliveryMethod, string Region), MockShippingFee> Fees =
        new[]
        {
            new MockShippingFee("mock_home", "demo_supported", 60, CurrentVersion, "示意運費，非正式報價"),
            new MockShippingFee("mock_pickup", "demo_supported", 0, CurrentVersion, "示意取貨費用，非正式報價")
        }.ToFrozenDictionary(fee => (fee.DeliveryMethod, fee.Region));

    public static bool IsSupportedRegion(string region) =>
        Fees.Keys.Any(key => key.Region == region);

    public static bool IsSupportedDeliveryMethod(string deliveryMethod) =>
        Fees.Keys.Any(key => key.DeliveryMethod == deliveryMethod);

    public static MockShippingFee? Resolve(string deliveryMethod, string region) =>
        Fees.GetValueOrDefault((deliveryMethod, region));
}
