using LedgerLite.Infrastructure.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LedgerLite.Infrastructure.Data.Configurations;

public class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("IdempotencyRecords");

        // Composite primary key
        builder.HasKey(r => new { r.ClientId, r.Key });

        builder.Property(r => r.ClientId)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(r => r.Key)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(r => r.RequestHash)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(r => r.ResponseStatus)
            .IsRequired();

        builder.Property(r => r.ResponseBody)
            .IsRequired();

        builder.Property(r => r.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("GETUTCDATE()");

        // Index for cleanup queries
        builder.HasIndex(r => r.CreatedAt)
            .HasDatabaseName("IX_IdempotencyRecords_CreatedAt");
    }
}