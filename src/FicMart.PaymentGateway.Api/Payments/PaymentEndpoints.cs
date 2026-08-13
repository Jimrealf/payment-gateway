using FicMart.PaymentGateway.Domain.Common;
using FicMart.PaymentGateway.Domain.Identifiers;

namespace FicMart.PaymentGateway.Api.Payments;

public static class PaymentEndpoints
{
    public static IEndpointRouteBuilder MapPaymentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/payments/authorize", AuthorizeAsync);
        endpoints.MapPost("/api/v1/payments/{paymentId:guid}/capture", CaptureAsync);
        return endpoints;
    }

    private static async Task<IResult> AuthorizeAsync(
        AuthorizePaymentRequest request,
        HttpRequest httpRequest,
        PaymentService paymentService,
        CancellationToken cancellationToken)
    {
        var keyResult = ReadIdempotencyKey(httpRequest);
        if (keyResult.Error is not null)
        {
            return Results.Json(keyResult.Error, statusCode: 400);
        }

        var result = await paymentService.AuthorizeAsync(
            request,
            keyResult.Key!,
            cancellationToken);
        return ToHttpResult(result);
    }

    private static async Task<IResult> CaptureAsync(
        Guid paymentId,
        HttpRequest httpRequest,
        PaymentService paymentService,
        CancellationToken cancellationToken)
    {
        var keyResult = ReadIdempotencyKey(httpRequest);
        if (keyResult.Error is not null)
        {
            return Results.Json(keyResult.Error, statusCode: 400);
        }

        PaymentId typedPaymentId;
        try
        {
            typedPaymentId = PaymentId.From(paymentId);
        }
        catch (DomainValidationException exception)
        {
            return Results.BadRequest(new PaymentErrorResponse("invalid_request", exception.Message));
        }

        var result = await paymentService.CaptureAsync(
            typedPaymentId,
            keyResult.Key!,
            cancellationToken);
        return ToHttpResult(result);
    }

    private static (GatewayIdempotencyKey? Key, PaymentErrorResponse? Error) ReadIdempotencyKey(
        HttpRequest request)
    {
        try
        {
            return (
                GatewayIdempotencyKey.From(request.Headers["Idempotency-Key"].ToString()),
                null);
        }
        catch (DomainValidationException exception)
        {
            return (null, new PaymentErrorResponse("invalid_idempotency_key", exception.Message));
        }
    }

    private static IResult ToHttpResult(PaymentCommandResult result)
    {
        if (result.Payment is not null)
        {
            return Results.Json(result.Payment, statusCode: result.StatusCode);
        }

        return Results.Json(result.Error, statusCode: result.StatusCode);
    }
}
