namespace FicMart.PaymentGateway.Domain.OperationAttempts;

public enum OperationAttemptStatus
{
    Pending = 1,
    Succeeded = 2,
    Rejected = 3,
    Unknown = 4,
}
