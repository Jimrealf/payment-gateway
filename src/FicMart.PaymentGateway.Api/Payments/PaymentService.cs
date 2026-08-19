using FicMart.PaymentGateway.Domain.AuthorizationAttempts;
using FicMart.PaymentGateway.Domain.CaptureAttempts;
using FicMart.PaymentGateway.Domain.Common;
using FicMart.PaymentGateway.Domain.Identifiers;
using FicMart.PaymentGateway.Domain.Money;
using FicMart.PaymentGateway.Domain.OperationAttempts;
using FicMart.PaymentGateway.Domain.Payments;
using FicMart.PaymentGateway.Infrastructure.Bank;
using FicMart.PaymentGateway.Infrastructure.Persistence;
using FicMart.PaymentGateway.Api.Observability;
using System.Diagnostics;

namespace FicMart.PaymentGateway.Api.Payments;

public sealed class PaymentService(
    PaymentWorkflowStore workflowStore,
    PaymentStore paymentStore,
    IBankClient bankClient,
    RequestFingerprint fingerprint,
    PaymentGatewayMetrics metrics,
    TimeProvider timeProvider)
{
    private const int MaximumBankAttempts = 3;

    public async Task<PaymentCommandResult> AuthorizeAsync(
        AuthorizePaymentRequest request,
        GatewayIdempotencyKey idempotencyKey,
        CancellationToken cancellationToken)
    {
        metrics.RecordCommand("authorize");
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
        metrics.RecordCommand("capture");
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
                metrics.RecordReplay("capture");
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
            if (competing is null)
                return Error(409, "capture_already_started", "A capture already exists for this payment.");
            if (competing.RequestFingerprint != requestFingerprint)
                return Error(409, "idempotency_key_conflict", "The idempotency key was used for a different request.");
            return Pending(competing.PaymentId, "capture_processing", "Capture is already processing.");
        }

        return await ExecuteCaptureAsync(paymentId, idempotencyKey, cancellationToken);
    }

    public Task<PaymentCommandResult> VoidAsync(
        PaymentId paymentId,
        GatewayIdempotencyKey idempotencyKey,
        CancellationToken cancellationToken)
    {
        metrics.RecordCommand("void");
        return StartVoidAsync(paymentId, idempotencyKey, cancellationToken);
    }

    public Task<PaymentCommandResult> RefundAsync(
        PaymentId paymentId,
        GatewayIdempotencyKey idempotencyKey,
        CancellationToken cancellationToken)
    {
        metrics.RecordCommand("refund");
        return StartRefundAsync(paymentId, idempotencyKey, cancellationToken);
    }

    public async Task<ReconciliationResponse> ReconcileAsync(
        PaymentId paymentId,
        CancellationToken cancellationToken)
    {
        metrics.RecordCommand("reconcile");
        var payment = await paymentStore.FindByIdAsync(paymentId, cancellationToken);
        if (payment is null)
            return new ReconciliationResponse(paymentId.Value, "not_found", "payment_not_found", null);

        var candidate = await workflowStore.FindReconciliationCandidateAsync(paymentId, cancellationToken);
        if (candidate is null)
        {
            await RecordReconciliationAsync(paymentId, null, ReconciliationOutcome.NoActionRequired, "no_retryable_operation", cancellationToken);
            return new ReconciliationResponse(
                paymentId.Value,
                "no_action_required",
                "no_retryable_operation",
                ToPaymentResponse(payment.Payment));
        }

        if (candidate.Operation == IdempotencyOperation.Authorize)
        {
            await RecordReconciliationAsync(paymentId, candidate.Operation, ReconciliationOutcome.OperatorReviewRequired, "original_card_request_required", cancellationToken);
            return new ReconciliationResponse(paymentId.Value, "operator_review_required", "original_card_request_required", null);
        }

        if (!await workflowStore.TryClaimRetryAsync(
                candidate.Operation,
                candidate.IdempotencyKey,
                timeProvider.GetUtcNow(),
                cancellationToken))
        {
            await RecordReconciliationAsync(paymentId, candidate.Operation, ReconciliationOutcome.AlreadyProcessing, "operation_already_processing", cancellationToken);
            return new ReconciliationResponse(paymentId.Value, "already_processing", "operation_already_processing", null);
        }

        var result = candidate.Operation switch
        {
            IdempotencyOperation.Capture => await ExecuteCaptureAsync(paymentId, candidate.IdempotencyKey, cancellationToken, "reconciliation"),
            IdempotencyOperation.Void => await ExecuteVoidAsync(paymentId, candidate.IdempotencyKey, cancellationToken, "reconciliation"),
            IdempotencyOperation.Refund => await ExecuteRefundAsync(paymentId, candidate.IdempotencyKey, cancellationToken, "reconciliation"),
            _ => throw new InvalidOperationException("Unsupported reconciliation operation."),
        };
        var outcome = result.Payment is not null
            ? ReconciliationOutcome.Recovered
            : result.StatusCode is 202 or 503
                ? ReconciliationOutcome.StillPending
                : ReconciliationOutcome.OperatorReviewRequired;
        var detail = result.Payment is null ? result.Error!.Code : "operation_recovered";
        await RecordReconciliationAsync(paymentId, candidate.Operation, outcome, detail, cancellationToken);
        metrics.RecordReconciliation(outcome.ToString());
        return new ReconciliationResponse(
            paymentId.Value,
            outcome switch
            {
                ReconciliationOutcome.Recovered => "recovered",
                ReconciliationOutcome.StillPending => "still_pending",
                _ => "operator_review_required",
            },
            detail,
            result.Payment);
    }

    private Task RecordReconciliationAsync(
        PaymentId paymentId,
        IdempotencyOperation? operation,
        ReconciliationOutcome outcome,
        string detail,
        CancellationToken cancellationToken) => workflowStore.AddReconciliationRecordAsync(
            paymentId,
            operation,
            outcome,
            detail,
            timeProvider.GetUtcNow(),
            cancellationToken);

    private async Task<PaymentCommandResult> StartVoidAsync(
        PaymentId paymentId,
        GatewayIdempotencyKey key,
        CancellationToken cancellationToken)
    {
        var fingerprintValue = fingerprint.ForVoid(paymentId.Value);
        var replay = await ResumeOperationAsync(
            IdempotencyOperation.Void,
            key,
            fingerprintValue,
            "void",
            (id, operationKey, token) => ExecuteVoidAsync(id, operationKey, token),
            cancellationToken);
        if (replay is not null) return replay;

        var snapshot = await paymentStore.FindByIdAsync(paymentId, cancellationToken);
        if (snapshot is null) return Error(404, "payment_not_found", "Payment was not found.");
        if (snapshot.Payment.Status != PaymentStatus.Authorized)
            return Error(409, "payment_not_authorized", "Only an authorized payment can be voided.");

        var now = timeProvider.GetUtcNow();
        var result = await workflowStore.TryCreateVoidAsync(
            VoidAttempt.Create(VoidAttemptId.New(), paymentId, BankIdempotencyKey.New(), now),
            key,
            fingerprintValue,
            now,
            cancellationToken);
        if (result != CreateOperationResult.Created)
        {
            return result == CreateOperationResult.DuplicateIdempotencyKey
                ? await ResumeOperationAsync(
                    IdempotencyOperation.Void,
                    key,
                    fingerprintValue,
                    "void",
                    (id, operationKey, token) => ExecuteVoidAsync(id, operationKey, token),
                    cancellationToken)
                    ?? Pending(paymentId, "void_processing", "Void is already processing.")
                : Error(409, "void_already_started", "A void already exists for this payment.");
        }
        return await ExecuteVoidAsync(paymentId, key, cancellationToken);
    }

    private async Task<PaymentCommandResult> StartRefundAsync(
        PaymentId paymentId,
        GatewayIdempotencyKey key,
        CancellationToken cancellationToken)
    {
        var fingerprintValue = fingerprint.ForRefund(paymentId.Value);
        var replay = await ResumeOperationAsync(
            IdempotencyOperation.Refund,
            key,
            fingerprintValue,
            "refund",
            (id, operationKey, token) => ExecuteRefundAsync(id, operationKey, token),
            cancellationToken);
        if (replay is not null) return replay;

        var snapshot = await paymentStore.FindByIdAsync(paymentId, cancellationToken);
        if (snapshot is null) return Error(404, "payment_not_found", "Payment was not found.");
        if (snapshot.Payment.Status != PaymentStatus.Captured)
            return Error(409, "payment_not_captured", "Only a captured payment can be refunded.");

        var now = timeProvider.GetUtcNow();
        var result = await workflowStore.TryCreateRefundAsync(
            RefundAttempt.Create(RefundAttemptId.New(), paymentId, BankIdempotencyKey.New(), now),
            key,
            fingerprintValue,
            now,
            cancellationToken);
        if (result != CreateOperationResult.Created)
        {
            return result == CreateOperationResult.DuplicateIdempotencyKey
                ? await ResumeOperationAsync(
                    IdempotencyOperation.Refund,
                    key,
                    fingerprintValue,
                    "refund",
                    (id, operationKey, token) => ExecuteRefundAsync(id, operationKey, token),
                    cancellationToken)
                    ?? Pending(paymentId, "refund_processing", "Refund is already processing.")
                : Error(409, "refund_already_started", "A refund already exists for this payment.");
        }
        return await ExecuteRefundAsync(paymentId, key, cancellationToken);
    }

    private async Task<PaymentCommandResult?> ResumeOperationAsync(
        IdempotencyOperation operation,
        GatewayIdempotencyKey key,
        string requestFingerprint,
        string operationName,
        Func<PaymentId, GatewayIdempotencyKey, CancellationToken, Task<PaymentCommandResult>> execute,
        CancellationToken cancellationToken)
    {
        var existing = await workflowStore.FindIdempotencyAsync(operation, key, cancellationToken);
        if (existing is null) return null;
        if (existing.RequestFingerprint != requestFingerprint)
            return Error(409, "idempotency_key_conflict", "The idempotency key was used for a different request.");
        if (existing.State == IdempotencyState.Completed)
        {
            metrics.RecordReplay(operationName);
            return await ResponseFromOperationAsync(existing.PaymentId, operation, cancellationToken);
        }
        if (existing.State == IdempotencyState.Processing ||
            !await workflowStore.TryClaimRetryAsync(operation, key, timeProvider.GetUtcNow(), cancellationToken))
            return Pending(existing.PaymentId, $"{operationName}_processing", $"{ToTitleCase(operationName)} is already processing.");
        return await execute(existing.PaymentId, key, cancellationToken);
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
            metrics.RecordReplay("authorize");
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
            result = await CallBankAsync(
                "authorize",
                () => bankClient.AuthorizeAsync(new BankAuthorizationRequest(
                    request.CardNumber,
                    request.Cvv,
                    work.Payment.Amount,
                    work.Attempt.BankIdempotencyKey),
                    cancellationToken));
            if (result is not BankAuthorizationResult.TransientFailure || attemptNumber == MaximumBankAttempts)
            {
                break;
            }

            metrics.RecordRetry("authorize");
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
        CancellationToken cancellationToken,
        string actor = "ficmart-api")
    {
        var authorization = await workflowStore.GetAuthorizationWorkAsync(paymentId, cancellationToken);
        var capture = await workflowStore.GetCaptureWorkAsync(paymentId, cancellationToken);
        var bankAuthorizationId = authorization.Attempt.BankAuthorizationId
            ?? throw new InvalidOperationException("Authorized payment is missing its bank authorization ID.");

        BankCaptureResult result = new BankCaptureResult.TransientFailure();
        for (var attemptNumber = 1; attemptNumber <= MaximumBankAttempts; attemptNumber++)
        {
            result = await CallBankAsync(
                "capture",
                () => bankClient.CaptureAsync(new BankCaptureRequest(
                    bankAuthorizationId,
                    capture.Payment.Amount,
                    capture.Attempt.BankIdempotencyKey),
                    cancellationToken));
            if (result is not BankCaptureResult.TransientFailure || attemptNumber == MaximumBankAttempts)
            {
                break;
            }

            metrics.RecordRetry("capture");
            await Task.Delay(TimeSpan.FromMilliseconds(50 * attemptNumber), cancellationToken);
        }

        var now = timeProvider.GetUtcNow();
        switch (result)
        {
            case BankCaptureResult.Captured captured:
                return await workflowStore.MarkCaptureApprovedAsync(key, captured.CaptureId, now, actor, cancellationToken)
                    ? Success(200, capture.Payment, PaymentStatus.Captured)
                    : Error(409, "payment_state_conflict", "The payment changed state while capture was processing.", paymentId);
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

    private async Task<PaymentCommandResult> ExecuteVoidAsync(
        PaymentId paymentId,
        GatewayIdempotencyKey key,
        CancellationToken cancellationToken,
        string actor = "ficmart-api")
    {
        var authorization = await workflowStore.GetAuthorizationWorkAsync(paymentId, cancellationToken);
        var work = await workflowStore.GetVoidWorkAsync(paymentId, cancellationToken);
        var bankAuthorizationId = authorization.Attempt.BankAuthorizationId
            ?? throw new InvalidOperationException("Authorized payment is missing its bank authorization ID.");
        BankVoidResult result = new BankVoidResult.TransientFailure();
        for (var attempt = 1; attempt <= MaximumBankAttempts; attempt++)
        {
            result = await CallBankAsync(
                "void",
                () => bankClient.VoidAsync(
                    new BankVoidRequest(bankAuthorizationId, work.Attempt.BankIdempotencyKey),
                    cancellationToken));
            if (result is not BankVoidResult.TransientFailure || attempt == MaximumBankAttempts) break;
            metrics.RecordRetry("void");
            await Task.Delay(TimeSpan.FromMilliseconds(50 * attempt), cancellationToken);
        }

        var now = timeProvider.GetUtcNow();
        return result switch
        {
            BankVoidResult.Voided voided =>
                await workflowStore.MarkVoidApprovedAsync(key, voided.VoidId, now, actor, cancellationToken)
                    ? Success(200, work.Payment, PaymentStatus.Voided)
                    : Error(409, "payment_state_conflict", "The payment changed state while void was processing.", paymentId),
            BankVoidResult.Rejected => await CompleteVoidRejectionAsync(key, paymentId, now, cancellationToken),
            BankVoidResult.TransientFailure => await MakeVoidRetryableAsync(key, paymentId, false, now, cancellationToken),
            _ => await MakeVoidRetryableAsync(key, paymentId, true, now, cancellationToken),
        };
    }

    private async Task<PaymentCommandResult> ExecuteRefundAsync(
        PaymentId paymentId,
        GatewayIdempotencyKey key,
        CancellationToken cancellationToken,
        string actor = "ficmart-api")
    {
        var capture = await workflowStore.GetCaptureWorkAsync(paymentId, cancellationToken);
        var work = await workflowStore.GetRefundWorkAsync(paymentId, cancellationToken);
        var bankCaptureId = capture.Attempt.BankCaptureId
            ?? throw new InvalidOperationException("Captured payment is missing its bank capture ID.");
        BankRefundResult result = new BankRefundResult.TransientFailure();
        for (var attempt = 1; attempt <= MaximumBankAttempts; attempt++)
        {
            result = await CallBankAsync(
                "refund",
                () => bankClient.RefundAsync(
                    new BankRefundRequest(bankCaptureId, work.Payment.Amount, work.Attempt.BankIdempotencyKey),
                    cancellationToken));
            if (result is not BankRefundResult.TransientFailure || attempt == MaximumBankAttempts) break;
            metrics.RecordRetry("refund");
            await Task.Delay(TimeSpan.FromMilliseconds(50 * attempt), cancellationToken);
        }

        var now = timeProvider.GetUtcNow();
        return result switch
        {
            BankRefundResult.Refunded refunded =>
                await workflowStore.MarkRefundApprovedAsync(key, refunded.RefundId, now, actor, cancellationToken)
                    ? Success(200, work.Payment, PaymentStatus.Refunded)
                    : Error(409, "payment_state_conflict", "The payment changed state while refund was processing.", paymentId),
            BankRefundResult.Rejected => await CompleteRefundRejectionAsync(key, paymentId, now, cancellationToken),
            BankRefundResult.TransientFailure => await MakeRefundRetryableAsync(key, paymentId, false, now, cancellationToken),
            _ => await MakeRefundRetryableAsync(key, paymentId, true, now, cancellationToken),
        };
    }

    private async Task<PaymentCommandResult> CompleteVoidRejectionAsync(GatewayIdempotencyKey key, PaymentId paymentId, DateTimeOffset now, CancellationToken token)
    {
        await workflowStore.MarkVoidRejectedAsync(key, now, token);
        return Error(409, "void_rejected", "The bank rejected the void.", paymentId);
    }

    private async Task<PaymentCommandResult> MakeVoidRetryableAsync(GatewayIdempotencyKey key, PaymentId paymentId, bool unknown, DateTimeOffset now, CancellationToken token)
    {
        await workflowStore.MarkVoidRetryableAsync(key, unknown, now, token);
        return unknown
            ? Pending(paymentId, "void_status_unknown", "The void status could not be determined. Wait a while and confirm the status before retrying.")
            : Error(503, "bank_temporarily_unavailable", "The bank is temporarily unavailable. Retry later.", paymentId, "authorized");
    }

    private async Task<PaymentCommandResult> CompleteRefundRejectionAsync(GatewayIdempotencyKey key, PaymentId paymentId, DateTimeOffset now, CancellationToken token)
    {
        await workflowStore.MarkRefundRejectedAsync(key, now, token);
        return Error(409, "refund_rejected", "The bank rejected the refund.", paymentId);
    }

    private async Task<PaymentCommandResult> MakeRefundRetryableAsync(GatewayIdempotencyKey key, PaymentId paymentId, bool unknown, DateTimeOffset now, CancellationToken token)
    {
        await workflowStore.MarkRefundRetryableAsync(key, unknown, now, token);
        return unknown
            ? Pending(paymentId, "refund_status_unknown", "The refund status could not be determined. Wait a while and confirm the status before retrying.")
            : Error(503, "bank_temporarily_unavailable", "The bank is temporarily unavailable. Retry later.", paymentId, "captured");
    }

    private async Task<PaymentCommandResult> ResponseFromOperationAsync(
        PaymentId paymentId,
        IdempotencyOperation operation,
        CancellationToken cancellationToken)
    {
        var snapshot = await paymentStore.FindByIdAsync(paymentId, cancellationToken)
            ?? throw new InvalidOperationException("Idempotent payment was not found.");
        return operation switch
        {
            IdempotencyOperation.Void when snapshot.Payment.Status == PaymentStatus.Voided =>
                Success(200, snapshot.Payment, PaymentStatus.Voided),
            IdempotencyOperation.Refund when snapshot.Payment.Status == PaymentStatus.Refunded =>
                Success(200, snapshot.Payment, PaymentStatus.Refunded),
            _ => Error(409, $"{operation.ToString().ToLowerInvariant()}_rejected", $"The bank rejected the {operation.ToString().ToLowerInvariant()}.", paymentId),
        };
    }

    private static string ToTitleCase(string value) => char.ToUpperInvariant(value[0]) + value[1..];

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

    private static PaymentResponse ToPaymentResponse(Payment payment) => new(
        payment.Id.Value,
        payment.OrderId.Value,
        payment.Status.ToString().ToLowerInvariant());

    private async Task<T> CallBankAsync<T>(string operation, Func<Task<T>> call)
        where T : notnull
    {
        var started = Stopwatch.GetTimestamp();
        var result = await call();
        metrics.RecordBankAttempt(
            operation,
            result.GetType().Name,
            Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        return result;
    }

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
