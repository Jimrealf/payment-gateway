using FicMart.PaymentGateway.Domain.Identifiers;

namespace FicMart.PaymentGateway.Infrastructure.Bank;

public abstract record BankVoidResult
{
    private BankVoidResult() { }
    public sealed record Voided(BankVoidId VoidId) : BankVoidResult;
    public sealed record Rejected : BankVoidResult;
    public sealed record TransientFailure : BankVoidResult;
    public sealed record Unknown : BankVoidResult;
}
