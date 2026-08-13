using FicMart.PaymentGateway.Domain.Identifiers;

namespace FicMart.PaymentGateway.Domain.AuthorizationAttempts;

public sealed class AuthorizationAttempt
{
    private AuthorizationAttempt(
        AuthorizationAttemptId id,
        PaymentId paymentId,
        BankIdempotencyKey bankIdempotencyKey,
        AuthorizationAttemptStatus status,
        BankAuthorizationId? bankAuthorizationId,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        Id = id;
        PaymentId = paymentId;
        BankIdempotencyKey = bankIdempotencyKey;
        Status = status;
        BankAuthorizationId = bankAuthorizationId;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public AuthorizationAttemptId Id { get; }

    public PaymentId PaymentId { get; }

    public BankIdempotencyKey BankIdempotencyKey { get; }

    public AuthorizationAttemptStatus Status { get; private set; }

    public BankAuthorizationId? BankAuthorizationId { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static AuthorizationAttempt Create(
        AuthorizationAttemptId id,
        PaymentId paymentId,
        BankIdempotencyKey bankIdempotencyKey,
        DateTimeOffset createdAt) => new(
            id,
            paymentId,
            bankIdempotencyKey,
            AuthorizationAttemptStatus.Pending,
            null,
            createdAt,
            createdAt);

    public static AuthorizationAttempt Restore(
        AuthorizationAttemptId id,
        PaymentId paymentId,
        BankIdempotencyKey bankIdempotencyKey,
        AuthorizationAttemptStatus status,
        BankAuthorizationId? bankAuthorizationId,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "Authorization attempt status is invalid.");
        }

        if ((status == AuthorizationAttemptStatus.Succeeded) != (bankAuthorizationId is not null))
        {
            throw new ArgumentException(
                "A bank authorization ID is required only for a succeeded authorization attempt.",
                nameof(bankAuthorizationId));
        }

        return new AuthorizationAttempt(
            id,
            paymentId,
            bankIdempotencyKey,
            status,
            bankAuthorizationId,
            createdAt,
            updatedAt);
    }

    public void MarkSucceeded(BankAuthorizationId bankAuthorizationId, DateTimeOffset occurredAt)
    {
        EnsureCanResolve(AuthorizationAttemptStatus.Succeeded);
        BankAuthorizationId = bankAuthorizationId;
        Status = AuthorizationAttemptStatus.Succeeded;
        UpdatedAt = occurredAt;
    }

    public void MarkRejected(DateTimeOffset occurredAt)
    {
        EnsureCanResolve(AuthorizationAttemptStatus.Rejected);
        Status = AuthorizationAttemptStatus.Rejected;
        UpdatedAt = occurredAt;
    }

    public void MarkUnknown(DateTimeOffset occurredAt)
    {
        if (Status != AuthorizationAttemptStatus.Pending)
        {
            throw new InvalidAuthorizationAttemptTransitionException(
                Status,
                AuthorizationAttemptStatus.Unknown);
        }

        Status = AuthorizationAttemptStatus.Unknown;
        UpdatedAt = occurredAt;
    }

    private void EnsureCanResolve(AuthorizationAttemptStatus requested)
    {
        if (Status is not AuthorizationAttemptStatus.Pending and not AuthorizationAttemptStatus.Unknown)
        {
            throw new InvalidAuthorizationAttemptTransitionException(Status, requested);
        }
    }
}
