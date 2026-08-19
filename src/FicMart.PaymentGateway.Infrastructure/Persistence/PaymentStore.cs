using FicMart.PaymentGateway.Domain.AuthorizationAttempts;
using FicMart.PaymentGateway.Domain.Identifiers;
using FicMart.PaymentGateway.Domain.Money;
using FicMart.PaymentGateway.Domain.Payments;
using Microsoft.EntityFrameworkCore;

namespace FicMart.PaymentGateway.Infrastructure.Persistence;

public sealed class PaymentStore(PaymentGatewayDbContext dbContext)
{
    public async Task AddAsync(
        Payment payment,
        AuthorizationAttempt authorizationAttempt,
        CancellationToken cancellationToken)
    {
        if (authorizationAttempt.PaymentId != payment.Id)
        {
            throw new ArgumentException(
                "The authorization attempt must belong to the payment being persisted.",
                nameof(authorizationAttempt));
        }

        dbContext.Payments.Add(new PaymentRecord(payment));
        dbContext.AuthorizationAttempts.Add(new AuthorizationAttemptRecord(authorizationAttempt));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<PaymentSnapshot?> FindByIdAsync(
        PaymentId paymentId,
        CancellationToken cancellationToken)
    {
        var paymentRecord = await dbContext.Payments
            .AsNoTracking()
            .SingleOrDefaultAsync(payment => payment.Id == paymentId.Value, cancellationToken);

        if (paymentRecord is null)
        {
            return null;
        }

        var attemptRecords = await dbContext.AuthorizationAttempts
            .AsNoTracking()
            .Where(attempt => attempt.PaymentId == paymentId.Value)
            .OrderBy(attempt => attempt.CreatedAt)
            .ToListAsync(cancellationToken);

        var payment = Payment.Restore(
            PaymentId.From(paymentRecord.Id),
            OrderId.From(paymentRecord.OrderId),
            CustomerId.From(paymentRecord.CustomerId),
            Money.Usd(paymentRecord.AmountMinorUnits),
            paymentRecord.Status,
            paymentRecord.CreatedAt,
            paymentRecord.UpdatedAt);

        var attempts = attemptRecords
            .Select(RestoreAuthorizationAttempt)
            .ToArray();

        return new PaymentSnapshot(payment, attempts);
    }

    public async Task<PaymentSnapshot?> FindByOrderIdAsync(
        OrderId orderId,
        CancellationToken cancellationToken)
    {
        var paymentId = await dbContext.Payments
            .AsNoTracking()
            .Where(payment => payment.OrderId == orderId.Value)
            .Select(payment => (Guid?)payment.Id)
            .SingleOrDefaultAsync(cancellationToken);
        return paymentId is null
            ? null
            : await FindByIdAsync(PaymentId.From(paymentId.Value), cancellationToken);
    }

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
}
