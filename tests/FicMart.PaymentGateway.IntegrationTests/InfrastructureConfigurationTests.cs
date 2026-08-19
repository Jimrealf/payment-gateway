using FicMart.PaymentGateway.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FicMart.PaymentGateway.IntegrationTests;

public sealed class InfrastructureConfigurationTests
{
    [Fact]
    public void MissingGatewayConnectionStringStopsStartup()
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddPaymentGatewayInfrastructure(configuration));

        Assert.Equal("ConnectionStrings:PaymentGateway must be configured.", exception.Message);
    }
}
