using FicMart.PaymentGateway.Domain.AuthorizationAttempts;
using FicMart.PaymentGateway.Domain.CaptureAttempts;
using FicMart.PaymentGateway.Domain.Common;
using FicMart.PaymentGateway.Domain.Identifiers;
using FicMart.PaymentGateway.Domain.Money;
using FicMart.PaymentGateway.Domain.Payments;
using FicMart.PaymentGateway.Infrastructure.Bank;
using FicMart.PaymentGateway.Infrastructure.Persistence;

namespace FicMart.PaymentGateway.Api.Payments;

public sealed class PaymentService(
    PaymentWorkflowStore workflowStore,
    PaymentStore paymentStore,
    IBankClient bankClient,
    RequestFingerprint fingerprint,
    TimeProvider timeProvider)
{
    private const int MaximumBankAttempts = 3;

    public async Task<PaymentCommandResult> AuthorizeAsync(
        AuthorizePaymentRequest request,
        GatewayIdempotencyKey idempotencyKey,
        CancellationToken cancellationToken)
    {
        var validationError = PaymentRequestValidator.Validate(request);
        if (validationError is not null)
        {
            return Error(400, "invalid_request", validationError);
        }

        Payment payment;
        try
        {
            var now = timeProvider.GetUtcNow();
            payment = Payment.Create(
                PaymentId.New(),
                OrderId.From(request.OrderId),
                CustomerId.From(request.CustomerId),
                Money.Usd(request.AmountMinorUnits),
                now);
            var attempt = AuthorizationAttempt.Create(
                AuthorizationAttemptId.New(),
                payment.Id,
                BankIdempotencyKey.New(),
                now);
            var requestFingerprint = fingerprint.ForAuthorization(request);
            var creation = await workflowStore.TryCreateAuthorizationAsync(
                payment,
                attempt,
                idempotencyKey,
                requestFingerprint,
                cancellationToken);

            if (creation == CreatePaymentResult.DuplicateOrder)
            {
                var concurrentRequest = await workflowStore.FindIdempotencyAsync(
                    IdempotencyOperation.Authorize,
                    idempotencyKey,
                    cancellationToken);
                if (concurrentRequest is not null)
                {
                    return await ResumeOrReplayAuthorizationAsync(
                        request,
                        idempotencyKey,
                        requestFingerprint,
                        cancellationToken);
                }

                return Error(409, "order_already_has_payment", "This order already has a payment.");
            }

            if (creation == CreatePaymentResult.DuplicateIdempotencyKey)
            {
                return await ResumeOrReplayAuthorizationAsync(
                    request,
                    idempotencyKey,
                    requestFingerprint,
                    cancellationToken);
            }
        }
        catch (DomainValidationException exception)
        {
            return Error(400, "invalid_request", exception.Message);
        }

        return await ExecuteAuthorizationAsync(request, payment.Id, idempotencyKey, cancellationToken);
    }

    public async Task<PaymentCommandResult> CaptureAsync(
        PaymentId paymentId,
        GatewayIdempotencyKey idempotencyKey,
        CancellationToken cancellationToken)
    {
        var requestFingerprint = fingerprint.ForCapture(paymentId.Value);
        var existingIdempotency = await workflowStore.FindIdempotencyAsync(
            IdempotencyOperation.Capture,
            idempotencyKey,
            cancellationToken);
        if (existingIdempotency is not null)
        {
            if (existingIdempotency.RequestFingerprint != requestFingerprint)
            {
                return Error(409, "idempotency_key_conflict", "The idempotency key was used for a different request.");
            }

            if (existingIdempotency.State == IdempotencyState.Completed)
            {
                return await ResponseFromPaymentAsync(existingIdempotency.PaymentId, false, cancellationToken);
            }

            if (existingIdempotency.State == IdempotencyState.Processing)
            {
                return Pending(existingIdempotency.PaymentId, "capture_processing", "Capture is already processing.");
            }

            if (!await workflowStore.TryClaimRetryAsync(
                    IdempotencyOperation.Capture,
                    idempotencyKey,
                    timeProvider.GetUtcNow(),
                    cancellationToken))
            {
                return Pending(existingIdempotency.PaymentId, "capture_processing", "Capture is already processing.");
            }

            return await ExecuteCaptureAsync(existingIdempotency.PaymentId, idempotencyKey, cancellationToken);
        }

        var snapshot = await paymentStore.FindByIdAsync(paymentId, cancellationToken);
        if (snapshot is null)
        {
            return Error(404, "payment_not_found", "Payment was not found.");
        }

        if (snapshot.Payment.Status != PaymentStatus.Authorized)
        {
            return Error(409, "payment_not_authorized", "Only an authorized payment can be captured.");
        }

        var now = timeProvider.GetUtcNow();
        var attempt = CaptureAttempt.Create(
            CaptureAttemptId.New(),
            paymentId,
            BankIdempotencyKey.New(),
            now);
        var creation = await workflowStore.TryCreateCaptureAsync(
            attempt,
            idempotencyKey,
            requestFingerprint,
            now,
            cancellationToken);

        if (creation != CreateCaptureResult.Created)
        {
            var competing = await workflowStore.FindIdempotencyAsync(
                IdempotencyOperation.Capture,
                idempotencyKey,
                cancellationToken);
            return competing is not null
                ? Pending(competing.PaymentId, "capture_processing", "Capture is already processing.")
                : Error(409, "capture_already_started", "A capture already exists for this payment.");
        }

        return await ExecuteCaptureAsync(paymentId, idempotencyKey, cancellationToken);
    }

    private async Task<PaymentCommandResult> ResumeOrReplayAuthorizationAsync(
        AuthorizePaymentRequest request,
        GatewayIdempotencyKey key,
        string requestFingerprint,
        CancellationToken cancellationToken)
    {
        var existing = await workflowStore.FindIdempotencyAsync(
            IdempotencyOperation.Authorize,
            key,
            cancellationToken) ?? throw new InvalidOperationException("Idempotency record was not found.");

        if (existing.RequestFingerprint != requestFingerprint)
        {
            return Error(409, "idempotency_key_conflict", "The idempotency key was used for a different request.");
        }

        if (existing.State == IdempotencyState.Completed)
        {
            return await ResponseFromPaymentAsync(existing.PaymentId, true, cancellationToken);
        }

        if (existing.State == IdempotencyState.Processing)
        {
            return Pending(existing.PaymentId, "authorization_processing", "Authorization is already processing.");
        }

        if (!await workflowStore.TryClaimRetryAsync(
                IdempotencyOperation.Authorize,
                key,
                timeProvider.GetUtcNow(),
                cancellationToken))
        {
            return Pending(existing.PaymentId, "authorization_processing", "Authorization is already processing.");
        }

        return await ExecuteAuthorizationAsync(request, existing.PaymentId, key, cancellationToken);
    }

    private async Task<PaymentCommandResult> ExecuteAuthorizationAsync(
        AuthorizePaymentRequest request,
        PaymentId paymentId,
        GatewayIdempotencyKey key,
        CancellationToken cancellationToken)
    {
        var work = await workflowStore.GetAuthorizationWorkAsync(paymentId, cancellationToken);
        BankAuthorizationResult result = new BankAuthorizationResult.TransientFailure();
        for (var attemptNumber = 1; attemptNumber <= MaximumBankAttempts; attemptNumber++)
        {
            result = await bankClient.AuthorizeAsync(
                new BankAuthorizationRequest(
                    request.CardNumber,
                    request.Cvv,
                    work.Payment.Amount,
                    work.Attempt.BankIdempotencyKey),
                cancellationToken);
            if (result is not BankAuthorizationResult.TransientFailure || attemptNumber == MaximumBankAttempts)
            {
                break;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50 * attemptNumber), cancellationToken);
        }

        var now = timeProvider.GetUtcNow();
        switch (result)
        {
            case BankAuthorizationResult.Approved approved
                when approved.AmountMinorUnits == work.Payment.Amount.MinorUnits:
                await workflowStore.MarkAuthorizationApprovedAsync(key, approved.AuthorizationId, now, cancellationToken);
                return Success(201, work.Payment, PaymentStatus.Authorized);
            case BankAuthorizationResult.Rejected:
                await workflowStore.MarkAuthorizationRejectedAsync(key, now, cancellationToken);
                return Error(402, "payment_declined", "The bank declined the authorization.", paymentId, "declined");
            case BankAuthorizationResult.TransientFailure:
                await workflowStore.MarkAuthorizationRetryableAsync(key, false, now, cancellationToken);
                return Error(503, "bank_temporarily_unavailable", "The bank is temporarily unavailable. Retry later.", paymentId, "pending");
            default:
                await workflowStore.MarkAuthorizationRetryableAsync(key, true, now, cancellationToken);
                return Pending(
                    paymentId,
                    "authorization_status_unknown",
                    "The transaction status could not be determined. Wait a while and confirm the status before retrying.");
        }
    }

    private async Task<PaymentCommandResult> ExecuteCaptureAsync(
        PaymentId paymentId,
        GatewayIdempotencyKey key,
        CancellationToken cancellationToken)
    {
        var authorization = await workflowStore.GetAuthorizationWorkAsync(paymentId, cancellationToken);
        var capture = await workflowStore.GetCaptureWorkAsync(paymentId, cancellationToken);
        var bankAuthorizationId = authorization.Attempt.BankAuthorizationId
            ?? throw new InvalidOperationException("Authorized payment is missing its bank authorization ID.");

        BankCaptureResult result = new BankCaptureResult.TransientFailure();
        for (var attemptNumber = 1; attemptNumber <= MaximumBankAttempts; attemptNumber++)
        {
            result = await bankClient.CaptureAsync(
                new BankCaptureRequest(
                    bankAuthorizationId,
                    capture.Payment.Amount,
                    capture.Attempt.BankIdempotencyKey),
                cancellationToken);
            if (result is not BankCaptureResult.TransientFailure || attemptNumber == MaximumBankAttempts)
            {
                break;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50 * attemptNumber), cancellationToken);
        }

        var now = timeProvider.GetUtcNow();
        switch (result)
        {
            case BankCaptureResult.Captured captured:
                await workflowStore.MarkCaptureApprovedAsync(key, captured.CaptureId, now, cancellationToken);
                return Success(200, capture.Payment, PaymentStatus.Captured);
            case BankCaptureResult.Rejected:
                await workflowStore.MarkCaptureRejectedAsync(key, now, cancellationToken);
                return Error(409, "capture_rejected", "The bank rejected the capture.", paymentId);
            case BankCaptureResult.TransientFailure:
                await workflowStore.MarkCaptureRetryableAsync(key, false, now, cancellationToken);
                return Error(503, "bank_temporarily_unavailable", "The bank is temporarily unavailable. Retry later.", paymentId, "authorized");
            default:
                await workflowStore.MarkCaptureRetryableAsync(key, true, now, cancellationToken);
                return Pending(
                    paymentId,
                    "capture_status_unknown",
                    "The capture status could not be determined. Wait a while and confirm the status before retrying.");
        }
    }

    private async Task<PaymentCommandResult> ResponseFromPaymentAsync(
        PaymentId paymentId,
        bool authorization,
        CancellationToken cancellationToken)
    {
        var snapshot = await paymentStore.FindByIdAsync(paymentId, cancellationToken)
            ?? throw new InvalidOperationException("Idempotent payment was not found.");
        if (!authorization)
        {
            var capture = await workflowStore.GetCaptureWorkAsync(paymentId, cancellationToken);
            return capture.Attempt.Status switch
            {
                CaptureAttemptStatus.Succeeded => Success(200, snapshot.Payment, PaymentStatus.Captured),
                CaptureAttemptStatus.Rejected => Error(
                    409,
                    "capture_rejected",
                    "The bank rejected the capture.",
                    paymentId),
                _ => Pending(paymentId, "capture_processing", "The capture is still processing."),
            };
        }

        return snapshot.Payment.Status switch
        {
            PaymentStatus.Authorized => Success(201, snapshot.Payment, PaymentStatus.Authorized),
            PaymentStatus.Declined => Error(402, "payment_declined", "The bank declined the authorization.", paymentId, "declined"),
            _ => Pending(paymentId, "payment_processing", "The payment is still processing."),
        };
    }

    private static PaymentCommandResult Success(int statusCode, Payment payment, PaymentStatus status) =>
        PaymentCommandResult.Success(
            statusCode,
            new PaymentResponse(payment.Id.Value, payment.OrderId.Value, status.ToString().ToLowerInvariant()));

    private static PaymentCommandResult Pending(PaymentId paymentId, string code, string message) =>
        Error(202, code, message, paymentId, "pending");

    private static PaymentCommandResult Error(
        int statusCode,
        string code,
        string message,
        PaymentId? paymentId = null,
        string? status = null) => PaymentCommandResult.Failure(
            statusCode,
            new PaymentErrorResponse(code, message, paymentId?.Value, status));
}
