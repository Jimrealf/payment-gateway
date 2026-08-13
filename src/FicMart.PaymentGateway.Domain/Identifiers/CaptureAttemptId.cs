using FicMart.PaymentGateway.Domain.Common;

namespace FicMart.PaymentGateway.Domain.Identifiers;

public sealed record CaptureAttemptId
{
    private CaptureAttemptId(Guid value)
    {
        Value = value;
    }

    public Guid Value { get; }

    public static CaptureAttemptId New() => new(Guid.CreateVersion7());

    public static CaptureAttemptId From(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new DomainValidationException("Capture attempt ID cannot be empty.");
        }

        return new CaptureAttemptId(value);
    }
}
