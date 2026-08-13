using FicMart.PaymentGateway.Domain.CaptureAttempts;
using FicMart.PaymentGateway.Domain.Payments;

namespace FicMart.PaymentGateway.Infrastructure.Persistence;

public sealed record CaptureWork(Payment Payment, CaptureAttempt Attempt);
