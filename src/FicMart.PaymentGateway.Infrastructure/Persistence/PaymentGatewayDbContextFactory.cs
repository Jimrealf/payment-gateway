using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FicMart.PaymentGateway.Infrastructure.Persistence;

public sealed class PaymentGatewayDbContextFactory
    : IDesignTimeDbContextFactory<PaymentGatewayDbContext>
{
    public PaymentGatewayDbContext CreateDbContext(string[] args)
    {
        const string localConnection =
            "Host=localhost;Port=5433;Database=payment_gateway;Username=postgres;Password=postgres";
        var connectionString = Environment.GetEnvironmentVariable(
            "ConnectionStrings__PaymentGateway") ?? localConnection;
        var options = new DbContextOptionsBuilder<PaymentGatewayDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new PaymentGatewayDbContext(options);
    }
}
