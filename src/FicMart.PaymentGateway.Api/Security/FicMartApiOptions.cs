using System.ComponentModel.DataAnnotations;

namespace FicMart.PaymentGateway.Api.Security;

public sealed class FicMartApiOptions
{
    public const string SectionName = "FicMartApi";

    [Required]
    [MinLength(32)]
    public string ApiKey { get; init; } = string.Empty;
}
