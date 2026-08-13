using FicMart.PaymentGateway.Domain.Common;

namespace FicMart.PaymentGateway.Domain.Identifiers;

public sealed record GatewayIdempotencyKey
{
    public const int MaximumLength = 128;

    private GatewayIdempotencyKey(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static GatewayIdempotencyKey From(string value) =>
        new(RequiredIdentifier.Validate(value, "Idempotency key", MaximumLength));
}
