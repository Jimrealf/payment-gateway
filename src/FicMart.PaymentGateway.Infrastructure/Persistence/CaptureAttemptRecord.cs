using FicMart.PaymentGateway.Domain.CaptureAttempts;

namespace FicMart.PaymentGateway.Infrastructure.Persistence;

internal sealed class CaptureAttemptRecord
{
    private CaptureAttemptRecord()
    {
    }

    internal CaptureAttemptRecord(CaptureAttempt attempt)
    {
        Id = attempt.Id.Value;
        PaymentId = attempt.PaymentId.Value;
        BankIdempotencyKey = attempt.BankIdempotencyKey.Value;
        Status = attempt.Status;
        BankCaptureId = attempt.BankCaptureId?.Value;
        CreatedAt = attempt.CreatedAt;
        UpdatedAt = attempt.UpdatedAt;
    }

    public Guid Id { get; private set; }

    public Guid PaymentId { get; private set; }

    public Guid BankIdempotencyKey { get; private set; }

    public CaptureAttemptStatus Status { get; private set; }

    public string? BankCaptureId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    internal void Apply(CaptureAttempt attempt)
    {
        Status = attempt.Status;
        BankCaptureId = attempt.BankCaptureId?.Value;
        UpdatedAt = attempt.UpdatedAt;
    }
}
