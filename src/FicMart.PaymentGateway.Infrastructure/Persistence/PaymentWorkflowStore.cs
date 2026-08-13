using FicMart.PaymentGateway.Domain.AuthorizationAttempts;
using FicMart.PaymentGateway.Domain.CaptureAttempts;
using FicMart.PaymentGateway.Domain.Identifiers;
using FicMart.PaymentGateway.Domain.Money;
using FicMart.PaymentGateway.Domain.Payments;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FicMart.PaymentGateway.Infrastructure.Persistence;

public sealed class PaymentWorkflowStore(PaymentGatewayDbContext dbContext)
{
    public async Task<CreatePaymentResult> TryCreateAuthorizationAsync(
        Payment payment,
        AuthorizationAttempt attempt,
        GatewayIdempotencyKey idempotencyKey,
        string requestFingerprint,
        CancellationToken cancellationToken)
    {
        dbContext.Payments.Add(new PaymentRecord(payment));
        dbContext.AuthorizationAttempts.Add(new AuthorizationAttemptRecord(attempt));
        dbContext.IdempotencyRecords.Add(new IdempotencyRecord(
            idempotencyKey.Value,
            IdempotencyOperation.Authorize,
            requestFingerprint,
            payment.Id.Value,
            payment.CreatedAt));

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return CreatePaymentResult.Created;
        }
        catch (DbUpdateException exception) when (GetConstraintName(exception) is var constraint)
        {
            dbContext.ChangeTracker.Clear();
            if (constraint == "PK_idempotency_records")
            {
                return CreatePaymentResult.DuplicateIdempotencyKey;
            }

            if (constraint == "ux_payments_order_id")
            {
                return CreatePaymentResult.DuplicateOrder;
            }

            throw;
        }
    }

    public async Task<IdempotencySnapshot?> FindIdempotencyAsync(
        IdempotencyOperation operation,
        GatewayIdempotencyKey key,
        CancellationToken cancellationToken)
    {
        var record = await dbContext.IdempotencyRecords
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Operation == operation && item.Key == key.Value,
                cancellationToken);

        return record is null
            ? null
            : new IdempotencySnapshot(
                GatewayIdempotencyKey.From(record.Key),
                record.Operation,
                record.RequestFingerprint,
                PaymentId.From(record.PaymentId),
                record.State);
    }

    public async Task<bool> TryClaimRetryAsync(
        IdempotencyOperation operation,
        GatewayIdempotencyKey key,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken) =>
        await dbContext.IdempotencyRecords
            .Where(record =>
                record.Operation == operation &&
                record.Key == key.Value &&
                record.State == IdempotencyState.Retryable)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(record => record.State, IdempotencyState.Processing)
                    .SetProperty(record => record.UpdatedAt, occurredAt),
                cancellationToken) == 1;

    public async Task<AuthorizationWork> GetAuthorizationWorkAsync(
        PaymentId paymentId,
        CancellationToken cancellationToken)
    {
        var paymentRecord = await dbContext.Payments.SingleAsync(
            payment => payment.Id == paymentId.Value,
            cancellationToken);
        var attemptRecord = await dbContext.AuthorizationAttempts
            .OrderByDescending(attempt => attempt.CreatedAt)
            .FirstAsync(attempt => attempt.PaymentId == paymentId.Value, cancellationToken);

        return new AuthorizationWork(
            RestorePayment(paymentRecord),
            RestoreAuthorizationAttempt(attemptRecord));
    }

    public async Task MarkAuthorizationApprovedAsync(
        GatewayIdempotencyKey key,
        BankAuthorizationId bankAuthorizationId,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        var records = await GetAuthorizationRecordsAsync(key, cancellationToken);
        var payment = RestorePayment(records.Payment);
        var attempt = RestoreAuthorizationAttempt(records.Attempt);
        payment.MarkAuthorized(occurredAt);
        attempt.MarkSucceeded(bankAuthorizationId, occurredAt);
        records.Payment.Apply(payment);
        records.Attempt.Apply(attempt);
        records.Idempotency.MarkCompleted(occurredAt);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkAuthorizationRejectedAsync(
        GatewayIdempotencyKey key,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        var records = await GetAuthorizationRecordsAsync(key, cancellationToken);
        var payment = RestorePayment(records.Payment);
        var attempt = RestoreAuthorizationAttempt(records.Attempt);
        payment.MarkDeclined(occurredAt);
        attempt.MarkRejected(occurredAt);
        records.Payment.Apply(payment);
        records.Attempt.Apply(attempt);
        records.Idempotency.MarkCompleted(occurredAt);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkAuthorizationRetryableAsync(
        GatewayIdempotencyKey key,
        bool outcomeUnknown,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        var records = await GetAuthorizationRecordsAsync(key, cancellationToken);
        if (outcomeUnknown)
        {
            var attempt = RestoreAuthorizationAttempt(records.Attempt);
            if (attempt.Status == AuthorizationAttemptStatus.Pending)
            {
                attempt.MarkUnknown(occurredAt);
                records.Attempt.Apply(attempt);
            }
        }

        records.Idempotency.MarkRetryable(occurredAt);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<CreateCaptureResult> TryCreateCaptureAsync(
        CaptureAttempt attempt,
        GatewayIdempotencyKey idempotencyKey,
        string requestFingerprint,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken)
    {
        dbContext.CaptureAttempts.Add(new CaptureAttemptRecord(attempt));
        dbContext.IdempotencyRecords.Add(new IdempotencyRecord(
            idempotencyKey.Value,
            IdempotencyOperation.Capture,
            requestFingerprint,
            attempt.PaymentId.Value,
            createdAt));

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return CreateCaptureResult.Created;
        }
        catch (DbUpdateException exception) when (GetConstraintName(exception) is var constraint)
        {
            dbContext.ChangeTracker.Clear();
            if (constraint == "PK_idempotency_records")
            {
                return CreateCaptureResult.DuplicateIdempotencyKey;
            }

            if (constraint == "ix_capture_attempts_payment_id")
            {
                return CreateCaptureResult.CaptureAlreadyExists;
            }

            throw;
        }
    }

    public async Task<CaptureWork> GetCaptureWorkAsync(
        PaymentId paymentId,
        CancellationToken cancellationToken)
    {
        var paymentRecord = await dbContext.Payments.SingleAsync(
            payment => payment.Id == paymentId.Value,
            cancellationToken);
        var attemptRecord = await dbContext.CaptureAttempts.SingleAsync(
            attempt => attempt.PaymentId == paymentId.Value,
            cancellationToken);

        return new CaptureWork(RestorePayment(paymentRecord), RestoreCaptureAttempt(attemptRecord));
    }

    public async Task MarkCaptureApprovedAsync(
        GatewayIdempotencyKey key,
        BankCaptureId bankCaptureId,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        var records = await GetCaptureRecordsAsync(key, cancellationToken);
        var payment = RestorePayment(records.Payment);
        var attempt = RestoreCaptureAttempt(records.Attempt);
        payment.MarkCaptured(occurredAt);
        attempt.MarkSucceeded(bankCaptureId, occurredAt);
        records.Payment.Apply(payment);
        records.Attempt.Apply(attempt);
        records.Idempotency.MarkCompleted(occurredAt);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkCaptureRejectedAsync(
        GatewayIdempotencyKey key,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        var records = await GetCaptureRecordsAsync(key, cancellationToken);
        var attempt = RestoreCaptureAttempt(records.Attempt);
        attempt.MarkRejected(occurredAt);
        records.Attempt.Apply(attempt);
        records.Idempotency.MarkCompleted(occurredAt);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkCaptureRetryableAsync(
        GatewayIdempotencyKey key,
        bool outcomeUnknown,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        var records = await GetCaptureRecordsAsync(key, cancellationToken);
        if (outcomeUnknown)
        {
            var attempt = RestoreCaptureAttempt(records.Attempt);
            if (attempt.Status == CaptureAttemptStatus.Pending)
            {
                attempt.MarkUnknown(occurredAt);
                records.Attempt.Apply(attempt);
            }
        }

        records.Idempotency.MarkRetryable(occurredAt);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<(PaymentRecord Payment, AuthorizationAttemptRecord Attempt, IdempotencyRecord Idempotency)>
        GetAuthorizationRecordsAsync(
            GatewayIdempotencyKey key,
            CancellationToken cancellationToken)
    {
        var idempotency = await dbContext.IdempotencyRecords.SingleAsync(
            record => record.Operation == IdempotencyOperation.Authorize && record.Key == key.Value,
            cancellationToken);
        var payment = await dbContext.Payments.SingleAsync(
            item => item.Id == idempotency.PaymentId,
            cancellationToken);
        var attempt = await dbContext.AuthorizationAttempts
            .OrderByDescending(item => item.CreatedAt)
            .FirstAsync(item => item.PaymentId == payment.Id, cancellationToken);
        return (payment, attempt, idempotency);
    }

    private async Task<(PaymentRecord Payment, CaptureAttemptRecord Attempt, IdempotencyRecord Idempotency)>
        GetCaptureRecordsAsync(
            GatewayIdempotencyKey key,
            CancellationToken cancellationToken)
    {
        var idempotency = await dbContext.IdempotencyRecords.SingleAsync(
            record => record.Operation == IdempotencyOperation.Capture && record.Key == key.Value,
            cancellationToken);
        var payment = await dbContext.Payments.SingleAsync(
            item => item.Id == idempotency.PaymentId,
            cancellationToken);
        var attempt = await dbContext.CaptureAttempts.SingleAsync(
            item => item.PaymentId == payment.Id,
            cancellationToken);
        return (payment, attempt, idempotency);
    }

    private static Payment RestorePayment(PaymentRecord payment) => Payment.Restore(
        PaymentId.From(payment.Id),
        OrderId.From(payment.OrderId),
        CustomerId.From(payment.CustomerId),
        Money.Usd(payment.AmountMinorUnits),
        payment.Status,
        payment.CreatedAt,
        payment.UpdatedAt);

    private static AuthorizationAttempt RestoreAuthorizationAttempt(
        AuthorizationAttemptRecord attempt) => AuthorizationAttempt.Restore(
            AuthorizationAttemptId.From(attempt.Id),
            PaymentId.From(attempt.PaymentId),
            BankIdempotencyKey.From(attempt.BankIdempotencyKey),
            attempt.Status,
            attempt.BankAuthorizationId is null
                ? null
                : BankAuthorizationId.From(attempt.BankAuthorizationId),
            attempt.CreatedAt,
            attempt.UpdatedAt);

    private static CaptureAttempt RestoreCaptureAttempt(CaptureAttemptRecord attempt)
    {
        var restored = CaptureAttempt.Create(
            CaptureAttemptId.From(attempt.Id),
            PaymentId.From(attempt.PaymentId),
            BankIdempotencyKey.From(attempt.BankIdempotencyKey),
            attempt.CreatedAt);

        if (attempt.Status == CaptureAttemptStatus.Unknown)
        {
            restored.MarkUnknown(attempt.UpdatedAt);
        }
        else if (attempt.Status == CaptureAttemptStatus.Succeeded)
        {
            restored.MarkSucceeded(BankCaptureId.From(attempt.BankCaptureId!), attempt.UpdatedAt);
        }
        else if (attempt.Status == CaptureAttemptStatus.Rejected)
        {
            restored.MarkRejected(attempt.UpdatedAt);
        }

        return restored;
    }

    private static string? GetConstraintName(DbUpdateException exception) =>
        exception.InnerException is PostgresException postgresException
            ? postgresException.ConstraintName
            : null;
}
