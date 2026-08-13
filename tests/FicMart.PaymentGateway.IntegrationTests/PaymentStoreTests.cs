using FicMart.PaymentGateway.Domain.AuthorizationAttempts;
using FicMart.PaymentGateway.Domain.Identifiers;
using FicMart.PaymentGateway.Domain.Money;
using FicMart.PaymentGateway.Domain.Payments;
using FicMart.PaymentGateway.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FicMart.PaymentGateway.IntegrationTests;

[Collection(PaymentPersistenceTestGroup.Name)]
public sealed class PaymentStoreTests(PostgreSqlDatabase database)
{
    private static readonly DateTimeOffset CreatedAt = new(
        2026,
        8,
        13,
        12,
        0,
        0,
        TimeSpan.Zero);

    [Fact]
    public async Task PendingAuthorizationIntentCanBeReloadedAfterItsContextIsDisposed()
    {
        var payment = CreatePayment("order-reload");
        var attempt = CreateAttempt(payment.Id, BankIdempotencyKey.New());

        await using (var writeContext = database.CreateDbContext())
        {
            var store = new PaymentStore(writeContext);
            await store.AddAsync(payment, attempt, TestContext.Current.CancellationToken);
        }

        await using var readContext = database.CreateDbContext();
        var reloaded = await new PaymentStore(readContext)
            .FindByIdAsync(payment.Id, TestContext.Current.CancellationToken);

        Assert.NotNull(reloaded);
        Assert.Equal(payment.Id, reloaded.Payment.Id);
        Assert.Equal("order-reload", reloaded.Payment.OrderId.Value);
        Assert.Equal("customer-123", reloaded.Payment.CustomerId.Value);
        Assert.Equal(2500, reloaded.Payment.Amount.MinorUnits);
        Assert.Equal(Currency.Usd, reloaded.Payment.Amount.Currency);
        Assert.Equal(PaymentStatus.PendingAuthorization, reloaded.Payment.Status);

        var reloadedAttempt = Assert.Single(reloaded.AuthorizationAttempts);
        Assert.Equal(attempt.Id, reloadedAttempt.Id);
        Assert.Equal(attempt.BankIdempotencyKey, reloadedAttempt.BankIdempotencyKey);
        Assert.Equal(AuthorizationAttemptStatus.Pending, reloadedAttempt.Status);
        Assert.Null(reloadedAttempt.BankAuthorizationId);
    }

    [Fact]
    public async Task DuplicateOrderIdIsRejected()
    {
        var firstPayment = CreatePayment("order-duplicate");
        var secondPayment = CreatePayment("order-duplicate");

        await using var firstContext = database.CreateDbContext();
        await new PaymentStore(firstContext).AddAsync(
            firstPayment,
            CreateAttempt(firstPayment.Id, BankIdempotencyKey.New()),
            TestContext.Current.CancellationToken);

        await using var secondContext = database.CreateDbContext();
        await Assert.ThrowsAsync<DbUpdateException>(() => new PaymentStore(secondContext).AddAsync(
            secondPayment,
            CreateAttempt(secondPayment.Id, BankIdempotencyKey.New()),
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DuplicateBankIdempotencyKeyIsRejected()
    {
        var sharedBankKey = BankIdempotencyKey.New();
        var firstPayment = CreatePayment("order-bank-key-1");
        var secondPayment = CreatePayment("order-bank-key-2");

        await using var firstContext = database.CreateDbContext();
        await new PaymentStore(firstContext).AddAsync(
            firstPayment,
            CreateAttempt(firstPayment.Id, sharedBankKey),
            TestContext.Current.CancellationToken);

        await using var secondContext = database.CreateDbContext();
        await Assert.ThrowsAsync<DbUpdateException>(() => new PaymentStore(secondContext).AddAsync(
            secondPayment,
            CreateAttempt(secondPayment.Id, sharedBankKey),
            TestContext.Current.CancellationToken));
    }

    private static Payment CreatePayment(string orderId) => Payment.Create(
        PaymentId.New(),
        OrderId.From(orderId),
        CustomerId.From("customer-123"),
        Money.Usd(2500),
        CreatedAt);

    private static AuthorizationAttempt CreateAttempt(
        PaymentId paymentId,
        BankIdempotencyKey bankIdempotencyKey) => AuthorizationAttempt.Create(
            AuthorizationAttemptId.New(),
            paymentId,
            bankIdempotencyKey,
            CreatedAt);
}
