using LedgerLite.Domain;
using LedgerLite.Domain.Exceptions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LedgerLite.Infrastructure.Data.Configurations;

public class LedgerEntryConfiguration : IEntityTypeConfiguration<LedgerEntry>
{
    public void Configure(EntityTypeBuilder<LedgerEntry> builder)
    {
        builder.ToTable("LedgerEntries");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .UseIdentityColumn(); // Auto-incrementing ID

        builder.Property(e => e.TransferId)
            .IsRequired();

        builder.Property(e => e.AccountId)
            .IsRequired();

        builder.Property(e => e.Currency)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(3);

        builder.Property(e => e.AmountMinor)
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("GETUTCDATE()");

        // Index for queries by account (most common query pattern)
        builder.HasIndex(e => new { e.AccountId, e.CreatedAt })
            .HasDatabaseName("IX_LedgerEntries_AccountId_CreatedAt");

        // Index for queries by transfer
        builder.HasIndex(e => e.TransferId)
            .HasDatabaseName("IX_LedgerEntries_TransferId");

        // Index for reconciliation queries by currency
        builder.HasIndex(e => new { e.Currency, e.CreatedAt })
            .HasDatabaseName("IX_LedgerEntries_Currency_CreatedAt");

        // Navigation property
        builder.HasOne(e => e.Account)
            .WithMany()
            .HasForeignKey(e => e.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        // Prevent updates and deletes on LedgerEntries (append-only)
        // This will be enforced at the SaveChanges level
    }
}