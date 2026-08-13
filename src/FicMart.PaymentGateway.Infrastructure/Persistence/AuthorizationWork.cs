using FicMart.PaymentGateway.Domain.AuthorizationAttempts;
using FicMart.PaymentGateway.Domain.Payments;

namespace FicMart.PaymentGateway.Infrastructure.Persistence;

public sealed record AuthorizationWork(Payment Payment, AuthorizationAttempt Attempt);
