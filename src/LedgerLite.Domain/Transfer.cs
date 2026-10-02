namespace LedgerLite.Domain;

/// <summary>
/// Status of a transfer operation.
/// </summary>
public enum TransferStatus
{
    Pending,
    Completed,
    Failed
}

/// <summary>
/// A transfer represents a movement of money between accounts.
/// Each transfer generates ledger entries that must sum to zero (double-entry bookkeeping).
/// </summary>
public class Transfer
{
    public Guid Id { get; private set; }

    /// <summary>
    /// Client identifier for the API key that initiated this transfer.
    /// </summary>
    public string ClientId { get; private set; } = string.Empty;

    /// <summary>
    /// Idempotency key provided by the client.
    /// </summary>
    public string IdempotencyKey { get; private set; } = string.Empty;

    public Guid FromAccountId { get; private set; }
    public Guid ToAccountId { get; private set; }

    /// <summary>
    /// The amount in the source account's currency (minor units).
    /// </summary>
    public long AmountMinor { get; private set; }

    public Currency SourceCurrency { get; private set; }
    public Currency TargetCurrency { get; private set; }

    /// <summary>
    /// The amount in the target account's currency after FX conversion (minor units).
    /// For same-currency transfers, this equals AmountMinor.
    /// </summary>
    public long ConvertedAmountMinor { get; private set; }

    /// <summary>
    /// FX rate used for conversion, if applicable.
    /// </summary>
    public Guid? FxRateId { get; private set; }

    public TransferStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // Navigation properties
    public Account FromAccount { get; private set; } = null!;
    public Account ToAccount { get; private set; } = null!;
    public List<LedgerEntry> Entries { get; private set; } = new();

    // EF Core constructor
    private Transfer() { }

    public Transfer(
        string clientId,
        string idempotencyKey,
        Guid fromAccountId,
        Guid toAccountId,
        Money amount,
        Money? convertedAmount = null,
        Guid? fxRateId = null)
    {
        Id = Guid.NewGuid();
        ClientId = clientId ?? throw new ArgumentNullException(nameof(clientId));
        IdempotencyKey = idempotencyKey ?? throw new ArgumentNullException(nameof(idempotencyKey));
        FromAccountId = fromAccountId;
        ToAccountId = toAccountId;
        AmountMinor = amount.MinorUnits;
        SourceCurrency = amount.Currency;

        if (convertedAmount.HasValue)
        {
            ConvertedAmountMinor = convertedAmount.Value.MinorUnits;
            TargetCurrency = convertedAmount.Value.Currency;
        }
        else
        {
            ConvertedAmountMinor = AmountMinor;
            TargetCurrency = SourceCurrency;
        }

        FxRateId = fxRateId;
        Status = TransferStatus.Pending;
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// The source amount as Money.
    /// </summary>
    public Money Amount => new(AmountMinor, SourceCurrency);

    /// <summary>
    /// The converted amount as Money.
    /// </summary>
    public Money ConvertedAmount => new(ConvertedAmountMinor, TargetCurrency);

    /// <summary>
    /// True if this is a cross-currency transfer.
    /// </summary>
    public bool IsCrossCurrency => SourceCurrency != TargetCurrency;

    /// <summary>
    /// Marks the transfer as completed.
    /// </summary>
    public void MarkCompleted()
    {
        Status = TransferStatus.Completed;
    }

    /// <summary>
    /// Marks the transfer as failed.
    /// </summary>
    public void MarkFailed()
    {
        Status = TransferStatus.Failed;
    }

    /// <summary>
    /// Creates a deposit transfer from an external funding account.
    /// </summary>
    public static Transfer CreateDeposit(
        string clientId,
        string idempotencyKey,
        Guid externalFundingAccountId,
        Guid toAccountId,
        Money amount)
    {
        return new Transfer(clientId, idempotencyKey, externalFundingAccountId, toAccountId, amount);
    }
}