namespace FicMart.PaymentGateway.Infrastructure.Persistence;

public enum CreatePaymentResult
{
    Created = 1,
    DuplicateIdempotencyKey = 2,
    DuplicateOrder = 3,
}
