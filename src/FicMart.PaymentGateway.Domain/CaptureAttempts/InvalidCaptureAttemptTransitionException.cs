namespace FicMart.PaymentGateway.Domain.CaptureAttempts;

public sealed class InvalidCaptureAttemptTransitionException(
    CaptureAttemptStatus current,
    CaptureAttemptStatus requested)
    : Exception($"Capture attempt cannot transition from {current} to {requested}.")
{
    public CaptureAttemptStatus Current { get; } = current;

    public CaptureAttemptStatus Requested { get; } = requested;
}
