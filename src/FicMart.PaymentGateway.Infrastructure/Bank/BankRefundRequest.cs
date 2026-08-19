using FicMart.PaymentGateway.Domain.Identifiers;
using FicMart.PaymentGateway.Domain.Money;

namespace FicMart.PaymentGateway.Infrastructure.Bank;

public sealed record BankRefundRequest(BankCaptureId CaptureId, Money Amount, BankIdempotencyKey IdempotencyKey);
