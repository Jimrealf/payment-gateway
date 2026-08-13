using FicMart.PaymentGateway.Domain.AuthorizationAttempts;

namespace FicMart.PaymentGateway.Infrastructure.Persistence;

internal sealed class AuthorizationAttemptRecord
{
    private AuthorizationAttemptRecord()
    {
    }

    internal AuthorizationAttemptRecord(AuthorizationAttempt attempt)
    {
        Id = attempt.Id.Value;
        PaymentId = attempt.PaymentId.Value;
        BankIdempotencyKey = attempt.BankIdempotencyKey.Value;
        Status = attempt.Status;
        BankAuthorizationId = attempt.BankAuthorizationId?.Value;
        CreatedAt = attempt.CreatedAt;
        UpdatedAt = attempt.UpdatedAt;
    }

    public Guid Id { get; private set; }

    public Guid PaymentId { get; private set; }

    public Guid BankIdempotencyKey { get; private set; }

    public AuthorizationAttemptStatus Status { get; private set; }

    public string? BankAuthorizationId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }
}
