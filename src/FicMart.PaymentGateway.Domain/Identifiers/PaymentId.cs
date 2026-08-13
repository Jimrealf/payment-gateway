using FicMart.PaymentGateway.Domain.Common;

namespace FicMart.PaymentGateway.Domain.Identifiers;

public sealed record PaymentId
{
    private PaymentId(Guid value)
    {
        Value = value;
    }

    public Guid Value { get; }

    public static PaymentId New() => new(Guid.CreateVersion7());

    public static PaymentId From(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new DomainValidationException("Payment ID cannot be empty.");
        }

        return new PaymentId(value);
    }
}
