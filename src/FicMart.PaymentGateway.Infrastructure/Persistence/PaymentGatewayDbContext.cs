using Microsoft.EntityFrameworkCore;

namespace FicMart.PaymentGateway.Infrastructure.Persistence;

public sealed class PaymentGatewayDbContext(DbContextOptions<PaymentGatewayDbContext> options)
    : DbContext(options)
{
    internal DbSet<PaymentRecord> Payments => Set<PaymentRecord>();

    internal DbSet<AuthorizationAttemptRecord> AuthorizationAttempts =>
        Set<AuthorizationAttemptRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PaymentGatewayDbContext).Assembly);
}
