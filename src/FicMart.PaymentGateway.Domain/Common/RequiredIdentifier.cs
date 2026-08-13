namespace FicMart.PaymentGateway.Domain.Common;

internal static class RequiredIdentifier
{
    internal static string Validate(string value, string name, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainValidationException($"{name} is required.");
        }

        if (value.Length > maximumLength)
        {
            throw new DomainValidationException(
                $"{name} cannot exceed {maximumLength} characters.");
        }

        return value;
    }
}
