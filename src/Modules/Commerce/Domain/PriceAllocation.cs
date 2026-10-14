namespace Kohami.Commerce.Domain;

public sealed record PricedSku(string SkuId, int Quantity, long UnitPriceTwd);
public sealed record AllocatedLine(string SkuId, int Quantity, long UnitPriceTwd,
    long DiscountTwd, long PaidTwd, IReadOnlyList<long> UnitPayments);
public sealed record AllocatedPrice(long MerchandiseTwd, long DiscountTwd,
    long ShippingTwd, long TotalTwd, IReadOnlyList<AllocatedLine> Lines);

/// <summary>Integer TWD allocation, with stable SKU and per-unit ordering.</summary>
public static class PriceAllocation
{
    public static AllocatedPrice Calculate(IEnumerable<PricedSku> items, long shipping, bool discount)
    {
        var source = items.OrderBy(x => x.SkuId, StringComparer.Ordinal).ToArray();
        if (shipping < 0 || source.Length == 0 || source.Any(x => x.Quantity < 1 || x.UnitPriceTwd < 0))
            throw new ArgumentException("INVALID_PRICE_INPUT");
        long gross = source.Sum(x => checked(x.Quantity * x.UnitPriceTwd));
        if (gross <= 0) throw new ArgumentException("INVALID_PRICE_INPUT");
        long off = discount ? (long)decimal.Round(gross * .15m, 0, MidpointRounding.AwayFromZero) : 0;
        var exact = source.Select(x => off * (decimal)(x.Quantity * x.UnitPriceTwd) / gross).ToArray();
        var cuts = exact.Select(x => (long)x).ToArray();
        var remainder = off - cuts.Sum();
        foreach (var i in Enumerable.Range(0, source.Length)
            .OrderByDescending(i => exact[i] - cuts[i]).ThenBy(i => i).Take((int)remainder)) cuts[i]++;
        var lines = source.Select((x, i) =>
        {
            var paid = checked(x.Quantity * x.UnitPriceTwd - cuts[i]);
            var units = Enumerable.Range(0, x.Quantity)
                .Select(n => paid / x.Quantity + (n >= x.Quantity - paid % x.Quantity ? 1 : 0)).ToArray();
            return new AllocatedLine(x.SkuId, x.Quantity, x.UnitPriceTwd, cuts[i], paid, Array.AsReadOnly(units));
        }).ToArray();
        return new(gross, off, shipping, checked(gross - off + shipping), Array.AsReadOnly(lines));
    }
}
