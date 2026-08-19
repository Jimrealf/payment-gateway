using FicMart.PaymentGateway.Domain.Common;
using FicMart.PaymentGateway.Domain.Identifiers;
using FicMart.PaymentGateway.Domain.Payments;
using FicMart.PaymentGateway.Infrastructure.Persistence;

namespace FicMart.PaymentGateway.Api.Payments;

public static class PaymentEndpoints
{
    public static IEndpointRouteBuilder MapPaymentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/payments/authorize", AuthorizeAsync);
        endpoints.MapPost("/api/v1/payments/{paymentId:guid}/capture", CaptureAsync);
        endpoints.MapPost("/api/v1/payments/{paymentId:guid}/void", VoidAsync);
        endpoints.MapPost("/api/v1/payments/{paymentId:guid}/refund", RefundAsync);
        endpoints.MapGet("/api/v1/payments/{paymentId:guid}", GetByIdAsync);
        endpoints.MapGet("/api/v1/payments/by-order/{orderId}", GetByOrderAsync);
        endpoints.MapPost("/api/v1/payments/{paymentId:guid}/reconcile", ReconcileAsync);
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

    private static Task<IResult> VoidAsync(Guid paymentId, HttpRequest request, PaymentService service, CancellationToken token) =>
        RunPaymentOperationAsync(paymentId, request, service.VoidAsync, token);

    private static Task<IResult> RefundAsync(Guid paymentId, HttpRequest request, PaymentService service, CancellationToken token) =>
        RunPaymentOperationAsync(paymentId, request, service.RefundAsync, token);

    private static async Task<IResult> GetByIdAsync(
        Guid paymentId,
        PaymentStore store,
        CancellationToken cancellationToken)
    {
        var snapshot = await store.FindByIdAsync(PaymentId.From(paymentId), cancellationToken);
        return snapshot is null
            ? Results.NotFound(new PaymentErrorResponse("payment_not_found", "Payment was not found."))
            : Results.Ok(ToDetails(snapshot.Payment));
    }

    private static async Task<IResult> GetByOrderAsync(
        string orderId,
        PaymentStore store,
        CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = await store.FindByOrderIdAsync(OrderId.From(orderId), cancellationToken);
            return snapshot is null
                ? Results.NotFound(new PaymentErrorResponse("payment_not_found", "Payment was not found."))
                : Results.Ok(ToDetails(snapshot.Payment));
        }
        catch (DomainValidationException exception)
        {
            return Results.BadRequest(new PaymentErrorResponse("invalid_request", exception.Message));
        }
    }

    private static async Task<IResult> ReconcileAsync(
        Guid paymentId,
        PaymentService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ReconcileAsync(PaymentId.From(paymentId), cancellationToken);
        return result.Outcome == "not_found" ? Results.NotFound(result) : Results.Ok(result);
    }

    private static PaymentDetailsResponse ToDetails(Payment payment) => new(
        payment.Id.Value,
        payment.OrderId.Value,
        payment.Amount.MinorUnits,
        "USD",
        payment.Status.ToString().ToLowerInvariant(),
        payment.Status == PaymentStatus.Captured,
        payment.CreatedAt,
        payment.UpdatedAt);

    private static async Task<IResult> RunPaymentOperationAsync(
        Guid paymentId,
        HttpRequest request,
        Func<PaymentId, GatewayIdempotencyKey, CancellationToken, Task<PaymentCommandResult>> operation,
        CancellationToken cancellationToken)
    {
        var keyResult = ReadIdempotencyKey(request);
        if (keyResult.Error is not null) return Results.Json(keyResult.Error, statusCode: 400);
        var result = await operation(PaymentId.From(paymentId), keyResult.Key!, cancellationToken);
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
