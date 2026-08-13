namespace FicMart.PaymentGateway.Api.Payments;

internal static class PaymentRequestValidator
{
    internal static string? Validate(AuthorizePaymentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.OrderId) ||
            string.IsNullOrWhiteSpace(request.CustomerId))
        {
            return "Order ID and customer ID are required.";
        }

        if (request.AmountMinorUnits <= 0 || request.Currency != "USD")
        {
            return "A positive amount in USD minor units is required.";
        }

        if (!IsValidCardNumber(request.CardNumber))
        {
            return "Card number is invalid.";
        }

        if (request.Cvv.Length is < 3 or > 4 || request.Cvv.Any(character => !char.IsAsciiDigit(character)))
        {
            return "CVV is invalid.";
        }

        return null;
    }

    private static bool IsValidCardNumber(string cardNumber)
    {
        if (cardNumber.Length is < 13 or > 19 || cardNumber.Any(character => !char.IsAsciiDigit(character)))
        {
            return false;
        }

        var sum = 0;
        var shouldDouble = false;
        for (var index = cardNumber.Length - 1; index >= 0; index--)
        {
            var digit = cardNumber[index] - '0';
            if (shouldDouble)
            {
                digit *= 2;
                if (digit > 9)
                {
                    digit -= 9;
                }
            }

            sum += digit;
            shouldDouble = !shouldDouble;
        }

        return sum % 10 == 0;
    }
}
