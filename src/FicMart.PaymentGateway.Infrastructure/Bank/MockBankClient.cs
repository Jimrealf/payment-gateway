using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using FicMart.PaymentGateway.Domain.Identifiers;

namespace FicMart.PaymentGateway.Infrastructure.Bank;

public sealed class MockBankClient(HttpClient httpClient) : IBankClient
{
    public async Task<BankAuthorizationResult> AuthorizeAsync(
        BankAuthorizationRequest request,
        CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "api/v1/authorizations")
        {
            Content = JsonContent.Create(new CreateAuthorizationRequest(
                request.CardNumber,
                request.Cvv,
                request.Amount.MinorUnits)),
        };
        message.Headers.Add("Idempotency-Key", request.IdempotencyKey.Value.ToString());

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(message, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new BankAuthorizationResult.Unknown();
        }
        catch (HttpRequestException)
        {
            return new BankAuthorizationResult.Unknown();
        }

        using (response)
        {
            if (response.IsSuccessStatusCode)
            {
                return await ReadApprovedAsync(response, cancellationToken);
            }

            if (response.StatusCode == HttpStatusCode.InternalServerError)
            {
                return new BankAuthorizationResult.TransientFailure();
            }

            if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.PaymentRequired)
            {
                var error = await response.Content.ReadFromJsonAsync<BankErrorResponse>(
                    cancellationToken);
                return new BankAuthorizationResult.Rejected(MapRejection(error?.Error));
            }

            return new BankAuthorizationResult.Unknown();
        }
    }

    public async Task<BankCaptureResult> CaptureAsync(
        BankCaptureRequest request,
        CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "api/v1/captures")
        {
            Content = JsonContent.Create(new CreateCaptureRequest(
                request.AuthorizationId.Value,
                request.Amount.MinorUnits)),
        };
        message.Headers.Add("Idempotency-Key", request.IdempotencyKey.Value.ToString());

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(message, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new BankCaptureResult.Unknown();
        }
        catch (HttpRequestException)
        {
            return new BankCaptureResult.Unknown();
        }

        using (response)
        {
            if (response.IsSuccessStatusCode)
            {
                var capture = await response.Content.ReadFromJsonAsync<CaptureResponse>(
                    cancellationToken);
                return capture is not null && capture.Status == "captured"
                    ? new BankCaptureResult.Captured(BankCaptureId.From(capture.CaptureId))
                    : new BankCaptureResult.Unknown();
            }

            if (response.StatusCode == HttpStatusCode.InternalServerError)
            {
                return new BankCaptureResult.TransientFailure();
            }

            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                var error = await response.Content.ReadFromJsonAsync<BankErrorResponse>(
                    cancellationToken);
                return new BankCaptureResult.Rejected(MapCaptureRejection(error?.Error));
            }

            return new BankCaptureResult.Unknown();
        }
    }

    public async Task<BankVoidResult> VoidAsync(BankVoidRequest request, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "api/v1/voids")
        {
            Content = JsonContent.Create(new CreateVoidRequest(request.AuthorizationId.Value)),
        };
        message.Headers.Add("Idempotency-Key", request.IdempotencyKey.Value.ToString());
        try
        {
            using var response = await httpClient.SendAsync(message, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadFromJsonAsync<VoidResponse>(cancellationToken);
                return body is not null && body.Status == "voided"
                    ? new BankVoidResult.Voided(BankVoidId.From(body.VoidId))
                    : new BankVoidResult.Unknown();
            }
            return response.StatusCode == HttpStatusCode.InternalServerError
                ? new BankVoidResult.TransientFailure()
                : response.StatusCode == HttpStatusCode.BadRequest
                    ? new BankVoidResult.Rejected()
                    : new BankVoidResult.Unknown();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new BankVoidResult.Unknown();
        }
        catch (HttpRequestException)
        {
            return new BankVoidResult.Unknown();
        }
    }

    public async Task<BankRefundResult> RefundAsync(BankRefundRequest request, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "api/v1/refunds")
        {
            Content = JsonContent.Create(new CreateRefundRequest(
                request.CaptureId.Value,
                request.Amount.MinorUnits)),
        };
        message.Headers.Add("Idempotency-Key", request.IdempotencyKey.Value.ToString());
        try
        {
            using var response = await httpClient.SendAsync(message, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadFromJsonAsync<RefundResponse>(cancellationToken);
                return body is not null && body.Status == "refunded"
                    ? new BankRefundResult.Refunded(BankRefundId.From(body.RefundId))
                    : new BankRefundResult.Unknown();
            }
            return response.StatusCode == HttpStatusCode.InternalServerError
                ? new BankRefundResult.TransientFailure()
                : response.StatusCode == HttpStatusCode.BadRequest
                    ? new BankRefundResult.Rejected()
                    : new BankRefundResult.Unknown();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new BankRefundResult.Unknown();
        }
        catch (HttpRequestException)
        {
            return new BankRefundResult.Unknown();
        }
    }

    private static async Task<BankAuthorizationResult> ReadApprovedAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var authorization = await response.Content.ReadFromJsonAsync<AuthorizationResponse>(
            cancellationToken);

        if (authorization is null ||
            authorization.Status != "approved" ||
            authorization.Currency != "USD" ||
            authorization.Amount <= 0 ||
            string.IsNullOrWhiteSpace(authorization.AuthorizationId))
        {
            return new BankAuthorizationResult.Unknown();
        }

        return new BankAuthorizationResult.Approved(
            BankAuthorizationId.From(authorization.AuthorizationId),
            authorization.Amount,
            authorization.ExpiresAt);
    }

    private static BankRejectionCode MapRejection(string? code) => code switch
    {
        "invalid_card" => BankRejectionCode.InvalidCard,
        "invalid_cvv" => BankRejectionCode.InvalidCvv,
        "card_expired" => BankRejectionCode.CardExpired,
        "insufficient_funds" => BankRejectionCode.InsufficientFunds,
        "invalid_amount" => BankRejectionCode.InvalidAmount,
        _ => BankRejectionCode.Other,
    };

    private static BankCaptureRejectionCode MapCaptureRejection(string? code) => code switch
    {
        "authorization_not_found" => BankCaptureRejectionCode.AuthorizationNotFound,
        "authorization_expired" => BankCaptureRejectionCode.AuthorizationExpired,
        "authorization_already_used" => BankCaptureRejectionCode.AuthorizationAlreadyUsed,
        "amount_mismatch" => BankCaptureRejectionCode.AmountMismatch,
        _ => BankCaptureRejectionCode.Other,
    };

    private sealed record CreateAuthorizationRequest(
        [property: JsonPropertyName("card_number")] string CardNumber,
        [property: JsonPropertyName("cvv")] string Cvv,
        [property: JsonPropertyName("amount")] long Amount);

    private sealed record AuthorizationResponse(
        [property: JsonPropertyName("authorization_id")] string AuthorizationId,
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("amount")] long Amount,
        [property: JsonPropertyName("currency")] string Currency,
        [property: JsonPropertyName("expires_at")] DateTimeOffset ExpiresAt);

    private sealed record BankErrorResponse(
        [property: JsonPropertyName("error")] string Error);

    private sealed record CreateCaptureRequest(
        [property: JsonPropertyName("authorization_id")] string AuthorizationId,
        [property: JsonPropertyName("amount")] long Amount);

    private sealed record CaptureResponse(
        [property: JsonPropertyName("capture_id")] string CaptureId,
        [property: JsonPropertyName("status")] string Status);

    private sealed record CreateVoidRequest(
        [property: JsonPropertyName("authorization_id")] string AuthorizationId);

    private sealed record VoidResponse(
        [property: JsonPropertyName("void_id")] string VoidId,
        [property: JsonPropertyName("status")] string Status);

    private sealed record CreateRefundRequest(
        [property: JsonPropertyName("capture_id")] string CaptureId,
        [property: JsonPropertyName("amount")] long Amount);

    private sealed record RefundResponse(
        [property: JsonPropertyName("refund_id")] string RefundId,
        [property: JsonPropertyName("status")] string Status);
}
