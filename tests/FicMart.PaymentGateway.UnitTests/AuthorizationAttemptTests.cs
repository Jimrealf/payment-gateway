using FicMart.PaymentGateway.Domain.AuthorizationAttempts;
using FicMart.PaymentGateway.Domain.Identifiers;

namespace FicMart.PaymentGateway.UnitTests;

public sealed class AuthorizationAttemptTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 8, 13, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void UnknownAttemptCanResolveAsSucceeded()
    {
        var attempt = CreateAttempt();
        attempt.MarkUnknown(CreatedAt.AddSeconds(5));

        attempt.MarkSucceeded(
            BankAuthorizationId.From("auth_123"),
            CreatedAt.AddMinutes(1));

        Assert.Equal(AuthorizationAttemptStatus.Succeeded, attempt.Status);
        Assert.Equal("auth_123", attempt.BankAuthorizationId?.Value);
    }

    [Fact]
    public void RejectedAttemptCannotLaterSucceed()
    {
        var attempt = CreateAttempt();
        attempt.MarkRejected(CreatedAt.AddSeconds(1));

        var error = Assert.Throws<InvalidAuthorizationAttemptTransitionException>(() =>
            attempt.MarkSucceeded(
                BankAuthorizationId.From("auth_123"),
                CreatedAt.AddMinutes(1)));

        Assert.Equal(AuthorizationAttemptStatus.Rejected, error.Current);
        Assert.Equal(AuthorizationAttemptStatus.Succeeded, error.Requested);
    }

    private static AuthorizationAttempt CreateAttempt() => AuthorizationAttempt.Create(
        AuthorizationAttemptId.New(),
        PaymentId.New(),
        BankIdempotencyKey.New(),
        CreatedAt);
}
