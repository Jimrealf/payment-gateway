namespace FicMart.PaymentGateway.Domain.AuthorizationAttempts;

public enum AuthorizationAttemptStatus
{
    Pending = 1,
    Succeeded = 2,
    Rejected = 3,
    Unknown = 4,
}
