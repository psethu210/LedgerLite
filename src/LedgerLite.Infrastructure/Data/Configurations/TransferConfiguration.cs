using LedgerLite.Domain;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LedgerLite.Infrastructure.Data.Configurations;

public class TransferConfiguration : IEntityTypeConfiguration<Transfer>
{
    public void Configure(EntityTypeBuilder<Transfer> builder)
    {
        builder.ToTable("Transfers");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.ClientId)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(t => t.IdempotencyKey)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(t => t.FromAccountId)
            .IsRequired();

        builder.Property(t => t.ToAccountId)
            .IsRequired();

        builder.Property(t => t.AmountMinor)
            .IsRequired();

        builder.Property(t => t.SourceCurrency)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(3);

        builder.Property(t => t.TargetCurrency)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(3);

        builder.Property(t => t.ConvertedAmountMinor)
            .IsRequired();

        builder.Property(t => t.FxRateId)
            .IsRequired(false);

        builder.Property(t => t.Status)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(t => t.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("GETUTCDATE()");

        // Unique constraint for idempotency: one transfer per (ClientId, IdempotencyKey)
        builder.HasIndex(t => new { t.ClientId, t.IdempotencyKey })
            .IsUnique()
            .HasDatabaseName("IX_Transfers_ClientId_IdempotencyKey_Unique");

        // Index for queries by account
        builder.HasIndex(t => t.FromAccountId)
            .HasDatabaseName("IX_Transfers_FromAccountId");

        builder.HasIndex(t => t.ToAccountId)
            .HasDatabaseName("IX_Transfers_ToAccountId");

        // Index for status queries
        builder.HasIndex(t => new { t.Status, t.CreatedAt })
            .HasDatabaseName("IX_Transfers_Status_CreatedAt");

        // Navigation properties
        builder.HasOne(t => t.FromAccount)
            .WithMany()
            .HasForeignKey(t => t.FromAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.ToAccount)
            .WithMany()
            .HasForeignKey(t => t.ToAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(t => t.Entries)
            .WithOne()
            .HasForeignKey(e => e.TransferId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}