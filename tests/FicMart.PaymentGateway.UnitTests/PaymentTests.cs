using FicMart.PaymentGateway.Domain.Identifiers;
using FicMart.PaymentGateway.Domain.Money;
using FicMart.PaymentGateway.Domain.Payments;

namespace FicMart.PaymentGateway.UnitTests;

public sealed class PaymentTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 8, 13, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void PaymentCanCompleteCaptureAndRefundLifecycle()
    {
        var payment = CreatePayment();

        payment.MarkAuthorized(CreatedAt.AddMinutes(1));
        payment.MarkCaptured(CreatedAt.AddMinutes(2));
        payment.MarkRefunded(CreatedAt.AddMinutes(3));

        Assert.Equal(PaymentStatus.Refunded, payment.Status);
        Assert.Equal(CreatedAt.AddMinutes(3), payment.UpdatedAt);
    }

    [Fact]
    public void AuthorizedPaymentCanBeVoided()
    {
        var payment = CreatePayment();
        payment.MarkAuthorized(CreatedAt.AddMinutes(1));

        payment.MarkVoided(CreatedAt.AddMinutes(2));

        Assert.Equal(PaymentStatus.Voided, payment.Status);
    }

    [Fact]
    public void PendingAuthorizationCanBeDeclined()
    {
        var payment = CreatePayment();

        payment.MarkDeclined(CreatedAt.AddMinutes(1));

        Assert.Equal(PaymentStatus.Declined, payment.Status);
    }

    [Fact]
    public void PendingPaymentCannotBeCaptured()
    {
        var payment = CreatePayment();

        var error = Assert.Throws<InvalidPaymentTransitionException>(
            () => payment.MarkCaptured(CreatedAt.AddMinutes(1)));

        Assert.Equal(PaymentStatus.PendingAuthorization, error.Current);
        Assert.Equal(PaymentStatus.Captured, error.Requested);
    }

    private static Payment CreatePayment() => Payment.Create(
        PaymentId.New(),
        OrderId.From("order-1001"),
        CustomerId.From("customer-1001"),
        Money.Usd(5000),
        CreatedAt);
}
