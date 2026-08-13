namespace FicMart.PaymentGateway.Domain.Common;

public sealed class DomainValidationException(string message) : Exception(message);
