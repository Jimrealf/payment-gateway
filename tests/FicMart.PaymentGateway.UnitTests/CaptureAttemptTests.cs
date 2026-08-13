using FicMart.PaymentGateway.Domain.CaptureAttempts;
using FicMart.PaymentGateway.Domain.Identifiers;

namespace FicMart.PaymentGateway.UnitTests;

public sealed class CaptureAttemptTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 8, 13, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void UnknownCaptureCanResolveAsSucceeded()
    {
        var attempt = CreateAttempt();
        attempt.MarkUnknown(CreatedAt.AddSeconds(5));

        attempt.MarkSucceeded(BankCaptureId.From("cap_123"), CreatedAt.AddMinutes(1));

        Assert.Equal(CaptureAttemptStatus.Succeeded, attempt.Status);
        Assert.Equal("cap_123", attempt.BankCaptureId?.Value);
    }

    [Fact]
    public void RejectedCaptureCannotLaterSucceed()
    {
        var attempt = CreateAttempt();
        attempt.MarkRejected(CreatedAt.AddSeconds(1));

        Assert.Throws<InvalidCaptureAttemptTransitionException>(() =>
            attempt.MarkSucceeded(BankCaptureId.From("cap_123"), CreatedAt.AddMinutes(1)));
    }

    private static CaptureAttempt CreateAttempt() => CaptureAttempt.Create(
        CaptureAttemptId.New(),
        PaymentId.New(),
        BankIdempotencyKey.New(),
        CreatedAt);
}
