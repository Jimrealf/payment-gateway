using FicMart.PaymentGateway.Domain.Common;

namespace FicMart.PaymentGateway.Domain.Identifiers;

public sealed record RefundAttemptId
{
    private RefundAttemptId(Guid value) => Value = value;

    public Guid Value { get; }

    public static RefundAttemptId New() => new(Guid.CreateVersion7());

    public static RefundAttemptId From(Guid value) => value == Guid.Empty
        ? throw new DomainValidationException("Refund attempt ID cannot be empty.")
        : new RefundAttemptId(value);
}
