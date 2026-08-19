using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FicMart.PaymentGateway.Infrastructure.Persistence;

internal sealed class ReconciliationRecordConfiguration : IEntityTypeConfiguration<ReconciliationRecord>
{
    public void Configure(EntityTypeBuilder<ReconciliationRecord> builder)
    {
        builder.ToTable("reconciliation_records", table =>
        {
            table.HasCheckConstraint(
                "ck_reconciliation_records_operation",
                "operation IS NULL OR operation IN ('Authorize', 'Capture', 'Void', 'Refund')");
            table.HasCheckConstraint(
                "ck_reconciliation_records_outcome",
                "outcome IN ('Recovered', 'NoActionRequired', 'OperatorReviewRequired', 'AlreadyProcessing', 'StillPending')");
        });
        builder.HasKey(record => record.Id);
        builder.Property(record => record.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(record => record.PaymentId).HasColumnName("payment_id");
        builder.Property(record => record.Operation).HasColumnName("operation").HasConversion<string>().HasMaxLength(16);
        builder.Property(record => record.Outcome).HasColumnName("outcome").HasConversion<string>().HasMaxLength(32);
        builder.Property(record => record.DetailCode).HasColumnName("detail_code").HasMaxLength(64);
        builder.Property(record => record.CreatedAt).HasColumnName("created_at");
        builder.HasOne<PaymentRecord>().WithMany().HasForeignKey(record => record.PaymentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(record => new { record.PaymentId, record.CreatedAt })
            .HasDatabaseName("ix_reconciliation_records_payment_created_at");
    }
}
