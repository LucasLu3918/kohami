using Kohami.CommerceBaseline;

namespace Kohami.CommerceBaseline.Tests;

public class CheckoutPolicyTests
{
    [Theory]
    [InlineData(101, 15, 86)]
    [InlineData(309, 50, 154)] // Three NT$103 units: discount NT$155 once, not NT$156.
    [InlineData(103, 50, 51)] // Discount NT$51.5 rounds upward to NT$52.
    [InlineData(101, 0, 101)]
    [InlineData(101, 100, 0)]
    public void Discount_is_rounded_once_to_whole_TWD(int subtotal, int percent, int payable) =>
        Assert.Equal(payable, CheckoutPolicy.DiscountedSubtotal(subtotal, percent));

    private static readonly DateTimeOffset SubmittedAt = new(2026, 10, 13, 0, 0, 0, TimeSpan.Zero);
    private static readonly CheckoutRequest TwoUnits = new(2, 101, 15m, 60);

    [Fact]
    public void Same_operation_replay_returns_snapshot_without_reserving_again_or_renewing()
    {
        var store = new SyntheticCheckoutStore(3);
        var first = store.Start("checkout-A", TwoUnits, SubmittedAt);
        var replay = store.Start("checkout-A", TwoUnits, SubmittedAt.AddMinutes(4));

        Assert.Equal(first, replay);
        Assert.Equal(SubmittedAt.AddMinutes(15), replay.ExpiresAt);
        Assert.Equal(232, replay.TotalTwd); // NT$202 less NT$30 discount plus NT$60 shipping.
        Assert.Equal(1, store.SellableStock);
        Assert.Equal(OrderState.Pending, replay.State);
    }

    [Fact]
    public void Changed_content_with_same_key_conflicts_without_changing_stock_or_snapshot()
    {
        var store = new SyntheticCheckoutStore(3);
        var first = store.Start("checkout-A", TwoUnits, SubmittedAt);
        Assert.Throws<InvalidOperationException>(() =>
            store.Start("checkout-A", TwoUnits with { Quantity = 3 }, SubmittedAt.AddMinutes(1)));
        Assert.Equal(first, store.Read("checkout-A"));
        Assert.Equal(1, store.SellableStock);
    }

    [Fact]
    public void Shortage_refuses_checkout_without_partial_reservation()
    {
        var store = new SyntheticCheckoutStore(1);
        Assert.Throws<InvalidOperationException>(() => store.Start("checkout-A", TwoUnits, SubmittedAt));
        Assert.Equal(1, store.SellableStock);
        var one = store.Start("checkout-B", TwoUnits with { Quantity = 1 }, SubmittedAt);
        Assert.Equal(OrderState.Pending, one.State);
        Assert.Equal(0, store.SellableStock);
    }

    [Fact]
    public void Zero_payable_checkout_is_refused_without_reserving()
    {
        var store = new SyntheticCheckoutStore(2);
        Assert.Throws<InvalidOperationException>(() =>
            store.Start("checkout-A", new(1, 101, 100m, 0), SubmittedAt));
        Assert.Equal(2, store.SellableStock);
    }

    [Fact]
    public void Explicit_expiry_releases_once_at_fifteen_minutes()
    {
        var store = new SyntheticCheckoutStore(2);
        var initial = store.Start("checkout-A", TwoUnits, SubmittedAt);
        Assert.Equal(initial, store.Expire("checkout-A", SubmittedAt.AddMinutes(14)));
        Assert.Equal(0, store.SellableStock);
        var expired = store.Expire("checkout-A", SubmittedAt.AddMinutes(15));
        Assert.Equal(OrderState.Expired, expired.State);
        Assert.Equal(PaymentState.Pending, expired.Payment);
        Assert.Equal(2, store.SellableStock);
        Assert.Equal(expired, store.Expire("checkout-A", SubmittedAt.AddMinutes(16)));
        Assert.Equal(expired, store.Cancel("checkout-A"));
        Assert.Equal(2, store.SellableStock);
    }

    [Fact]
    public void Cancellation_releases_once_and_does_not_become_expiry()
    {
        var store = new SyntheticCheckoutStore(2);
        store.Start("checkout-A", TwoUnits, SubmittedAt);
        var cancelled = store.Cancel("checkout-A");
        Assert.Equal(OrderState.Cancelled, cancelled.State);
        Assert.Equal(cancelled, store.Cancel("checkout-A"));
        Assert.Equal(cancelled, store.Expire("checkout-A", SubmittedAt.AddMinutes(20)));
        Assert.Equal(2, store.SellableStock);
    }

    [Fact]
    public void Unknown_payment_remains_pending_then_expires_without_asserting_failure()
    {
        var store = new SyntheticCheckoutStore(2);
        var initial = store.Start("checkout-A", TwoUnits, SubmittedAt);
        Assert.Equal(initial, store.RecordTrustedPayment("checkout-A", PaymentOutcome.Unknown, SubmittedAt.AddMinutes(2)));
        Assert.Equal(0, store.SellableStock);
        var afterExpiry = store.RecordTrustedPayment("checkout-A", PaymentOutcome.Unknown, SubmittedAt.AddMinutes(16));
        Assert.Equal(OrderState.Expired, afterExpiry.State);
        Assert.Equal(PaymentState.Pending, afterExpiry.Payment);
        Assert.False(afterExpiry.CompensationNeeded);
        Assert.Equal(2, store.SellableStock);
    }

    [Fact]
    public void Timely_trusted_success_consumes_hold_once_and_old_failure_cannot_downgrade()
    {
        var store = new SyntheticCheckoutStore(2);
        store.Start("checkout-A", TwoUnits, SubmittedAt);
        var confirmed = store.RecordTrustedPayment("checkout-A", PaymentOutcome.Succeeded, SubmittedAt.AddMinutes(14));
        Assert.Equal(OrderState.Confirmed, confirmed.State);
        Assert.Equal(PaymentState.Succeeded, confirmed.Payment);
        Assert.False(confirmed.CompensationNeeded);
        Assert.Equal(confirmed, store.RecordTrustedPayment("checkout-A", PaymentOutcome.Succeeded, SubmittedAt.AddMinutes(18)));
        Assert.Equal(confirmed, store.RecordTrustedPayment("checkout-A", PaymentOutcome.Failed, SubmittedAt.AddMinutes(19)));
        Assert.Equal(confirmed, store.RecordTrustedPayment("checkout-A", PaymentOutcome.Unknown, SubmittedAt.AddMinutes(20)));
        Assert.Equal(confirmed, store.Cancel("checkout-A"));
        Assert.Equal(confirmed, store.Expire("checkout-A", SubmittedAt.AddMinutes(21)));
        Assert.Equal(0, store.SellableStock);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Late_success_after_release_needs_compensation_without_restoring_order_or_stock(bool cancel)
    {
        var store = new SyntheticCheckoutStore(2);
        store.Start("checkout-A", TwoUnits, SubmittedAt);
        if (cancel) store.Cancel("checkout-A");
        else store.Expire("checkout-A", SubmittedAt.AddMinutes(15));
        store.Start("checkout-B", TwoUnits, SubmittedAt.AddMinutes(16)); // Freed units are promised elsewhere.
        var late = store.RecordTrustedPayment("checkout-A", PaymentOutcome.Succeeded, SubmittedAt.AddMinutes(17));
        Assert.Equal(cancel ? OrderState.Cancelled : OrderState.Expired, late.State);
        Assert.Equal(PaymentState.Succeeded, late.Payment);
        Assert.True(late.CompensationNeeded);
        Assert.Equal(232, late.TotalTwd); // Whole received amount, including shipping, is visible for compensation.
        Assert.Equal(0, store.SellableStock);
        Assert.Equal(OrderState.Pending, store.Read("checkout-B").State);
        Assert.Equal(late, store.RecordTrustedPayment("checkout-A", PaymentOutcome.Succeeded, SubmittedAt.AddMinutes(18)));
        Assert.Equal(late, store.RecordTrustedPayment("checkout-A", PaymentOutcome.Failed, SubmittedAt.AddMinutes(19)));
        Assert.Equal(late, store.Start("checkout-A", TwoUnits, SubmittedAt.AddMinutes(20)));
        Assert.Equal(0, store.SellableStock);
    }

    [Fact]
    public void Success_at_exact_expiry_is_late_even_without_running_expiry_job()
    {
        var store = new SyntheticCheckoutStore(2);
        store.Start("checkout-A", TwoUnits, SubmittedAt);
        var result = store.RecordTrustedPayment("checkout-A", PaymentOutcome.Succeeded, SubmittedAt.AddMinutes(15));
        Assert.Equal(OrderState.Expired, result.State);
        Assert.True(result.CompensationNeeded);
        Assert.Equal(2, store.SellableStock);
    }

    [Fact]
    public void Trusted_failure_keeps_original_hold_and_later_success_can_confirm()
    {
        var store = new SyntheticCheckoutStore(2);
        store.Start("checkout-A", TwoUnits, SubmittedAt);
        var failed = store.RecordTrustedPayment("checkout-A", PaymentOutcome.Failed, SubmittedAt.AddMinutes(2));
        Assert.Equal(OrderState.Pending, failed.State);
        Assert.Equal(PaymentState.Failed, failed.Payment);
        Assert.Equal(SubmittedAt.AddMinutes(15), failed.ExpiresAt);
        Assert.Equal(0, store.SellableStock);
        var success = store.RecordTrustedPayment("checkout-A", PaymentOutcome.Succeeded, SubmittedAt.AddMinutes(3));
        Assert.Equal(OrderState.Confirmed, success.State);
        Assert.Equal(PaymentState.Succeeded, success.Payment);
        Assert.Equal(0, store.SellableStock);
    }
}
