using LedgerLite.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LedgerLite.Infrastructure.Data.Configurations;

public class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("Accounts", t => 
            t.HasCheckConstraint("CK_Accounts_BalanceMinor_NonNegative_CustomerAccounts", 
                                "BalanceMinor >= 0 OR Kind = 'System'"));
        
        builder.HasKey(a => a.Id);
        
        builder.Property(a => a.OwnerRef)
            .IsRequired()
            .HasMaxLength(100);
        
        builder.Property(a => a.Currency)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(3);
        
        builder.Property(a => a.Kind)
            .IsRequired()
            .HasConversion<string>();
        
        builder.Property(a => a.BalanceMinor)
            .IsRequired();
        
        builder.Property(a => a.RowVersion)
            .IsRequired()
            .IsRowVersion();
        
        builder.Property(a => a.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("GETUTCDATE()");
        
        // Index for efficient queries by owner and currency
        builder.HasIndex(a => new { a.OwnerRef, a.Currency })
            .HasDatabaseName("IX_Accounts_OwnerRef_Currency");
        
        // Index for system account queries
        builder.HasIndex(a => new { a.Kind, a.Currency })
            .HasDatabaseName("IX_Accounts_Kind_Currency");
    }
}