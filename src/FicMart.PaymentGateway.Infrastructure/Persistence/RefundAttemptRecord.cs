using FicMart.PaymentGateway.Domain.OperationAttempts;

namespace FicMart.PaymentGateway.Infrastructure.Persistence;

internal sealed class RefundAttemptRecord
{
    private RefundAttemptRecord()
    {
    }

    internal RefundAttemptRecord(RefundAttempt attempt)
    {
        Id = attempt.Id.Value;
        PaymentId = attempt.PaymentId.Value;
        BankIdempotencyKey = attempt.BankIdempotencyKey.Value;
        Status = attempt.Status;
        BankRefundId = attempt.BankRefundId?.Value;
        CreatedAt = attempt.CreatedAt;
        UpdatedAt = attempt.UpdatedAt;
    }

    public Guid Id { get; private set; }
    public Guid PaymentId { get; private set; }
    public Guid BankIdempotencyKey { get; private set; }
    public OperationAttemptStatus Status { get; private set; }
    public string? BankRefundId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    internal void Apply(RefundAttempt attempt)
    {
        Status = attempt.Status;
        BankRefundId = attempt.BankRefundId?.Value;
        UpdatedAt = attempt.UpdatedAt;
    }
}
