namespace FicMart.PaymentGateway.Api.Payments;

public sealed record ReconciliationResponse(
    Guid PaymentId,
    string Outcome,
    string DetailCode,
    PaymentResponse? Payment);
