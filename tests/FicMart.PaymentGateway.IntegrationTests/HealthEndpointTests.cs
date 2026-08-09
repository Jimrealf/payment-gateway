using System.Net;
using System.Net.Http.Json;
using FicMart.PaymentGateway.Api;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FicMart.PaymentGateway.IntegrationTests;

public sealed class HealthEndpointTests
{
    [Fact]
    public async Task GetHealthReturnsHealthyStatus()
    {
        using var application = new WebApplicationFactory<Program>();
        using var client = application.CreateClient();

        using var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);
        var health = await response.Content.ReadFromJsonAsync<HealthResponse>(
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(health);
        Assert.Equal("healthy", health.Status);
    }
}
