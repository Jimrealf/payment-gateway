using FicMart.PaymentGateway.Domain.Identifiers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FicMart.PaymentGateway.Infrastructure.Persistence;

internal sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable(
            "idempotency_records",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_idempotency_records_operation",
                    "operation IN ('Authorize', 'Capture', 'Void', 'Refund')");
                table.HasCheckConstraint(
                    "ck_idempotency_records_state",
                    "state IN ('Processing', 'Retryable', 'Completed')");
                table.HasCheckConstraint(
                    "ck_idempotency_records_fingerprint",
                    "char_length(request_fingerprint) = 64");
                table.HasCheckConstraint(
                    "ck_idempotency_records_timestamps",
                    "updated_at >= created_at");
            });

        builder.HasKey(record => new { record.Operation, record.Key });
        builder.Property(record => record.Key)
            .HasColumnName("key")
            .HasMaxLength(GatewayIdempotencyKey.MaximumLength);
        builder.Property(record => record.Operation)
            .HasColumnName("operation")
            .HasConversion<string>()
            .HasMaxLength(16);
        builder.Property(record => record.RequestFingerprint)
            .HasColumnName("request_fingerprint")
            .HasColumnType("character(64)");
        builder.Property(record => record.PaymentId).HasColumnName("payment_id");
        builder.Property(record => record.State)
            .HasColumnName("state")
            .HasConversion<string>()
            .HasMaxLength(16);
        builder.Property(record => record.CreatedAt).HasColumnName("created_at");
        builder.Property(record => record.UpdatedAt).HasColumnName("updated_at");
        builder.HasOne<PaymentRecord>()
            .WithMany()
            .HasForeignKey(record => record.PaymentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(record => record.PaymentId)
            .HasDatabaseName("ix_idempotency_records_payment_id");
    }
}
