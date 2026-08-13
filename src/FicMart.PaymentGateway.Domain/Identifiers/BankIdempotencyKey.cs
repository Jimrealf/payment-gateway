using FicMart.PaymentGateway.Domain.Common;

namespace FicMart.PaymentGateway.Domain.Identifiers;

public sealed record BankIdempotencyKey
{
    private BankIdempotencyKey(Guid value)
    {
        Value = value;
    }

    public Guid Value { get; }

    public static BankIdempotencyKey New() => new(Guid.CreateVersion7());

    public static BankIdempotencyKey From(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new DomainValidationException("Bank idempotency key cannot be empty.");
        }

        return new BankIdempotencyKey(value);
    }
}
