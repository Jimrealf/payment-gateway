namespace FicMart.PaymentGateway.Infrastructure.Persistence;

public enum CreateOperationResult
{
    Created = 1,
    DuplicateIdempotencyKey = 2,
    OperationAlreadyExists = 3,
}
