namespace FicMart.PaymentGateway.Api.Payments;

public sealed record PaymentResponse(
    Guid PaymentId,
    string OrderId,
    string Status);
