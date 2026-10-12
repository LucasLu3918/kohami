namespace Kohami.CommerceBaseline;

public static class CheckoutPolicy
{
    public static int DiscountedSubtotal(int subtotal, decimal discountPercent)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(subtotal);
        if (discountPercent is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(discountPercent));
        var discount = decimal.Round(subtotal * discountPercent / 100m, 0, MidpointRounding.AwayFromZero);
        return subtotal - (int)discount;
    }
}

public sealed record CheckoutRequest(int Quantity, int UnitPriceTwd, decimal DiscountPercent, int ShippingTwd);
public enum OrderState { Pending, Confirmed, Cancelled, Expired }
public enum PaymentOutcome { Unknown, Succeeded, Failed }
public enum PaymentState { Pending, Succeeded, Failed }
public sealed record CheckoutSnapshot(string OperationKey, CheckoutRequest Request, int TotalTwd,
    DateTimeOffset ExpiresAt, OrderState State = OrderState.Pending,
    PaymentState Payment = PaymentState.Pending, bool CompensationNeeded = false);

// Deliberately sequential, synthetic and in-memory: not a persistence/concurrency implementation.
public sealed class SyntheticCheckoutStore
{
    private readonly Dictionary<string, CheckoutSnapshot> checkouts = new(StringComparer.Ordinal);
    public int SellableStock { get; private set; }

    public SyntheticCheckoutStore(int initialStock)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(initialStock);
        SellableStock = initialStock;
    }

    public CheckoutSnapshot Start(string operationKey, CheckoutRequest request, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationKey);
        ArgumentNullException.ThrowIfNull(request);
        if (checkouts.TryGetValue(operationKey, out var existing))
        {
            if (existing.Request != request)
                throw new InvalidOperationException("The operation key already has different checkout content.");
            return existing;
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.Quantity);
        ArgumentOutOfRangeException.ThrowIfNegative(request.UnitPriceTwd);
        ArgumentOutOfRangeException.ThrowIfNegative(request.ShippingTwd);
        var total = checked(CheckoutPolicy.DiscountedSubtotal(
            checked(request.Quantity * request.UnitPriceTwd), request.DiscountPercent) + request.ShippingTwd);
        if (total == 0)
            throw new InvalidOperationException("Zero-payable checkout is not enabled.");
        if (request.Quantity > SellableStock)
            throw new InvalidOperationException("Insufficient sellable stock.");

        var snapshot = new CheckoutSnapshot(operationKey, request, total, now.AddMinutes(15));
        checkouts.Add(operationKey, snapshot);
        SellableStock -= request.Quantity;
        return snapshot;
    }

    public CheckoutSnapshot Read(string operationKey) => checkouts[operationKey];

    public CheckoutSnapshot Expire(string operationKey, DateTimeOffset now)
    {
        var snapshot = Read(operationKey);
        if (snapshot.State != OrderState.Pending || now < snapshot.ExpiresAt)
            return snapshot;
        return Release(snapshot, OrderState.Expired);
    }

    public CheckoutSnapshot Cancel(string operationKey)
    {
        var snapshot = Read(operationKey);
        if (snapshot.State != OrderState.Pending)
            return snapshot;
        return Release(snapshot, OrderState.Cancelled);
    }

    private CheckoutSnapshot Release(CheckoutSnapshot snapshot, OrderState state)
    {
        SellableStock += snapshot.Request.Quantity;
        return checkouts[snapshot.OperationKey] = snapshot with { State = state };
    }

    // Caller supplies only an already authenticated, matched transaction outcome.
    // This seam deliberately does not verify signatures, amount/currency or provider identity.
    public CheckoutSnapshot RecordTrustedPayment(string operationKey, PaymentOutcome outcome, DateTimeOffset now)
    {
        if (!Enum.IsDefined(outcome))
            throw new ArgumentOutOfRangeException(nameof(outcome));
        var snapshot = Expire(operationKey, now);
        if (snapshot.Payment == PaymentState.Succeeded || outcome == PaymentOutcome.Unknown)
            return snapshot;

        var next = outcome == PaymentOutcome.Failed
            ? snapshot with { Payment = PaymentState.Failed }
            : snapshot with
            {
                Payment = PaymentState.Succeeded,
                State = snapshot.State == OrderState.Pending ? OrderState.Confirmed : snapshot.State,
                CompensationNeeded = snapshot.State is OrderState.Cancelled or OrderState.Expired
            };
        return checkouts[operationKey] = next;
    }
}
