namespace FicMart.PaymentGateway.Domain.AuthorizationAttempts;

public sealed class InvalidAuthorizationAttemptTransitionException(
    AuthorizationAttemptStatus current,
    AuthorizationAttemptStatus requested)
    : Exception($"Authorization attempt cannot transition from {current} to {requested}.")
{
    public AuthorizationAttemptStatus Current { get; } = current;

    public AuthorizationAttemptStatus Requested { get; } = requested;
}
