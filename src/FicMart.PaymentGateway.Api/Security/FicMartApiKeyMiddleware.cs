using System.Security.Cryptography;
using System.Text;
using FicMart.PaymentGateway.Api.Payments;
using Microsoft.Extensions.Options;

namespace FicMart.PaymentGateway.Api.Security;

public sealed class FicMartApiKeyMiddleware(RequestDelegate next, IOptions<FicMartApiOptions> options)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api/v1"))
        {
            await next(context);
            return;
        }

        var supplied = context.Request.Headers["X-FicMart-Api-Key"].ToString();
        if (!KeysMatch(supplied, options.Value.ApiKey))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(
                new PaymentErrorResponse("unauthorized", "A valid FicMart API key is required."),
                context.RequestAborted);
            return;
        }

        await next(context);
    }

    private static bool KeysMatch(string supplied, string expected)
    {
        var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(supplied));
        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        return CryptographicOperations.FixedTimeEquals(suppliedHash, expectedHash);
    }
}
