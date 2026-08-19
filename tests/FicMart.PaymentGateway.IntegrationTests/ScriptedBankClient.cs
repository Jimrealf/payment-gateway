using System.Collections.Concurrent;
using FicMart.PaymentGateway.Domain.Identifiers;
using FicMart.PaymentGateway.Infrastructure.Bank;

namespace FicMart.PaymentGateway.IntegrationTests;

public sealed class ScriptedBankClient : IBankClient
{
    private readonly ConcurrentQueue<BankAuthorizationResult> authorizationResults = new();
    private readonly ConcurrentQueue<BankCaptureResult> captureResults = new();
    private readonly ConcurrentQueue<BankVoidResult> voidResults = new();
    private readonly ConcurrentQueue<BankRefundResult> refundResults = new();
    private readonly TaskCompletionSource authorizationStarted = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource authorizationRelease = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource captureStarted = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource captureRelease = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource voidStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource voidRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int authorizationCalls;
    private int captureCalls;
    private int voidCalls;
    private int refundCalls;

    public int AuthorizationCalls => authorizationCalls;

    public int CaptureCalls => captureCalls;
    public int VoidCalls => voidCalls;
    public int RefundCalls => refundCalls;

    public bool BlockAuthorization { get; set; }

    public bool BlockCapture { get; set; }
    public bool BlockVoid { get; set; }

    public ConcurrentQueue<Guid> AuthorizationKeys { get; } = new();

    public ConcurrentQueue<Guid> CaptureKeys { get; } = new();
    public ConcurrentQueue<Guid> VoidKeys { get; } = new();
    public ConcurrentQueue<Guid> RefundKeys { get; } = new();

    public void EnqueueAuthorization(params BankAuthorizationResult[] results)
    {
        foreach (var result in results)
        {
            authorizationResults.Enqueue(result);
        }
    }

    public void EnqueueCapture(params BankCaptureResult[] results)
    {
        foreach (var result in results)
        {
            captureResults.Enqueue(result);
        }
    }

    public void EnqueueVoid(params BankVoidResult[] results)
    {
        foreach (var result in results) voidResults.Enqueue(result);
    }

    public void EnqueueRefund(params BankRefundResult[] results)
    {
        foreach (var result in results) refundResults.Enqueue(result);
    }

    public Task WaitForAuthorizationAsync() => authorizationStarted.Task;

    public void ReleaseAuthorization() => authorizationRelease.TrySetResult();

    public Task WaitForCaptureAsync() => captureStarted.Task;

    public void ReleaseCapture() => captureRelease.TrySetResult();
    public Task WaitForVoidAsync() => voidStarted.Task;
    public void ReleaseVoid() => voidRelease.TrySetResult();

    public async Task<BankAuthorizationResult> AuthorizeAsync(
        BankAuthorizationRequest request,
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref authorizationCalls);
        AuthorizationKeys.Enqueue(request.IdempotencyKey.Value);
        authorizationStarted.TrySetResult();
        if (BlockAuthorization)
        {
            await authorizationRelease.Task.WaitAsync(cancellationToken);
        }

        return authorizationResults.TryDequeue(out var result)
            ? result
            : new BankAuthorizationResult.Approved(
                BankAuthorizationId.From($"auth_{Guid.NewGuid()}"),
                request.Amount.MinorUnits,
                DateTimeOffset.UtcNow.AddDays(7));
    }

    public async Task<BankCaptureResult> CaptureAsync(
        BankCaptureRequest request,
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref captureCalls);
        CaptureKeys.Enqueue(request.IdempotencyKey.Value);
        captureStarted.TrySetResult();
        if (BlockCapture)
        {
            await captureRelease.Task.WaitAsync(cancellationToken);
        }

        return captureResults.TryDequeue(out var result)
            ? result
            : new BankCaptureResult.Captured(
                BankCaptureId.From($"cap_{Guid.NewGuid()}"));
    }

    public async Task<BankVoidResult> VoidAsync(BankVoidRequest request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref voidCalls);
        VoidKeys.Enqueue(request.IdempotencyKey.Value);
        voidStarted.TrySetResult();
        if (BlockVoid) await voidRelease.Task.WaitAsync(cancellationToken);
        return voidResults.TryDequeue(out var result)
            ? result
            : new BankVoidResult.Voided(BankVoidId.From($"void_{Guid.NewGuid()}"));
    }

    public Task<BankRefundResult> RefundAsync(BankRefundRequest request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref refundCalls);
        RefundKeys.Enqueue(request.IdempotencyKey.Value);
        return Task.FromResult(refundResults.TryDequeue(out var result)
            ? result
            : new BankRefundResult.Refunded(BankRefundId.From($"refund_{Guid.NewGuid()}")));
    }
}
