using FicMart.PaymentGateway.Domain.Common;

namespace FicMart.PaymentGateway.Domain.Money;

public sealed record Money
{
    private Money(long minorUnits, Currency currency)
    {
        MinorUnits = minorUnits;
        Currency = currency;
    }

    public long MinorUnits { get; }

    public Currency Currency { get; }

    public static Money Usd(long cents)
    {
        if (cents <= 0)
        {
            throw new DomainValidationException("Money must contain at least one cent.");
        }

        return new Money(cents, Currency.Usd);
    }
}
