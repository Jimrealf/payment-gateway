using FicMart.PaymentGateway.Domain.Identifiers;

namespace FicMart.PaymentGateway.Domain.CaptureAttempts;

public sealed class CaptureAttempt
{
    private CaptureAttempt(
        CaptureAttemptId id,
        PaymentId paymentId,
        BankIdempotencyKey bankIdempotencyKey,
        CaptureAttemptStatus status,
        BankCaptureId? bankCaptureId,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        Id = id;
        PaymentId = paymentId;
        BankIdempotencyKey = bankIdempotencyKey;
        Status = status;
        BankCaptureId = bankCaptureId;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public CaptureAttemptId Id { get; }

    public PaymentId PaymentId { get; }

    public BankIdempotencyKey BankIdempotencyKey { get; }

    public CaptureAttemptStatus Status { get; private set; }

    public BankCaptureId? BankCaptureId { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static CaptureAttempt Create(
        CaptureAttemptId id,
        PaymentId paymentId,
        BankIdempotencyKey bankIdempotencyKey,
        DateTimeOffset createdAt) => new(
            id,
            paymentId,
            bankIdempotencyKey,
            CaptureAttemptStatus.Pending,
            null,
            createdAt,
            createdAt);

    public void MarkSucceeded(BankCaptureId bankCaptureId, DateTimeOffset occurredAt)
    {
        EnsureCanResolve();
        BankCaptureId = bankCaptureId;
        Status = CaptureAttemptStatus.Succeeded;
        UpdatedAt = occurredAt;
    }

    public void MarkRejected(DateTimeOffset occurredAt)
    {
        EnsureCanResolve();
        Status = CaptureAttemptStatus.Rejected;
        UpdatedAt = occurredAt;
    }

    public void MarkUnknown(DateTimeOffset occurredAt)
    {
        if (Status != CaptureAttemptStatus.Pending)
        {
            throw new InvalidCaptureAttemptTransitionException(Status, CaptureAttemptStatus.Unknown);
        }

        Status = CaptureAttemptStatus.Unknown;
        UpdatedAt = occurredAt;
    }

    private void EnsureCanResolve()
    {
        if (Status is not CaptureAttemptStatus.Pending and not CaptureAttemptStatus.Unknown)
        {
            throw new InvalidCaptureAttemptTransitionException(Status, CaptureAttemptStatus.Succeeded);
        }
    }
}
