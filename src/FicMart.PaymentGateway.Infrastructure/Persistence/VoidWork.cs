using FicMart.PaymentGateway.Domain.OperationAttempts;
using FicMart.PaymentGateway.Domain.Payments;

namespace FicMart.PaymentGateway.Infrastructure.Persistence;

public sealed record VoidWork(Payment Payment, VoidAttempt Attempt);
