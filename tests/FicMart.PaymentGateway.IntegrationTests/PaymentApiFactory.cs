using FicMart.PaymentGateway.Infrastructure.Bank;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FicMart.PaymentGateway.IntegrationTests;

public sealed class PaymentApiFactory(
    PostgreSqlDatabase database,
    ScriptedBankClient bankClient) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:PaymentGateway"] = database.ConnectionString,
                ["Idempotency:FingerprintSecret"] = "integration-test-secret-at-least-32-characters",
            }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IBankClient>();
            services.AddSingleton<IBankClient>(bankClient);
        });
    }
}
