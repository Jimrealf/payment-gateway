namespace FicMart.PaymentGateway.Infrastructure.Persistence;

public enum IdempotencyState
{
    Processing = 1,
    Retryable = 2,
    Completed = 3,
}
