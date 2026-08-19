using FicMart.PaymentGateway.Domain.Identifiers;

namespace FicMart.PaymentGateway.Infrastructure.Bank;

public abstract record BankRefundResult
{
    private BankRefundResult() { }
    public sealed record Refunded(BankRefundId RefundId) : BankRefundResult;
    public sealed record Rejected : BankRefundResult;
    public sealed record TransientFailure : BankRefundResult;
    public sealed record Unknown : BankRefundResult;
}
