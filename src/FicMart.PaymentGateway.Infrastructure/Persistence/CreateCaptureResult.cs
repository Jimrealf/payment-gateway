namespace FicMart.PaymentGateway.Infrastructure.Persistence;

public enum CreateCaptureResult
{
    Created = 1,
    DuplicateIdempotencyKey = 2,
    CaptureAlreadyExists = 3,
}
