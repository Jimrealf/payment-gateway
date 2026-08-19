using System.ComponentModel.DataAnnotations;

namespace FicMart.PaymentGateway.Api.Payments;

public sealed class IdempotencyOptions
{
    public const string SectionName = "Idempotency";

    [Required]
    [MinLength(32)]
    public required string FingerprintSecret { get; init; }

    [Range(120, 3600)]
    public int ProcessingLeaseSeconds { get; init; } = 120;
}
