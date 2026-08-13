using FicMart.PaymentGateway.Domain.Common;
using FicMart.PaymentGateway.Domain.Money;

namespace FicMart.PaymentGateway.UnitTests;

public sealed class MoneyTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void UsdRejectsNonPositiveAmounts(long cents)
    {
        Assert.Throws<DomainValidationException>(() => Money.Usd(cents));
    }
}
