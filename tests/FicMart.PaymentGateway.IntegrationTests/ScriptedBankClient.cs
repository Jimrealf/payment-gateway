using System.Collections.Concurrent;
using FicMart.PaymentGateway.Domain.Identifiers;
using FicMart.PaymentGateway.Infrastructure.Bank;

namespace FicMart.PaymentGateway.IntegrationTests;

public sealed class ScriptedBankClient : IBankClient
{
    private readonly ConcurrentQueue<BankAuthorizationResult> authorizationResults = new();
    private readonly ConcurrentQueue<BankCaptureResult> captureResults = new();
    private readonly TaskCompletionSource authorizationStarted = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource authorizationRelease = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource captureStarted = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource captureRelease = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private int authorizationCalls;
    private int captureCalls;

    public int AuthorizationCalls => authorizationCalls;

    public int CaptureCalls => captureCalls;

    public bool BlockAuthorization { get; set; }

    public bool BlockCapture { get; set; }

    public ConcurrentQueue<Guid> AuthorizationKeys { get; } = new();

    public ConcurrentQueue<Guid> CaptureKeys { get; } = new();

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

    public Task WaitForAuthorizationAsync() => authorizationStarted.Task;

    public void ReleaseAuthorization() => authorizationRelease.TrySetResult();

    public Task WaitForCaptureAsync() => captureStarted.Task;

    public void ReleaseCapture() => captureRelease.TrySetResult();

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
}
