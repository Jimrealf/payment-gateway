using FicMart.PaymentGateway.Domain.Identifiers;

namespace FicMart.PaymentGateway.Domain.OperationAttempts;

public sealed class VoidAttempt
{
    private VoidAttempt(
        VoidAttemptId id,
        PaymentId paymentId,
        BankIdempotencyKey bankIdempotencyKey,
        OperationAttemptStatus status,
        BankVoidId? bankVoidId,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        Id = id;
        PaymentId = paymentId;
        BankIdempotencyKey = bankIdempotencyKey;
        Status = status;
        BankVoidId = bankVoidId;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public VoidAttemptId Id { get; }
    public PaymentId PaymentId { get; }
    public BankIdempotencyKey BankIdempotencyKey { get; }
    public OperationAttemptStatus Status { get; private set; }
    public BankVoidId? BankVoidId { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static VoidAttempt Create(VoidAttemptId id, PaymentId paymentId, BankIdempotencyKey key, DateTimeOffset now) =>
        new(id, paymentId, key, OperationAttemptStatus.Pending, null, now, now);

    public static VoidAttempt Restore(VoidAttemptId id, PaymentId paymentId, BankIdempotencyKey key,
        OperationAttemptStatus status, BankVoidId? bankVoidId, DateTimeOffset createdAt, DateTimeOffset updatedAt) =>
        new(id, paymentId, key, status, bankVoidId, createdAt, updatedAt);

    public void MarkSucceeded(BankVoidId bankVoidId, DateTimeOffset now)
    {
        EnsureResolvable();
        BankVoidId = bankVoidId;
        Status = OperationAttemptStatus.Succeeded;
        UpdatedAt = now;
    }

    public void MarkRejected(DateTimeOffset now)
    {
        EnsureResolvable();
        Status = OperationAttemptStatus.Rejected;
        UpdatedAt = now;
    }

    public void MarkUnknown(DateTimeOffset now)
    {
        if (Status != OperationAttemptStatus.Pending)
        {
            throw new InvalidOperationException($"Void attempt cannot transition from {Status} to Unknown.");
        }
        Status = OperationAttemptStatus.Unknown;
        UpdatedAt = now;
    }

    private void EnsureResolvable()
    {
        if (Status is not OperationAttemptStatus.Pending and not OperationAttemptStatus.Unknown)
        {
            throw new InvalidOperationException($"Void attempt cannot resolve from {Status}.");
        }
    }
}
