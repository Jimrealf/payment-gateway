using FicMart.PaymentGateway.Domain.Identifiers;
using FicMart.PaymentGateway.Domain.Money;

namespace FicMart.PaymentGateway.Infrastructure.Bank;

public sealed record BankCaptureRequest(
    BankAuthorizationId AuthorizationId,
    Money Amount,
    BankIdempotencyKey IdempotencyKey);
