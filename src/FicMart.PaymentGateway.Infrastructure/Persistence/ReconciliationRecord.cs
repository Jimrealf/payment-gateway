namespace FicMart.PaymentGateway.Infrastructure.Persistence;

internal sealed class ReconciliationRecord
{
    private ReconciliationRecord()
    {
    }

    internal ReconciliationRecord(
        Guid id,
        Guid paymentId,
        IdempotencyOperation? operation,
        ReconciliationOutcome outcome,
        string detailCode,
        DateTimeOffset createdAt)
    {
        Id = id;
        PaymentId = paymentId;
        Operation = operation;
        Outcome = outcome;
        DetailCode = detailCode;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid PaymentId { get; private set; }
    public IdempotencyOperation? Operation { get; private set; }
    public ReconciliationOutcome Outcome { get; private set; }
    public string DetailCode { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
}
