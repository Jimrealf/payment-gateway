using FicMart.PaymentGateway.Domain.Payments;

namespace FicMart.PaymentGateway.Infrastructure.Persistence;

internal sealed class PaymentRecord
{
    private PaymentRecord()
    {
    }

    internal PaymentRecord(Payment payment)
    {
        Id = payment.Id.Value;
        OrderId = payment.OrderId.Value;
        CustomerId = payment.CustomerId.Value;
        AmountMinorUnits = payment.Amount.MinorUnits;
        Currency = "USD";
        Status = payment.Status;
        CreatedAt = payment.CreatedAt;
        UpdatedAt = payment.UpdatedAt;
    }

    public Guid Id { get; private set; }

    public string OrderId { get; private set; } = string.Empty;

    public string CustomerId { get; private set; } = string.Empty;

    public long AmountMinorUnits { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public PaymentStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    internal void Apply(Payment payment)
    {
        Status = payment.Status;
        UpdatedAt = payment.UpdatedAt;
    }
}
