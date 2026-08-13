using FicMart.PaymentGateway.Domain.Common;

namespace FicMart.PaymentGateway.Domain.Identifiers;

public sealed record OrderId
{
    public const int MaximumLength = 128;

    private OrderId(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static OrderId From(string value) =>
        new(RequiredIdentifier.Validate(value, "Order ID", MaximumLength));
}
