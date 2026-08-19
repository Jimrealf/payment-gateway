using FicMart.PaymentGateway.Domain.Identifiers;

namespace FicMart.PaymentGateway.Infrastructure.Bank;

public sealed record BankVoidRequest(BankAuthorizationId AuthorizationId, BankIdempotencyKey IdempotencyKey);
