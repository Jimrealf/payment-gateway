using FicMart.PaymentGateway.Domain.Common;

namespace FicMart.PaymentGateway.Domain.Identifiers;

public sealed record AuthorizationAttemptId
{
    private AuthorizationAttemptId(Guid value)
    {
        Value = value;
    }

    public Guid Value { get; }

    public static AuthorizationAttemptId New() => new(Guid.CreateVersion7());

    public static AuthorizationAttemptId From(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new DomainValidationException("Authorization attempt ID cannot be empty.");
        }

        return new AuthorizationAttemptId(value);
    }
}
