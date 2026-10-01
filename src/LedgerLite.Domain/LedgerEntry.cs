namespace LedgerLite.Domain;

/// <summary>
/// A ledger entry records one side of a double-entry transaction.
/// Entries are append-only: they can never be updated or deleted, only corrected with new reversing entries.
/// </summary>
public class LedgerEntry
{
    public long Id { get; private set; }
    
    /// <summary>
    /// The transfer this entry belongs to.
    /// </summary>
    public Guid TransferId { get; private set; }
    
    /// <summary>
    /// The account this entry affects.
    /// </summary>
    public Guid AccountId { get; private set; }
    
    public Currency Currency { get; private set; }
    
    /// <summary>
    /// The amount in minor units. Positive = credit, negative = debit.
    /// </summary>
    public long AmountMinor { get; private set; }
    
    public DateTime CreatedAt { get; private set; }
    
    // Navigation properties
    public Account Account { get; private set; } = null!;
    
    // EF Core constructor
    private LedgerEntry() { }
    
    public LedgerEntry(Guid transferId, Guid accountId, Currency currency, long amountMinor)
    {
        TransferId = transferId;
        AccountId = accountId;
        Currency = currency;
        AmountMinor = amountMinor;
        CreatedAt = DateTime.UtcNow;
    }
    
    /// <summary>
    /// The amount as Money.
    /// </summary>
    public Money Amount => new(AmountMinor, Currency);
    
    /// <summary>
    /// True if this is a credit (positive amount).
    /// </summary>
    public bool IsCredit => AmountMinor > 0;
    
    /// <summary>
    /// True if this is a debit (negative amount).
    /// </summary>
    public bool IsDebit => AmountMinor < 0;
    
    /// <summary>
    /// Creates a credit entry (adds to account balance).
    /// </summary>
    public static LedgerEntry Credit(Guid transferId, Guid accountId, Money amount)
    {
        if (amount.MinorUnits <= 0)
            throw new ArgumentException("Credit amount must be positive", nameof(amount));
        
        return new LedgerEntry(transferId, accountId, amount.Currency, amount.MinorUnits);
    }
    
    /// <summary>
    /// Creates a debit entry (subtracts from account balance).
    /// </summary>
    public static LedgerEntry Debit(Guid transferId, Guid accountId, Money amount)
    {
        if (amount.MinorUnits <= 0)
            throw new ArgumentException("Debit amount must be positive", nameof(amount));
        
        return new LedgerEntry(transferId, accountId, amount.Currency, -amount.MinorUnits);
    }
}