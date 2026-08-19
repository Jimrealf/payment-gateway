using FicMart.PaymentGateway.Domain.Identifiers;

namespace FicMart.PaymentGateway.Infrastructure.Persistence;

public sealed record ReconciliationCandidate(
    PaymentId PaymentId,
    IdempotencyOperation Operation,
    GatewayIdempotencyKey IdempotencyKey);
