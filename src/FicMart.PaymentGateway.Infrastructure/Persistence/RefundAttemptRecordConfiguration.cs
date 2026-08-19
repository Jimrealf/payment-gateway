using FicMart.PaymentGateway.Domain.Identifiers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FicMart.PaymentGateway.Infrastructure.Persistence;

internal sealed class RefundAttemptRecordConfiguration : IEntityTypeConfiguration<RefundAttemptRecord>
{
    public void Configure(EntityTypeBuilder<RefundAttemptRecord> builder)
    {
        builder.ToTable("refund_attempts", table =>
        {
            table.HasCheckConstraint("ck_refund_attempts_status", "status IN ('Pending', 'Succeeded', 'Rejected', 'Unknown')");
            table.HasCheckConstraint("ck_refund_attempts_bank_id", "(status = 'Succeeded' AND bank_refund_id IS NOT NULL) OR (status <> 'Succeeded' AND bank_refund_id IS NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.PaymentId).HasColumnName("payment_id");
        builder.Property(x => x.BankIdempotencyKey).HasColumnName("bank_idempotency_key");
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(16);
        builder.Property(x => x.BankRefundId).HasColumnName("bank_refund_id").HasMaxLength(BankRefundId.MaximumLength);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.HasOne<PaymentRecord>().WithMany().HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.PaymentId).IsUnique().HasDatabaseName("ux_refund_attempts_payment_id");
        builder.HasIndex(x => x.BankIdempotencyKey).IsUnique().HasDatabaseName("ux_refund_attempts_bank_idempotency_key");
        builder.HasIndex(x => x.BankRefundId).IsUnique().HasDatabaseName("ux_refund_attempts_bank_refund_id");
    }
}
