using System.ComponentModel.DataAnnotations;

namespace FicMart.PaymentGateway.Infrastructure.Bank;

public sealed class BankOptions
{
    public const string SectionName = "Bank";

    [Required]
    public required Uri BaseUrl { get; init; }

    [Range(1, 30)]
    public int TimeoutSeconds { get; init; } = 5;
}
