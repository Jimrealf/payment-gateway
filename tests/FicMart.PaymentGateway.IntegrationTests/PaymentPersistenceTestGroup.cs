namespace FicMart.PaymentGateway.IntegrationTests;

[CollectionDefinition(Name)]
public sealed class PaymentPersistenceTestGroup : ICollectionFixture<PostgreSqlDatabase>
{
    public const string Name = "Payment persistence";
}
