namespace FicMart.PaymentGateway.Domain.Payments;

public sealed class InvalidPaymentTransitionException(PaymentStatus current, PaymentStatus requested)
    : Exception($"Payment cannot transition from {current} to {requested}.")
{
    public PaymentStatus Current { get; } = current;

    public PaymentStatus Requested { get; } = requested;
}
