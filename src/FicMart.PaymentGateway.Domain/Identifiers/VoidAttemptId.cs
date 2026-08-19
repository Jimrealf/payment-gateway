using FicMart.PaymentGateway.Domain.Common;

namespace FicMart.PaymentGateway.Domain.Identifiers;

public sealed record VoidAttemptId
{
    private VoidAttemptId(Guid value) => Value = value;

    public Guid Value { get; }

    public static VoidAttemptId New() => new(Guid.CreateVersion7());

    public static VoidAttemptId From(Guid value) => value == Guid.Empty
        ? throw new DomainValidationException("Void attempt ID cannot be empty.")
        : new VoidAttemptId(value);
}
