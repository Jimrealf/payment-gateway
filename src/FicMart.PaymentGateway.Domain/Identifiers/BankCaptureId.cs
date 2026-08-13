using FicMart.PaymentGateway.Domain.Common;

namespace FicMart.PaymentGateway.Domain.Identifiers;

public sealed record BankCaptureId
{
    public const int MaximumLength = 128;

    private BankCaptureId(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static BankCaptureId From(string value) =>
        new(RequiredIdentifier.Validate(value, "Bank capture ID", MaximumLength));
}
