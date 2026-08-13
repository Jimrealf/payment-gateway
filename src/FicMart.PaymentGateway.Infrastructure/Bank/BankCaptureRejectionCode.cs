namespace FicMart.PaymentGateway.Infrastructure.Bank;

public enum BankCaptureRejectionCode
{
    AuthorizationNotFound = 1,
    AuthorizationExpired = 2,
    AuthorizationAlreadyUsed = 3,
    AmountMismatch = 4,
    Other = 5,
}
