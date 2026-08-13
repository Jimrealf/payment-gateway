using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace FicMart.PaymentGateway.Api.Payments;

public sealed class RequestFingerprint(IOptions<IdempotencyOptions> options)
{
    private readonly byte[] secret = Encoding.UTF8.GetBytes(options.Value.FingerprintSecret);

    public string ForAuthorization(AuthorizePaymentRequest request)
    {
        var canonicalRequest = string.Join(
            '\n',
            request.OrderId,
            request.CustomerId,
            request.AmountMinorUnits,
            request.Currency,
            request.CardNumber);
        return Convert.ToHexString(HMACSHA256.HashData(
            secret,
            Encoding.UTF8.GetBytes(canonicalRequest)));
    }

    public string ForCapture(Guid paymentId) => Convert.ToHexString(HMACSHA256.HashData(
        secret,
        Encoding.UTF8.GetBytes(paymentId.ToString())));
}
