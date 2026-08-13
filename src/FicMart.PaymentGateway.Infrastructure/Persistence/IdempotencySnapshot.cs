using FicMart.PaymentGateway.Domain.Identifiers;

namespace FicMart.PaymentGateway.Infrastructure.Persistence;

public sealed record IdempotencySnapshot(
    GatewayIdempotencyKey Key,
    IdempotencyOperation Operation,
    string RequestFingerprint,
    PaymentId PaymentId,
    IdempotencyState State);
