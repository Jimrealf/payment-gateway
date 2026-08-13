using FicMart.PaymentGateway.Domain.AuthorizationAttempts;
using FicMart.PaymentGateway.Domain.Identifiers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FicMart.PaymentGateway.Infrastructure.Persistence;

internal sealed class AuthorizationAttemptRecordConfiguration
    : IEntityTypeConfiguration<AuthorizationAttemptRecord>
{
    public void Configure(EntityTypeBuilder<AuthorizationAttemptRecord> builder)
    {
        builder.ToTable(
            "authorization_attempts",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_authorization_attempts_status",
                    "status IN ('Pending', 'Succeeded', 'Rejected', 'Unknown')");
                table.HasCheckConstraint(
                    "ck_authorization_attempts_bank_id",
                    "(status = 'Succeeded' AND bank_authorization_id IS NOT NULL) OR " +
                    "(status <> 'Succeeded' AND bank_authorization_id IS NULL)");
                table.HasCheckConstraint(
                    "ck_authorization_attempts_timestamps",
                    "updated_at >= created_at");
            });

        builder.HasKey(attempt => attempt.Id);

        builder.Property(attempt => attempt.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(attempt => attempt.PaymentId).HasColumnName("payment_id").IsRequired();
        builder.Property(attempt => attempt.BankIdempotencyKey)
            .HasColumnName("bank_idempotency_key")
            .IsRequired();
        builder.Property(attempt => attempt.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();
        builder.Property(attempt => attempt.BankAuthorizationId)
            .HasColumnName("bank_authorization_id")
            .HasMaxLength(BankAuthorizationId.MaximumLength);
        builder.Property(attempt => attempt.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(attempt => attempt.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasOne<PaymentRecord>()
            .WithMany()
            .HasForeignKey(attempt => attempt.PaymentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(attempt => attempt.PaymentId)
            .HasDatabaseName("ix_authorization_attempts_payment_id");
        builder.HasIndex(attempt => attempt.BankIdempotencyKey)
            .IsUnique()
            .HasDatabaseName("ux_authorization_attempts_bank_idempotency_key");
        builder.HasIndex(attempt => attempt.BankAuthorizationId)
            .IsUnique()
            .HasDatabaseName("ux_authorization_attempts_bank_authorization_id");
    }
}
