namespace FicMart.PaymentGateway.Api.Payments;

public sealed record PaymentErrorResponse(
    string Code,
    string Message,
    Guid? PaymentId = null,
    string? Status = null);
