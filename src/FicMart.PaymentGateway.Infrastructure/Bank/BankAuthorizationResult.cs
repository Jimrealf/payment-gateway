using FicMart.PaymentGateway.Domain.Identifiers;

namespace FicMart.PaymentGateway.Infrastructure.Bank;

public abstract record BankAuthorizationResult
{
    private BankAuthorizationResult()
    {
    }

    public sealed record Approved(
        BankAuthorizationId AuthorizationId,
        long AmountMinorUnits,
        DateTimeOffset ExpiresAt) : BankAuthorizationResult;

    public sealed record Rejected(BankRejectionCode Code) : BankAuthorizationResult;

    public sealed record TransientFailure : BankAuthorizationResult;

    public sealed record Unknown : BankAuthorizationResult;
}
