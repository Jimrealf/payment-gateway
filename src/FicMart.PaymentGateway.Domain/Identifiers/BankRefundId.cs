using FicMart.PaymentGateway.Domain.Common;

namespace FicMart.PaymentGateway.Domain.Identifiers;

public sealed record BankRefundId
{
    public const int MaximumLength = 128;

    private BankRefundId(string value) => Value = value;

    public string Value { get; }

    public static BankRefundId From(string value) =>
        new(RequiredIdentifier.Validate(value, "Bank refund ID", MaximumLength));
}
