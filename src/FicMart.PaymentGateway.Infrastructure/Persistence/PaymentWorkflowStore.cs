using FicMart.PaymentGateway.Domain.AuthorizationAttempts;
using FicMart.PaymentGateway.Domain.CaptureAttempts;
using FicMart.PaymentGateway.Domain.Identifiers;
using FicMart.PaymentGateway.Domain.Money;
using FicMart.PaymentGateway.Domain.OperationAttempts;
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

    public async Task<ReconciliationCandidate?> FindReconciliationCandidateAsync(
        PaymentId paymentId,
        CancellationToken cancellationToken)
    {
        var record = await dbContext.IdempotencyRecords
            .AsNoTracking()
            .Where(item => item.PaymentId == paymentId.Value && item.State == IdempotencyState.Retryable)
            .OrderByDescending(item => item.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        return record is null
            ? null
            : new ReconciliationCandidate(
                paymentId,
                record.Operation,
                GatewayIdempotencyKey.From(record.Key));
    }

    public async Task AddReconciliationRecordAsync(
        PaymentId paymentId,
        IdempotencyOperation? operation,
        ReconciliationOutcome outcome,
        string detailCode,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken)
    {
        dbContext.ReconciliationRecords.Add(new ReconciliationRecord(
            Guid.CreateVersion7(),
            paymentId.Value,
            operation,
            outcome,
            detailCode,
            createdAt));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

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
        AddAudit(payment.Id, "authorization_succeeded", PaymentStatus.PendingAuthorization, PaymentStatus.Authorized, "ficmart-api", occurredAt);
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
        AddAudit(payment.Id, "authorization_rejected", PaymentStatus.PendingAuthorization, PaymentStatus.Declined, "ficmart-api", occurredAt);
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

    public async Task<bool> MarkCaptureApprovedAsync(
        GatewayIdempotencyKey key,
        BankCaptureId bankCaptureId,
        DateTimeOffset occurredAt,
        string actor,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var records = await GetCaptureRecordsAsync(key, cancellationToken);
        var attempt = RestoreCaptureAttempt(records.Attempt);
        attempt.MarkSucceeded(bankCaptureId, occurredAt);
        records.Attempt.Apply(attempt);
        records.Idempotency.MarkCompleted(occurredAt);
        var transitioned = await dbContext.Payments
            .Where(payment => payment.Id == records.Payment.Id && payment.Status == PaymentStatus.Authorized)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(payment => payment.Status, PaymentStatus.Captured)
                    .SetProperty(payment => payment.UpdatedAt, occurredAt),
                cancellationToken) == 1;
        if (transitioned) AddAudit(PaymentId.From(records.Payment.Id), "capture_succeeded", PaymentStatus.Authorized, PaymentStatus.Captured, actor, occurredAt);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return transitioned;
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

    public async Task<CreateOperationResult> TryCreateVoidAsync(
        VoidAttempt attempt,
        GatewayIdempotencyKey idempotencyKey,
        string requestFingerprint,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken)
    {
        dbContext.VoidAttempts.Add(new VoidAttemptRecord(attempt));
        dbContext.IdempotencyRecords.Add(new IdempotencyRecord(
            idempotencyKey.Value,
            IdempotencyOperation.Void,
            requestFingerprint,
            attempt.PaymentId.Value,
            createdAt));
        return await SaveOperationStartAsync("ux_void_attempts_payment_id", cancellationToken);
    }

    public async Task<CreateOperationResult> TryCreateRefundAsync(
        RefundAttempt attempt,
        GatewayIdempotencyKey idempotencyKey,
        string requestFingerprint,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken)
    {
        dbContext.RefundAttempts.Add(new RefundAttemptRecord(attempt));
        dbContext.IdempotencyRecords.Add(new IdempotencyRecord(
            idempotencyKey.Value,
            IdempotencyOperation.Refund,
            requestFingerprint,
            attempt.PaymentId.Value,
            createdAt));
        return await SaveOperationStartAsync("ux_refund_attempts_payment_id", cancellationToken);
    }

    public async Task<VoidWork> GetVoidWorkAsync(PaymentId paymentId, CancellationToken cancellationToken)
    {
        var payment = await dbContext.Payments.SingleAsync(item => item.Id == paymentId.Value, cancellationToken);
        var attempt = await dbContext.VoidAttempts.SingleAsync(item => item.PaymentId == paymentId.Value, cancellationToken);
        return new VoidWork(RestorePayment(payment), RestoreVoidAttempt(attempt));
    }

    public async Task<RefundWork> GetRefundWorkAsync(PaymentId paymentId, CancellationToken cancellationToken)
    {
        var payment = await dbContext.Payments.SingleAsync(item => item.Id == paymentId.Value, cancellationToken);
        var attempt = await dbContext.RefundAttempts.SingleAsync(item => item.PaymentId == paymentId.Value, cancellationToken);
        return new RefundWork(RestorePayment(payment), RestoreRefundAttempt(attempt));
    }

    public async Task<bool> MarkVoidApprovedAsync(
        GatewayIdempotencyKey key,
        BankVoidId bankVoidId,
        DateTimeOffset occurredAt,
        string actor,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var records = await GetVoidRecordsAsync(key, cancellationToken);
        var attempt = RestoreVoidAttempt(records.Attempt);
        attempt.MarkSucceeded(bankVoidId, occurredAt);
        records.Attempt.Apply(attempt);
        records.Idempotency.MarkCompleted(occurredAt);
        var transitioned = await dbContext.Payments
            .Where(payment => payment.Id == records.Payment.Id && payment.Status == PaymentStatus.Authorized)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(payment => payment.Status, PaymentStatus.Voided)
                    .SetProperty(payment => payment.UpdatedAt, occurredAt),
                cancellationToken) == 1;
        if (transitioned) AddAudit(PaymentId.From(records.Payment.Id), "void_succeeded", PaymentStatus.Authorized, PaymentStatus.Voided, actor, occurredAt);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return transitioned;
    }

    public async Task<bool> MarkRefundApprovedAsync(
        GatewayIdempotencyKey key,
        BankRefundId bankRefundId,
        DateTimeOffset occurredAt,
        string actor,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var records = await GetRefundRecordsAsync(key, cancellationToken);
        var attempt = RestoreRefundAttempt(records.Attempt);
        attempt.MarkSucceeded(bankRefundId, occurredAt);
        records.Attempt.Apply(attempt);
        records.Idempotency.MarkCompleted(occurredAt);
        var transitioned = await dbContext.Payments
            .Where(payment => payment.Id == records.Payment.Id && payment.Status == PaymentStatus.Captured)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(payment => payment.Status, PaymentStatus.Refunded)
                    .SetProperty(payment => payment.UpdatedAt, occurredAt),
                cancellationToken) == 1;
        if (transitioned) AddAudit(PaymentId.From(records.Payment.Id), "refund_succeeded", PaymentStatus.Captured, PaymentStatus.Refunded, actor, occurredAt);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return transitioned;
    }

    public Task MarkVoidRejectedAsync(GatewayIdempotencyKey key, DateTimeOffset occurredAt, CancellationToken cancellationToken) =>
        ResolveVoidAsync(key, occurredAt, false, false, cancellationToken);

    public Task MarkVoidRetryableAsync(GatewayIdempotencyKey key, bool unknown, DateTimeOffset occurredAt, CancellationToken cancellationToken) =>
        ResolveVoidAsync(key, occurredAt, true, unknown, cancellationToken);

    public Task MarkRefundRejectedAsync(GatewayIdempotencyKey key, DateTimeOffset occurredAt, CancellationToken cancellationToken) =>
        ResolveRefundAsync(key, occurredAt, false, false, cancellationToken);

    public Task MarkRefundRetryableAsync(GatewayIdempotencyKey key, bool unknown, DateTimeOffset occurredAt, CancellationToken cancellationToken) =>
        ResolveRefundAsync(key, occurredAt, true, unknown, cancellationToken);

    private async Task<CreateOperationResult> SaveOperationStartAsync(
        string operationConstraint,
        CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return CreateOperationResult.Created;
        }
        catch (DbUpdateException exception) when (GetConstraintName(exception) is var constraint)
        {
            dbContext.ChangeTracker.Clear();
            if (constraint == "PK_idempotency_records")
                return CreateOperationResult.DuplicateIdempotencyKey;
            if (constraint == operationConstraint)
                return CreateOperationResult.OperationAlreadyExists;
            throw;
        }
    }

    private async Task ResolveVoidAsync(
        GatewayIdempotencyKey key,
        DateTimeOffset occurredAt,
        bool retryable,
        bool unknown,
        CancellationToken cancellationToken)
    {
        var records = await GetVoidRecordsAsync(key, cancellationToken);
        var attempt = RestoreVoidAttempt(records.Attempt);
        if (unknown && attempt.Status == OperationAttemptStatus.Pending)
        {
            attempt.MarkUnknown(occurredAt);
            records.Attempt.Apply(attempt);
        }
        else if (!retryable)
        {
            attempt.MarkRejected(occurredAt);
            records.Attempt.Apply(attempt);
        }
        if (retryable) records.Idempotency.MarkRetryable(occurredAt);
        else records.Idempotency.MarkCompleted(occurredAt);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ResolveRefundAsync(
        GatewayIdempotencyKey key,
        DateTimeOffset occurredAt,
        bool retryable,
        bool unknown,
        CancellationToken cancellationToken)
    {
        var records = await GetRefundRecordsAsync(key, cancellationToken);
        var attempt = RestoreRefundAttempt(records.Attempt);
        if (unknown && attempt.Status == OperationAttemptStatus.Pending)
        {
            attempt.MarkUnknown(occurredAt);
            records.Attempt.Apply(attempt);
        }
        else if (!retryable)
        {
            attempt.MarkRejected(occurredAt);
            records.Attempt.Apply(attempt);
        }
        if (retryable) records.Idempotency.MarkRetryable(occurredAt);
        else records.Idempotency.MarkCompleted(occurredAt);
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

    private async Task<(PaymentRecord Payment, VoidAttemptRecord Attempt, IdempotencyRecord Idempotency)>
        GetVoidRecordsAsync(GatewayIdempotencyKey key, CancellationToken cancellationToken)
    {
        var idempotency = await dbContext.IdempotencyRecords.SingleAsync(
            record => record.Operation == IdempotencyOperation.Void && record.Key == key.Value,
            cancellationToken);
        var payment = await dbContext.Payments.SingleAsync(item => item.Id == idempotency.PaymentId, cancellationToken);
        var attempt = await dbContext.VoidAttempts.SingleAsync(item => item.PaymentId == payment.Id, cancellationToken);
        return (payment, attempt, idempotency);
    }

    private async Task<(PaymentRecord Payment, RefundAttemptRecord Attempt, IdempotencyRecord Idempotency)>
        GetRefundRecordsAsync(GatewayIdempotencyKey key, CancellationToken cancellationToken)
    {
        var idempotency = await dbContext.IdempotencyRecords.SingleAsync(
            record => record.Operation == IdempotencyOperation.Refund && record.Key == key.Value,
            cancellationToken);
        var payment = await dbContext.Payments.SingleAsync(item => item.Id == idempotency.PaymentId, cancellationToken);
        var attempt = await dbContext.RefundAttempts.SingleAsync(item => item.PaymentId == payment.Id, cancellationToken);
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

    private static VoidAttempt RestoreVoidAttempt(VoidAttemptRecord attempt) => VoidAttempt.Restore(
        VoidAttemptId.From(attempt.Id),
        PaymentId.From(attempt.PaymentId),
        BankIdempotencyKey.From(attempt.BankIdempotencyKey),
        attempt.Status,
        attempt.BankVoidId is null ? null : BankVoidId.From(attempt.BankVoidId),
        attempt.CreatedAt,
        attempt.UpdatedAt);

    private static RefundAttempt RestoreRefundAttempt(RefundAttemptRecord attempt) => RefundAttempt.Restore(
        RefundAttemptId.From(attempt.Id),
        PaymentId.From(attempt.PaymentId),
        BankIdempotencyKey.From(attempt.BankIdempotencyKey),
        attempt.Status,
        attempt.BankRefundId is null ? null : BankRefundId.From(attempt.BankRefundId),
        attempt.CreatedAt,
        attempt.UpdatedAt);

    private static string? GetConstraintName(DbUpdateException exception) =>
        exception.InnerException is PostgresException postgresException
            ? postgresException.ConstraintName
            : null;

    private void AddAudit(
        PaymentId paymentId,
        string eventType,
        PaymentStatus previousStatus,
        PaymentStatus newStatus,
        string actor,
        DateTimeOffset occurredAt) => dbContext.AuditRecords.Add(new AuditRecord(
            Guid.CreateVersion7(),
            paymentId.Value,
            eventType,
            previousStatus.ToString(),
            newStatus.ToString(),
            actor,
            occurredAt));
}
