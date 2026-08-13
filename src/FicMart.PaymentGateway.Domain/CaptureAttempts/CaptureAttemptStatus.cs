namespace FicMart.PaymentGateway.Domain.CaptureAttempts;

public enum CaptureAttemptStatus
{
    Pending = 1,
    Succeeded = 2,
    Rejected = 3,
    Unknown = 4,
}
