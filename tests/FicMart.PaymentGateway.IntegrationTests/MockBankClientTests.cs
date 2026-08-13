using System.Net;
using System.Text;
using FicMart.PaymentGateway.Domain.Identifiers;
using FicMart.PaymentGateway.Domain.Money;
using FicMart.PaymentGateway.Infrastructure.Bank;

namespace FicMart.PaymentGateway.IntegrationTests;

public sealed class MockBankClientTests
{
    [Fact]
    public async Task AuthorizationSendsTypedContractAndMapsApproval()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, """
            {
              "authorization_id": "auth_123",
              "status": "approved",
              "amount": 2500,
              "currency": "USD",
              "expires_at": "2026-08-20T12:00:00Z",
              "created_at": "2026-08-13T12:00:00Z"
            }
            """);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://bank/") };
        var key = BankIdempotencyKey.New();

        var result = await new MockBankClient(httpClient).AuthorizeAsync(
            new BankAuthorizationRequest("4111111111111111", "123", Money.Usd(2500), key),
            TestContext.Current.CancellationToken);

        var approved = Assert.IsType<BankAuthorizationResult.Approved>(result);
        Assert.Equal("auth_123", approved.AuthorizationId.Value);
        Assert.Equal(2500, approved.AmountMinorUnits);
        Assert.Equal(key.Value.ToString(), handler.IdempotencyKey);
        Assert.Contains("\"card_number\":\"4111111111111111\"", handler.Body);
        Assert.Contains("\"cvv\":\"123\"", handler.Body);
    }

    [Fact]
    public async Task InsufficientFundsIsMappedAsPermanentRejection()
    {
        var handler = new RecordingHandler(HttpStatusCode.PaymentRequired, """
            {"error":"insufficient_funds","message":"insufficient funds"}
            """);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://bank/") };

        var result = await new MockBankClient(httpClient).AuthorizeAsync(
            new BankAuthorizationRequest(
                "4111111111111111",
                "123",
                Money.Usd(2500),
                BankIdempotencyKey.New()),
            TestContext.Current.CancellationToken);

        var rejected = Assert.IsType<BankAuthorizationResult.Rejected>(result);
        Assert.Equal(BankRejectionCode.InsufficientFunds, rejected.Code);
    }

    [Fact]
    public async Task CaptureSendsTypedContractAndMapsSuccess()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, """
            {
              "capture_id": "cap_123",
              "authorization_id": "auth_123",
              "status": "captured",
              "amount": 2500,
              "currency": "USD",
              "captured_at": "2026-08-13T12:00:00Z"
            }
            """);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://bank/") };
        var key = BankIdempotencyKey.New();

        var result = await new MockBankClient(httpClient).CaptureAsync(
            new BankCaptureRequest(
                BankAuthorizationId.From("auth_123"),
                Money.Usd(2500),
                key),
            TestContext.Current.CancellationToken);

        var captured = Assert.IsType<BankCaptureResult.Captured>(result);
        Assert.Equal("cap_123", captured.CaptureId.Value);
        Assert.Equal(key.Value.ToString(), handler.IdempotencyKey);
        Assert.Contains("\"authorization_id\":\"auth_123\"", handler.Body);
    }

    [Fact]
    public async Task NetworkFailureProducesUnknownAuthorizationOutcome()
    {
        using var httpClient = new HttpClient(new NetworkFailureHandler())
        {
            BaseAddress = new Uri("http://bank/"),
        };

        var result = await new MockBankClient(httpClient).AuthorizeAsync(
            new BankAuthorizationRequest(
                "4111111111111111",
                "123",
                Money.Usd(2500),
                BankIdempotencyKey.New()),
            TestContext.Current.CancellationToken);

        Assert.IsType<BankAuthorizationResult.Unknown>(result);
    }

    [Fact]
    public async Task TimeoutProducesUnknownAuthorizationOutcome()
    {
        using var httpClient = new HttpClient(new TimeoutHandler())
        {
            BaseAddress = new Uri("http://bank/"),
            Timeout = TimeSpan.FromMilliseconds(20),
        };

        var result = await new MockBankClient(httpClient).AuthorizeAsync(
            new BankAuthorizationRequest(
                "4111111111111111",
                "123",
                Money.Usd(2500),
                BankIdempotencyKey.New()),
            TestContext.Current.CancellationToken);

        Assert.IsType<BankAuthorizationResult.Unknown>(result);
    }

    private sealed class RecordingHandler(HttpStatusCode statusCode, string responseBody)
        : HttpMessageHandler
    {
        public string? IdempotencyKey { get; private set; }

        public string Body { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            IdempotencyKey = request.Headers.GetValues("Idempotency-Key").Single();
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json"),
            };
        }
    }

    private sealed class NetworkFailureHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => throw new HttpRequestException("Connection failed.");
    }

    private sealed class TimeoutHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("The timeout handler should always be cancelled.");
        }
    }
}
