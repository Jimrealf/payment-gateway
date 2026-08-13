using FicMart.PaymentGateway.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace FicMart.PaymentGateway.IntegrationTests;

public sealed class PostgreSqlDatabase : IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("payment_gateway_tests")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public PaymentGatewayDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PaymentGatewayDbContext>()
            .UseNpgsql(container.GetConnectionString())
            .Options;

        return new PaymentGatewayDbContext(options);
    }

    public async ValueTask InitializeAsync()
    {
        await container.StartAsync();

        await using var dbContext = CreateDbContext();
        await dbContext.Database.MigrateAsync();
        await dbContext.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync() => await container.DisposeAsync();
}
