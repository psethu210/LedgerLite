using LedgerLite.Domain.Exceptions;

namespace LedgerLite.Domain;

/// <summary>
/// Type of account: Customer accounts hold user funds, System accounts handle internal operations.
/// </summary>
public enum AccountKind
{
    Customer,
    System
}

/// <summary>
/// An account holds a balance in one currency. Customer accounts cannot go negative.
/// System accounts (like EXTERNAL_FUNDING_USD) can go negative for operational purposes.
/// </summary>
public class Account
{
    public Guid Id { get; private set; }

    /// <summary>
    /// External identifier for the account owner (user ID, system account name, etc.)
    /// </summary>
    public string OwnerRef { get; private set; } = string.Empty;

    public Currency Currency { get; private set; }
    public AccountKind Kind { get; private set; }

    /// <summary>
    /// Current balance stored as minor units (cents).
    /// </summary>
    public long BalanceMinor { get; private set; }

    /// <summary>
    /// Whether this account can have a negative balance.
    /// Customer accounts: false, System accounts: true.
    /// </summary>
    public bool AllowNegative => Kind == AccountKind.System;

    /// <summary>
    /// Row version for optimistic concurrency control.
    /// </summary>
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public DateTime CreatedAt { get; private set; }

    // EF Core constructor
    private Account() { }

    public Account(string ownerRef, Currency currency, AccountKind kind)
    {
        Id = Guid.NewGuid();
        OwnerRef = ownerRef ?? throw new ArgumentNullException(nameof(ownerRef));
        Currency = currency;
        Kind = kind;
        BalanceMinor = 0;
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Current balance as Money.
    /// </summary>
    public Money Balance => new(BalanceMinor, Currency);

    /// <summary>
    /// Debits (subtracts from) this account. Throws if this would make a Customer account negative.
    /// </summary>
    public void Debit(Money amount)
    {
        if (amount.Currency != Currency)
            throw new InvalidOperationException($"Cannot debit {amount.Currency} from {Currency} account");

        if (amount.MinorUnits < 0)
            throw new ArgumentException("Debit amount must be positive", nameof(amount));

        var newBalance = BalanceMinor - amount.MinorUnits;

        if (!AllowNegative && newBalance < 0)
            throw new InsufficientFundsException(Id, Balance, amount);

        BalanceMinor = newBalance;
    }

    /// <summary>
    /// Credits (adds to) this account.
    /// </summary>
    public void Credit(Money amount)
    {
        if (amount.Currency != Currency)
            throw new InvalidOperationException($"Cannot credit {amount.Currency} to {Currency} account");

        if (amount.MinorUnits < 0)
            throw new ArgumentException("Credit amount must be positive", nameof(amount));

        BalanceMinor += amount.MinorUnits;
    }

    /// <summary>
    /// Creates a system account for external funding. These accounts can go negative.
    /// </summary>
    public static Account CreateExternalFundingAccount(Currency currency)
    {
        return new Account($"EXTERNAL_FUNDING_{currency}", currency, AccountKind.System);
    }

    /// <summary>
    /// Creates a customer account for a user.
    /// </summary>
    public static Account CreateCustomerAccount(string ownerRef, Currency currency)
    {
        if (string.IsNullOrWhiteSpace(ownerRef))
            throw new ArgumentException("Owner reference cannot be empty", nameof(ownerRef));

        return new Account(ownerRef, currency, AccountKind.Customer);
    }
}