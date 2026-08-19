using FicMart.PaymentGateway.Domain.Identifiers;

namespace FicMart.PaymentGateway.Domain.OperationAttempts;

public sealed class RefundAttempt
{
    private RefundAttempt(RefundAttemptId id, PaymentId paymentId, BankIdempotencyKey key,
        OperationAttemptStatus status, BankRefundId? bankRefundId, DateTimeOffset createdAt, DateTimeOffset updatedAt)
    {
        Id = id;
        PaymentId = paymentId;
        BankIdempotencyKey = key;
        Status = status;
        BankRefundId = bankRefundId;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public RefundAttemptId Id { get; }
    public PaymentId PaymentId { get; }
    public BankIdempotencyKey BankIdempotencyKey { get; }
    public OperationAttemptStatus Status { get; private set; }
    public BankRefundId? BankRefundId { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static RefundAttempt Create(RefundAttemptId id, PaymentId paymentId, BankIdempotencyKey key, DateTimeOffset now) =>
        new(id, paymentId, key, OperationAttemptStatus.Pending, null, now, now);

    public static RefundAttempt Restore(RefundAttemptId id, PaymentId paymentId, BankIdempotencyKey key,
        OperationAttemptStatus status, BankRefundId? bankRefundId, DateTimeOffset createdAt, DateTimeOffset updatedAt) =>
        new(id, paymentId, key, status, bankRefundId, createdAt, updatedAt);

    public void MarkSucceeded(BankRefundId bankRefundId, DateTimeOffset now)
    {
        EnsureResolvable();
        BankRefundId = bankRefundId;
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
            throw new InvalidOperationException($"Refund attempt cannot transition from {Status} to Unknown.");
        }
        Status = OperationAttemptStatus.Unknown;
        UpdatedAt = now;
    }

    private void EnsureResolvable()
    {
        if (Status is not OperationAttemptStatus.Pending and not OperationAttemptStatus.Unknown)
        {
            throw new InvalidOperationException($"Refund attempt cannot resolve from {Status}.");
        }
    }
}
