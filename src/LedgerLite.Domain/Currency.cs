namespace LedgerLite.Domain;

/// <summary>
/// Supported currencies. All have 2 decimal places (minor unit = cents).
/// </summary>
public enum Currency
{
    CAD,
    USD,
    EUR,
    INR
}

public static class CurrencyExtensions
{
    /// <summary>
    /// Number of digits after the decimal point for this currency.
    /// All supported currencies use 2 decimal places, so 100 minor units = 1 major unit.
    /// </summary>
    public static int DecimalPlaces(this Currency currency) => 2;

    /// <summary>
    /// Minor units per major unit. For 2 decimal places, this is 100.
    /// Example: 1.00 USD = 100 minor units (cents).
    /// </summary>
    public static long MinorUnitsPerMajor(this Currency currency) => 100;
}