using FicMart.PaymentGateway.Domain.Common;

namespace FicMart.PaymentGateway.Domain.Identifiers;

public sealed record BankVoidId
{
    public const int MaximumLength = 128;

    private BankVoidId(string value) => Value = value;

    public string Value { get; }

    public static BankVoidId From(string value) =>
        new(RequiredIdentifier.Validate(value, "Bank void ID", MaximumLength));
}
