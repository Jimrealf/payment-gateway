using System.Text.RegularExpressions;

namespace FicMart.PaymentGateway.Api.Observability;

public sealed partial class CorrelationMiddleware(RequestDelegate next, ILogger<CorrelationMiddleware> logger)
{
    private const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var supplied = context.Request.Headers[HeaderName].ToString();
        var correlationId = supplied.Length <= 64 && CorrelationIdPattern().IsMatch(supplied)
            ? supplied
            : context.TraceIdentifier;
        context.Response.Headers[HeaderName] = correlationId;
        using (logger.BeginScope(new Dictionary<string, string> { ["CorrelationId"] = correlationId }))
        {
            await next(context);
        }
    }

    [GeneratedRegex("^[A-Za-z0-9._:-]+$")]
    private static partial Regex CorrelationIdPattern();
}
