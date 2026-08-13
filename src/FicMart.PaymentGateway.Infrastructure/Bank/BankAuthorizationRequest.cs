using FicMart.PaymentGateway.Domain.Identifiers;
using FicMart.PaymentGateway.Domain.Money;

namespace FicMart.PaymentGateway.Infrastructure.Bank;

public sealed record BankAuthorizationRequest(
    string CardNumber,
    string Cvv,
    Money Amount,
    BankIdempotencyKey IdempotencyKey);
