using FicMart.PaymentGateway.Domain.Common;
using FicMart.PaymentGateway.Domain.Identifiers;

namespace FicMart.PaymentGateway.UnitTests;

public sealed class IdentifierTests
{
    [Fact]
    public void StringIdentifiersRejectValuesLongerThanTheirPersistedColumns()
    {
        var oversizedValue = new string('x', 129);

        Assert.Throws<DomainValidationException>(() => OrderId.From(oversizedValue));
        Assert.Throws<DomainValidationException>(() => CustomerId.From(oversizedValue));
        Assert.Throws<DomainValidationException>(() => BankAuthorizationId.From(oversizedValue));
    }
}
