using FicMart.PaymentGateway.Domain.Identifiers;

namespace FicMart.PaymentGateway.Infrastructure.Bank;

public abstract record BankCaptureResult
{
    private BankCaptureResult()
    {
    }

    public sealed record Captured(BankCaptureId CaptureId) : BankCaptureResult;

    public sealed record Rejected(BankCaptureRejectionCode Code) : BankCaptureResult;

    public sealed record TransientFailure : BankCaptureResult;

    public sealed record Unknown : BankCaptureResult;
}
