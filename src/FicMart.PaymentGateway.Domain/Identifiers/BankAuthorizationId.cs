using FicMart.PaymentGateway.Domain.Common;

namespace FicMart.PaymentGateway.Domain.Identifiers;

public sealed record BankAuthorizationId
{
    public const int MaximumLength = 128;

    private BankAuthorizationId(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static BankAuthorizationId From(string value) =>
        new(RequiredIdentifier.Validate(value, "Bank authorization ID", MaximumLength));
}
