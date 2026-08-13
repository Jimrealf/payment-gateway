namespace FicMart.PaymentGateway.Infrastructure.Persistence;

public enum IdempotencyOperation
{
    Authorize = 1,
    Capture = 2,
}
