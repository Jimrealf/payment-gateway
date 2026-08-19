namespace FicMart.PaymentGateway.Infrastructure.Persistence;

internal sealed class AuditRecord
{
    private AuditRecord()
    {
    }

    internal AuditRecord(
        Guid id,
        Guid paymentId,
        string eventType,
        string previousStatus,
        string newStatus,
        string actor,
        DateTimeOffset occurredAt)
    {
        Id = id;
        PaymentId = paymentId;
        EventType = eventType;
        PreviousStatus = previousStatus;
        NewStatus = newStatus;
        Actor = actor;
        OccurredAt = occurredAt;
    }

    public Guid Id { get; private set; }
    public Guid PaymentId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string PreviousStatus { get; private set; } = string.Empty;
    public string NewStatus { get; private set; } = string.Empty;
    public string Actor { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; private set; }
}
