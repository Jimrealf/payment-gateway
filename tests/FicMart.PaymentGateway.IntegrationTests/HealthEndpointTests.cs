using System.Net;
using System.Net.Http.Json;
using FicMart.PaymentGateway.Api;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace FicMart.PaymentGateway.IntegrationTests;

public sealed class HealthEndpointTests
{
    [Fact]
    public async Task GetHealthReturnsHealthyStatus()
    {
        using var application = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.ConfigureAppConfiguration(
                (_, configuration) => configuration.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["Idempotency:FingerprintSecret"] =
                            "integration-test-secret-at-least-32-characters",
                    })));
        using var client = application.CreateClient();

        using var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);
        var health = await response.Content.ReadFromJsonAsync<HealthResponse>(
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(health);
        Assert.Equal("healthy", health.Status);
    }
}
