using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FicMart.PaymentGateway.Infrastructure.Persistence;

internal sealed class AuditRecordConfiguration : IEntityTypeConfiguration<AuditRecord>
{
    public void Configure(EntityTypeBuilder<AuditRecord> builder)
    {
        builder.ToTable("audit_records");
        builder.HasKey(record => record.Id);
        builder.Property(record => record.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(record => record.PaymentId).HasColumnName("payment_id");
        builder.Property(record => record.EventType).HasColumnName("event_type").HasMaxLength(64);
        builder.Property(record => record.PreviousStatus).HasColumnName("previous_status").HasMaxLength(32);
        builder.Property(record => record.NewStatus).HasColumnName("new_status").HasMaxLength(32);
        builder.Property(record => record.Actor).HasColumnName("actor").HasMaxLength(64);
        builder.Property(record => record.OccurredAt).HasColumnName("occurred_at");
        builder.HasOne<PaymentRecord>().WithMany().HasForeignKey(record => record.PaymentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(record => new { record.PaymentId, record.OccurredAt }).HasDatabaseName("ix_audit_records_payment_occurred_at");
    }
}
