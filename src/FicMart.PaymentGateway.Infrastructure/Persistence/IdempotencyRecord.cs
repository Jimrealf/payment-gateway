namespace FicMart.PaymentGateway.Infrastructure.Persistence;

internal sealed class IdempotencyRecord
{
    private IdempotencyRecord()
    {
    }

    internal IdempotencyRecord(
        string key,
        IdempotencyOperation operation,
        string requestFingerprint,
        Guid paymentId,
        DateTimeOffset createdAt)
    {
        Key = key;
        Operation = operation;
        RequestFingerprint = requestFingerprint;
        PaymentId = paymentId;
        State = IdempotencyState.Processing;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public string Key { get; private set; } = string.Empty;

    public IdempotencyOperation Operation { get; private set; }

    public string RequestFingerprint { get; private set; } = string.Empty;

    public Guid PaymentId { get; private set; }

    public IdempotencyState State { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    internal void MarkCompleted(DateTimeOffset occurredAt)
    {
        State = IdempotencyState.Completed;
        UpdatedAt = occurredAt;
    }

    internal void MarkRetryable(DateTimeOffset occurredAt)
    {
        State = IdempotencyState.Retryable;
        UpdatedAt = occurredAt;
    }
}
