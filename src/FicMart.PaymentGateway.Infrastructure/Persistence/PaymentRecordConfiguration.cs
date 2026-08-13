using FicMart.PaymentGateway.Domain.Identifiers;
using FicMart.PaymentGateway.Domain.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FicMart.PaymentGateway.Infrastructure.Persistence;

internal sealed class PaymentRecordConfiguration : IEntityTypeConfiguration<PaymentRecord>
{
    public void Configure(EntityTypeBuilder<PaymentRecord> builder)
    {
        builder.ToTable(
            "payments",
            table =>
            {
                table.HasCheckConstraint("ck_payments_amount_positive", "amount_minor_units > 0");
                table.HasCheckConstraint("ck_payments_currency_usd", "currency = 'USD'");
                table.HasCheckConstraint(
                    "ck_payments_status",
                    "status IN ('PendingAuthorization', 'Authorized', 'Declined', 'Captured', 'Voided', 'Refunded')");
                table.HasCheckConstraint(
                    "ck_payments_timestamps",
                    "updated_at >= created_at");
            });

        builder.HasKey(payment => payment.Id);

        builder.Property(payment => payment.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(payment => payment.OrderId)
            .HasColumnName("order_id")
            .HasMaxLength(OrderId.MaximumLength)
            .IsRequired();
        builder.Property(payment => payment.CustomerId)
            .HasColumnName("customer_id")
            .HasMaxLength(CustomerId.MaximumLength)
            .IsRequired();
        builder.Property(payment => payment.AmountMinorUnits)
            .HasColumnName("amount_minor_units")
            .IsRequired();
        builder.Property(payment => payment.Currency)
            .HasColumnName("currency")
            .HasColumnType("character(3)")
            .IsRequired();
        builder.Property(payment => payment.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(payment => payment.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(payment => payment.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(payment => payment.OrderId)
            .IsUnique()
            .HasDatabaseName("ux_payments_order_id");
    }
}
