namespace LedgerLite.Domain.Exceptions;

/// <summary>
/// Base exception for domain rule violations.
/// </summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }
    protected DomainException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Thrown when an account would go negative but is not allowed to.
/// </summary>
public class InsufficientFundsException : DomainException
{
    public InsufficientFundsException(Guid accountId, Money currentBalance, Money attemptedDebit)
        : base($"Account {accountId} has balance {currentBalance} but attempted debit of {attemptedDebit}")
    {
    }
}

/// <summary>
/// Thrown when trying to modify ledger entries, which are append-only.
/// </summary>
public class LedgerEntryImmutableException : DomainException
{
    public LedgerEntryImmutableException()
        : base("Ledger entries are append-only and cannot be modified or deleted")
    {
    }
}