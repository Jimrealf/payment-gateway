using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace FicMart.PaymentGateway.Api.Payments;

public sealed class RequestFingerprint(IOptions<IdempotencyOptions> options)
{
    private readonly byte[] secret = Encoding.UTF8.GetBytes(options.Value.FingerprintSecret);

    public string ForAuthorization(AuthorizePaymentRequest request)
    {
        var canonicalRequest = Canonicalize(
            request.OrderId,
            request.CustomerId,
            request.AmountMinorUnits.ToString(System.Globalization.CultureInfo.InvariantCulture),
            request.Currency,
            request.CardNumber);
        return Convert.ToHexString(HMACSHA256.HashData(
            secret,
            Encoding.UTF8.GetBytes(canonicalRequest)));
    }

    public string ForCapture(Guid paymentId) => Convert.ToHexString(HMACSHA256.HashData(
        secret,
        Encoding.UTF8.GetBytes($"capture\n{paymentId}")));

    public string ForVoid(Guid paymentId) => Convert.ToHexString(HMACSHA256.HashData(
        secret,
        Encoding.UTF8.GetBytes($"void\n{paymentId}")));

    public string ForRefund(Guid paymentId) => Convert.ToHexString(HMACSHA256.HashData(
        secret,
        Encoding.UTF8.GetBytes($"refund\n{paymentId}")));

    private static string Canonicalize(params string[] values) => string.Concat(
        values.Select(value => $"{value.Length}:{value}"));
}
