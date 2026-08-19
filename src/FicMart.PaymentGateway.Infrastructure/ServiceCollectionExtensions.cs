using FicMart.PaymentGateway.Infrastructure.Bank;
using FicMart.PaymentGateway.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FicMart.PaymentGateway.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPaymentGatewayInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("PaymentGateway") ??
            throw new InvalidOperationException(
                "ConnectionStrings:PaymentGateway must be configured.");
        services.AddDbContext<PaymentGatewayDbContext>(options => options.UseNpgsql(connectionString));

        services.AddOptions<BankOptions>()
            .Bind(configuration.GetSection(BankOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(
                options => options.BaseUrl.IsAbsoluteUri &&
                    options.BaseUrl.Scheme is "http" or "https",
                "Bank:BaseUrl must be an absolute HTTP or HTTPS URL.")
            .ValidateOnStart();

        services.AddHttpClient<IBankClient, MockBankClient>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<BankOptions>>().Value;
            client.BaseAddress = options.BaseUrl;
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });

        return services;
    }
}
