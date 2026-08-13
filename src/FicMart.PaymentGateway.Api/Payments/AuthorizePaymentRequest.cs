namespace FicMart.PaymentGateway.Api.Payments;

public sealed record AuthorizePaymentRequest(
    string OrderId,
    string CustomerId,
    long AmountMinorUnits,
    string Currency,
    string CardNumber,
    string Cvv);
