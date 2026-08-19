using FicMart.PaymentGateway.Domain.OperationAttempts;

namespace FicMart.PaymentGateway.Infrastructure.Persistence;

internal sealed class VoidAttemptRecord
{
    private VoidAttemptRecord()
    {
    }

    internal VoidAttemptRecord(VoidAttempt attempt)
    {
        Id = attempt.Id.Value;
        PaymentId = attempt.PaymentId.Value;
        BankIdempotencyKey = attempt.BankIdempotencyKey.Value;
        Status = attempt.Status;
        BankVoidId = attempt.BankVoidId?.Value;
        CreatedAt = attempt.CreatedAt;
        UpdatedAt = attempt.UpdatedAt;
    }

    public Guid Id { get; private set; }
    public Guid PaymentId { get; private set; }
    public Guid BankIdempotencyKey { get; private set; }
    public OperationAttemptStatus Status { get; private set; }
    public string? BankVoidId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    internal void Apply(VoidAttempt attempt)
    {
        Status = attempt.Status;
        BankVoidId = attempt.BankVoidId?.Value;
        UpdatedAt = attempt.UpdatedAt;
    }
}
