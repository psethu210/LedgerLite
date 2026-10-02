using LedgerLite.Domain;
using LedgerLite.Domain.Exceptions;
using LedgerLite.Infrastructure.Data.Configurations;
using LedgerLite.Infrastructure.Services;

using Microsoft.EntityFrameworkCore;

namespace LedgerLite.Infrastructure.Data;

/// <summary>
/// EF Core context for the LedgerLite database.
/// Enforces append-only ledger entries and double-entry bookkeeping rules.
/// </summary>
public class LedgerContext : DbContext
{
    public LedgerContext(DbContextOptions<LedgerContext> options) : base(options)
    {
    }

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Transfer> Transfers => Set<Transfer>();
    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new AccountConfiguration());
        modelBuilder.ApplyConfiguration(new TransferConfiguration());
        modelBuilder.ApplyConfiguration(new LedgerEntryConfiguration());
        modelBuilder.ApplyConfiguration(new IdempotencyRecordConfiguration());
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);

        // Enable sensitive data logging in development only
        if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
        {
            optionsBuilder.EnableSensitiveDataLogging();
        }
    }

    public override int SaveChanges()
    {
        EnforceLedgerEntryImmutability();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        EnforceLedgerEntryImmutability();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void EnforceLedgerEntryImmutability()
    {
        var modifiedEntries = ChangeTracker.Entries<LedgerEntry>()
            .Where(e => e.State == EntityState.Modified || e.State == EntityState.Deleted);

        if (modifiedEntries.Any())
        {
            throw new LedgerEntryImmutableException();
        }
    }
}