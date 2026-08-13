using FicMart.PaymentGateway.Domain.Identifiers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FicMart.PaymentGateway.Infrastructure.Persistence;

internal sealed class CaptureAttemptRecordConfiguration
    : IEntityTypeConfiguration<CaptureAttemptRecord>
{
    public void Configure(EntityTypeBuilder<CaptureAttemptRecord> builder)
    {
        builder.ToTable(
            "capture_attempts",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_capture_attempts_status",
                    "status IN ('Pending', 'Succeeded', 'Rejected', 'Unknown')");
                table.HasCheckConstraint(
                    "ck_capture_attempts_bank_id",
                    "(status = 'Succeeded' AND bank_capture_id IS NOT NULL) OR " +
                    "(status <> 'Succeeded' AND bank_capture_id IS NULL)");
                table.HasCheckConstraint(
                    "ck_capture_attempts_timestamps",
                    "updated_at >= created_at");
            });

        builder.HasKey(attempt => attempt.Id);
        builder.Property(attempt => attempt.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(attempt => attempt.PaymentId).HasColumnName("payment_id");
        builder.Property(attempt => attempt.BankIdempotencyKey)
            .HasColumnName("bank_idempotency_key");
        builder.Property(attempt => attempt.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(16);
        builder.Property(attempt => attempt.BankCaptureId)
            .HasColumnName("bank_capture_id")
            .HasMaxLength(BankCaptureId.MaximumLength);
        builder.Property(attempt => attempt.CreatedAt).HasColumnName("created_at");
        builder.Property(attempt => attempt.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne<PaymentRecord>()
            .WithMany()
            .HasForeignKey(attempt => attempt.PaymentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(attempt => attempt.PaymentId)
            .IsUnique()
            .HasDatabaseName("ix_capture_attempts_payment_id");
        builder.HasIndex(attempt => attempt.BankIdempotencyKey)
            .IsUnique()
            .HasDatabaseName("ux_capture_attempts_bank_idempotency_key");
        builder.HasIndex(attempt => attempt.BankCaptureId)
            .IsUnique()
            .HasDatabaseName("ux_capture_attempts_bank_capture_id");
    }
}
