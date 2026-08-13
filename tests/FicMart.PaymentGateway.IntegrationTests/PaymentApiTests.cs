using System.Net;
using System.Net.Http.Json;
using FicMart.PaymentGateway.Api.Payments;
using FicMart.PaymentGateway.Domain.Identifiers;
using FicMart.PaymentGateway.Infrastructure.Bank;

namespace FicMart.PaymentGateway.IntegrationTests;

[Collection(PaymentPersistenceTestGroup.Name)]
public sealed class PaymentApiTests(PostgreSqlDatabase database)
{
    [Fact]
    public async Task AuthorizationSuccessIsPersistedAndReturned()
    {
        var bank = new ScriptedBankClient();
        using var application = new PaymentApiFactory(database, bank);
        using var client = application.CreateClient();

        using var response = await AuthorizeAsync(client, NewRequest(), NewKey());
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(
            response.StatusCode == HttpStatusCode.Created,
            $"Expected 201 Created but received {(int)response.StatusCode}: {body}");
        var payment = await response.Content.ReadFromJsonAsync<PaymentResponse>(
            TestContext.Current.CancellationToken);

        Assert.NotNull(payment);
        Assert.Equal("authorized", payment.Status);
        Assert.Equal(1, bank.AuthorizationCalls);
    }

    [Fact]
    public async Task PermanentBankRejectionReturnsStableGatewayError()
    {
        var bank = new ScriptedBankClient();
        bank.EnqueueAuthorization(new BankAuthorizationResult.Rejected(BankRejectionCode.InsufficientFunds));
        using var application = new PaymentApiFactory(database, bank);
        using var client = application.CreateClient();

        using var response = await AuthorizeAsync(client, NewRequest(), NewKey());
        var error = await response.Content.ReadFromJsonAsync<PaymentErrorResponse>(
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);
        Assert.Equal("payment_declined", error?.Code);
        Assert.Equal(1, bank.AuthorizationCalls);
        Assert.DoesNotContain(
            "insufficient_funds",
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CompletedAuthorizationIsReplayedWithoutCallingBankAgain()
    {
        var bank = new ScriptedBankClient();
        using var application = new PaymentApiFactory(database, bank);
        using var client = application.CreateClient();
        var request = NewRequest();
        var key = NewKey();

        using var first = await AuthorizeAsync(client, request, key);
        using var replay = await AuthorizeAsync(client, request, key);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        Assert.Equal(1, bank.AuthorizationCalls);
    }

    [Fact]
    public async Task ReusingAuthorizationKeyForDifferentRequestReturnsConflict()
    {
        var bank = new ScriptedBankClient();
        using var application = new PaymentApiFactory(database, bank);
        using var client = application.CreateClient();
        var key = NewKey();

        using var first = await AuthorizeAsync(client, NewRequest(), key);
        using var conflict = await AuthorizeAsync(client, NewRequest(amount: 3000), key);
        var error = await conflict.Content.ReadFromJsonAsync<PaymentErrorResponse>(
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal("idempotency_key_conflict", error?.Code);
        Assert.Equal(1, bank.AuthorizationCalls);
    }

    [Fact]
    public async Task ConcurrentDuplicateAuthorizationCallsBankOnce()
    {
        var bank = new ScriptedBankClient { BlockAuthorization = true };
        using var application = new PaymentApiFactory(database, bank);
        using var client = application.CreateClient();
        var request = NewRequest();
        var key = NewKey();

        var firstTask = AuthorizeAsync(client, request, key);
        await bank.WaitForAuthorizationAsync();
        using var duplicate = await AuthorizeAsync(client, request, key);
        bank.ReleaseAuthorization();
        using var first = await firstTask;

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, duplicate.StatusCode);
        Assert.Equal(1, bank.AuthorizationCalls);
    }

    [Fact]
    public async Task TransientBankFailuresUseBoundedRetriesWithSameBankKey()
    {
        var bank = new ScriptedBankClient();
        bank.EnqueueAuthorization(
            new BankAuthorizationResult.TransientFailure(),
            new BankAuthorizationResult.TransientFailure());
        using var application = new PaymentApiFactory(database, bank);
        using var client = application.CreateClient();

        using var response = await AuthorizeAsync(client, NewRequest(), NewKey());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(3, bank.AuthorizationCalls);
        Assert.Single(bank.AuthorizationKeys.Distinct());
    }

    [Fact]
    public async Task ExhaustedTransientFailuresRemainRetryable()
    {
        var bank = new ScriptedBankClient();
        bank.EnqueueAuthorization(
            new BankAuthorizationResult.TransientFailure(),
            new BankAuthorizationResult.TransientFailure(),
            new BankAuthorizationResult.TransientFailure());
        using var application = new PaymentApiFactory(database, bank);
        using var client = application.CreateClient();

        using var response = await AuthorizeAsync(client, NewRequest(), NewKey());
        var error = await response.Content.ReadFromJsonAsync<PaymentErrorResponse>(
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("bank_temporarily_unavailable", error?.Code);
        Assert.Equal(3, bank.AuthorizationCalls);
        Assert.Single(bank.AuthorizationKeys.Distinct());
    }

    [Fact]
    public async Task InvalidAuthorizationRequestDoesNotReachBank()
    {
        var bank = new ScriptedBankClient();
        using var application = new PaymentApiFactory(database, bank);
        using var client = application.CreateClient();
        var invalid = NewRequest() with { CardNumber = "not-a-card" };

        using var response = await AuthorizeAsync(client, invalid, NewKey());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, bank.AuthorizationCalls);
    }

    [Fact]
    public async Task UnknownAuthorizationCanBeRecoveredWithOriginalRequestAndKey()
    {
        var bank = new ScriptedBankClient();
        bank.EnqueueAuthorization(new BankAuthorizationResult.Unknown());
        using var application = new PaymentApiFactory(database, bank);
        using var client = application.CreateClient();
        var request = NewRequest();
        var key = NewKey();

        using var unknown = await AuthorizeAsync(client, request, key);
        using var recovered = await AuthorizeAsync(client, request, key);

        Assert.Equal(HttpStatusCode.Accepted, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.Created, recovered.StatusCode);
        Assert.Equal(2, bank.AuthorizationCalls);
        Assert.Single(bank.AuthorizationKeys.Distinct());
    }

    [Fact]
    public async Task AuthorizedPaymentCanBeCapturedAndReplayedExactlyOnce()
    {
        var bank = new ScriptedBankClient();
        using var application = new PaymentApiFactory(database, bank);
        using var client = application.CreateClient();
        using var authorization = await AuthorizeAsync(client, NewRequest(), NewKey());
        var payment = await authorization.Content.ReadFromJsonAsync<PaymentResponse>(
            TestContext.Current.CancellationToken);
        var captureKey = NewKey();

        using var capture = await CaptureAsync(client, payment!.PaymentId, captureKey);
        using var replay = await CaptureAsync(client, payment.PaymentId, captureKey);

        Assert.Equal(HttpStatusCode.OK, capture.StatusCode);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(1, bank.CaptureCalls);
    }

    [Fact]
    public async Task DeclinedPaymentCannotBeCaptured()
    {
        var bank = new ScriptedBankClient();
        bank.EnqueueAuthorization(new BankAuthorizationResult.Rejected(BankRejectionCode.InsufficientFunds));
        using var application = new PaymentApiFactory(database, bank);
        using var client = application.CreateClient();
        using var authorization = await AuthorizeAsync(client, NewRequest(), NewKey());
        var error = await authorization.Content.ReadFromJsonAsync<PaymentErrorResponse>(
            TestContext.Current.CancellationToken);

        using var capture = await CaptureAsync(client, error!.PaymentId!.Value, NewKey());

        Assert.Equal(HttpStatusCode.Conflict, capture.StatusCode);
        Assert.Equal(0, bank.CaptureCalls);
    }

    [Fact]
    public async Task UnknownCaptureCanBeRetriedWithSameBankKey()
    {
        var bank = new ScriptedBankClient();
        bank.EnqueueCapture(new BankCaptureResult.Unknown());
        using var application = new PaymentApiFactory(database, bank);
        using var client = application.CreateClient();
        using var authorization = await AuthorizeAsync(client, NewRequest(), NewKey());
        var payment = await authorization.Content.ReadFromJsonAsync<PaymentResponse>(
            TestContext.Current.CancellationToken);
        var key = NewKey();

        using var unknown = await CaptureAsync(client, payment!.PaymentId, key);
        using var recovered = await CaptureAsync(client, payment.PaymentId, key);

        Assert.Equal(HttpStatusCode.Accepted, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
        Assert.Equal(2, bank.CaptureCalls);
        Assert.Single(bank.CaptureKeys.Distinct());
    }

    [Fact]
    public async Task ConcurrentDuplicateCaptureCallsBankOnce()
    {
        var bank = new ScriptedBankClient();
        using var application = new PaymentApiFactory(database, bank);
        using var client = application.CreateClient();
        using var authorization = await AuthorizeAsync(client, NewRequest(), NewKey());
        var payment = await authorization.Content.ReadFromJsonAsync<PaymentResponse>(
            TestContext.Current.CancellationToken);
        var key = NewKey();
        bank.BlockCapture = true;

        var firstTask = CaptureAsync(client, payment!.PaymentId, key);
        await bank.WaitForCaptureAsync();
        using var duplicate = await CaptureAsync(client, payment.PaymentId, key);
        bank.ReleaseCapture();
        using var first = await firstTask;

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, duplicate.StatusCode);
        Assert.Equal(1, bank.CaptureCalls);
    }

    private static AuthorizePaymentRequest NewRequest(long amount = 2500) => new(
        $"order-{Guid.NewGuid()}",
        "customer-123",
        amount,
        "USD",
        "4111111111111111",
        "123");

    private static string NewKey() => $"key-{Guid.NewGuid()}";

    private static async Task<HttpResponseMessage> AuthorizeAsync(
        HttpClient client,
        AuthorizePaymentRequest request,
        string key)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments/authorize")
        {
            Content = JsonContent.Create(request),
        };
        message.Headers.Add("Idempotency-Key", key);
        return await client.SendAsync(message, TestContext.Current.CancellationToken);
    }

    private static async Task<HttpResponseMessage> CaptureAsync(HttpClient client, Guid paymentId, string key)
    {
        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/payments/{paymentId}/capture");
        message.Headers.Add("Idempotency-Key", key);
        return await client.SendAsync(message, TestContext.Current.CancellationToken);
    }
}
