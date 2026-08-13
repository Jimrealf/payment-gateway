using FicMart.PaymentGateway.Domain.Identifiers;
using PaymentMoney = FicMart.PaymentGateway.Domain.Money.Money;

namespace FicMart.PaymentGateway.Domain.Payments;

public sealed class Payment
{
    private Payment(
        PaymentId id,
        OrderId orderId,
        CustomerId customerId,
        PaymentMoney amount,
        PaymentStatus status,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        Id = id;
        OrderId = orderId;
        CustomerId = customerId;
        Amount = amount;
        Status = status;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public PaymentId Id { get; }

    public OrderId OrderId { get; }

    public CustomerId CustomerId { get; }

    public PaymentMoney Amount { get; }

    public PaymentStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static Payment Create(
        PaymentId id,
        OrderId orderId,
        CustomerId customerId,
        PaymentMoney amount,
        DateTimeOffset createdAt) => new(
            id,
            orderId,
            customerId,
            amount,
            PaymentStatus.PendingAuthorization,
            createdAt,
            createdAt);

    public static Payment Restore(
        PaymentId id,
        OrderId orderId,
        CustomerId customerId,
        PaymentMoney amount,
        PaymentStatus status,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "Payment status is invalid.");
        }

        return new Payment(id, orderId, customerId, amount, status, createdAt, updatedAt);
    }

    public void MarkAuthorized(DateTimeOffset occurredAt) =>
        TransitionFrom(PaymentStatus.PendingAuthorization, PaymentStatus.Authorized, occurredAt);

    public void MarkDeclined(DateTimeOffset occurredAt) =>
        TransitionFrom(PaymentStatus.PendingAuthorization, PaymentStatus.Declined, occurredAt);

    public void MarkCaptured(DateTimeOffset occurredAt) =>
        TransitionFrom(PaymentStatus.Authorized, PaymentStatus.Captured, occurredAt);

    public void MarkVoided(DateTimeOffset occurredAt) =>
        TransitionFrom(PaymentStatus.Authorized, PaymentStatus.Voided, occurredAt);

    public void MarkRefunded(DateTimeOffset occurredAt) =>
        TransitionFrom(PaymentStatus.Captured, PaymentStatus.Refunded, occurredAt);

    private void TransitionFrom(PaymentStatus expected, PaymentStatus requested, DateTimeOffset occurredAt)
    {
        if (Status != expected)
        {
            throw new InvalidPaymentTransitionException(Status, requested);
        }

        Status = requested;
        UpdatedAt = occurredAt;
    }
}
