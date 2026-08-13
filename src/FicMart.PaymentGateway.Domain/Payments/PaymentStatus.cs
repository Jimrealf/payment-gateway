namespace FicMart.PaymentGateway.Domain.Payments;

public enum PaymentStatus
{
    PendingAuthorization = 1,
    Authorized = 2,
    Declined = 3,
    Captured = 4,
    Voided = 5,
    Refunded = 6,
}
