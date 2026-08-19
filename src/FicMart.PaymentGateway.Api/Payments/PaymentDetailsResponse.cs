namespace FicMart.PaymentGateway.Api.Payments;

public sealed record PaymentDetailsResponse(
    Guid PaymentId,
    string OrderId,
    long AmountMinorUnits,
    string Currency,
    string Status,
    bool SafeToShip,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
