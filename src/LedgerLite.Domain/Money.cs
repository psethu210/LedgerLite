namespace LedgerLite.Domain;

/// <summary>
/// Represents a monetary amount stored as minor units (cents) to avoid floating-point precision issues.
/// Example: $12.34 USD is stored as 1234 minor units.
/// </summary>
public readonly record struct Money(long MinorUnits, Currency Currency)
{
    /// <summary>
    /// Creates Money from a decimal amount. The decimal is converted to minor units using banker's rounding.
    /// Example: Money.FromDecimal(12.34m, Currency.USD) creates Money(1234, Currency.USD).
    /// </summary>
    public static Money FromDecimal(decimal amount, Currency currency)
    {
        var minorUnits = Math.Round(amount * currency.MinorUnitsPerMajor(), MidpointRounding.ToEven);
        return new Money((long)minorUnits, currency);
    }
    
    /// <summary>
    /// Creates Money from major units (dollars, euros, etc.).
    /// Example: Money.FromMajor(12, Currency.USD) creates Money(1200, Currency.USD).
    /// </summary>
    public static Money FromMajor(long majorUnits, Currency currency)
    {
        return new Money(majorUnits * currency.MinorUnitsPerMajor(), currency);
    }
    
    /// <summary>
    /// Converts to decimal for display purposes.
    /// Example: Money(1234, Currency.USD).ToDecimal() returns 12.34m.
    /// </summary>
    public decimal ToDecimal()
    {
        return (decimal)MinorUnits / Currency.MinorUnitsPerMajor();
    }
    
    /// <summary>
    /// Zero amount in the specified currency.
    /// </summary>
    public static Money Zero(Currency currency) => new(0, currency);
    
    /// <summary>
    /// Returns true if the amount is zero.
    /// </summary>
    public bool IsZero => MinorUnits == 0;
    
    /// <summary>
    /// Returns true if the amount is positive.
    /// </summary>
    public bool IsPositive => MinorUnits > 0;
    
    /// <summary>
    /// Returns true if the amount is negative.
    /// </summary>
    public bool IsNegative => MinorUnits < 0;
    
    /// <summary>
    /// Adds two Money amounts. They must be in the same currency.
    /// </summary>
    public static Money operator +(Money left, Money right)
    {
        if (left.Currency != right.Currency)
            throw new InvalidOperationException($"Cannot add {left.Currency} and {right.Currency}");
        
        return new Money(left.MinorUnits + right.MinorUnits, left.Currency);
    }
    
    /// <summary>
    /// Subtracts two Money amounts. They must be in the same currency.
    /// </summary>
    public static Money operator -(Money left, Money right)
    {
        if (left.Currency != right.Currency)
            throw new InvalidOperationException($"Cannot subtract {right.Currency} from {left.Currency}");
        
        return new Money(left.MinorUnits - right.MinorUnits, left.Currency);
    }
    
    /// <summary>
    /// Negates a Money amount.
    /// </summary>
    public static Money operator -(Money money)
    {
        return new Money(-money.MinorUnits, money.Currency);
    }
    
    public override string ToString()
    {
        return $"{ToDecimal():F2} {Currency}";
    }
}