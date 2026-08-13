namespace FicMart.PaymentGateway.Infrastructure.Bank;

public enum BankRejectionCode
{
    InvalidCard = 1,
    InvalidCvv = 2,
    CardExpired = 3,
    InsufficientFunds = 4,
    InvalidAmount = 5,
    Other = 6,
}
