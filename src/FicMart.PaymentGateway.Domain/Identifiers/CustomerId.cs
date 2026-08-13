using FicMart.PaymentGateway.Domain.Common;

namespace FicMart.PaymentGateway.Domain.Identifiers;

public sealed record CustomerId
{
    public const int MaximumLength = 128;

    private CustomerId(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static CustomerId From(string value) =>
        new(RequiredIdentifier.Validate(value, "Customer ID", MaximumLength));
}
