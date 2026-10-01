using LedgerLite.Domain;

namespace LedgerLite.UnitTests.Domain;

public class MoneyTests
{
    [Fact]
    public void FromDecimal_ConvertsCorrectly()
    {
        // Arrange & Act
        var money = Money.FromDecimal(12.34m, Currency.USD);
        
        // Assert
        Assert.Equal(1234, money.MinorUnits);
        Assert.Equal(Currency.USD, money.Currency);
    }
    
    [Fact]
    public void FromDecimal_RoundsBankersRounding()
    {
        // Arrange & Act
        var money1 = Money.FromDecimal(12.345m, Currency.USD); // Round down (even)
        var money2 = Money.FromDecimal(12.355m, Currency.USD); // Round up (even)
        
        // Assert
        // Banker's rounding: 12.345 -> 12.34 (round to even), 12.355 -> 12.36 (round to even)
        Assert.Equal(1234, money1.MinorUnits); // 12.34
        Assert.Equal(1236, money2.MinorUnits); // 12.36
    }
    
    [Fact]
    public void ToDecimal_ConvertsCorrectly()
    {
        // Arrange
        var money = new Money(1234, Currency.USD);
        
        // Act
        var result = money.ToDecimal();
        
        // Assert
        Assert.Equal(12.34m, result);
    }
    
    [Fact]
    public void Add_SameCurrency_Works()
    {
        // Arrange
        var money1 = new Money(1000, Currency.USD);
        var money2 = new Money(500, Currency.USD);
        
        // Act
        var result = money1 + money2;
        
        // Assert
        Assert.Equal(1500, result.MinorUnits);
        Assert.Equal(Currency.USD, result.Currency);
    }
    
    [Fact]
    public void Add_DifferentCurrency_ThrowsException()
    {
        // Arrange
        var usd = new Money(1000, Currency.USD);
        var cad = new Money(1000, Currency.CAD);
        
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => usd + cad);
    }
    
    [Fact]
    public void Subtract_SameCurrency_Works()
    {
        // Arrange
        var money1 = new Money(1500, Currency.USD);
        var money2 = new Money(500, Currency.USD);
        
        // Act
        var result = money1 - money2;
        
        // Assert
        Assert.Equal(1000, result.MinorUnits);
    }
    
    [Fact]
    public void Negate_Works()
    {
        // Arrange
        var money = new Money(1000, Currency.USD);
        
        // Act
        var result = -money;
        
        // Assert
        Assert.Equal(-1000, result.MinorUnits);
        Assert.Equal(Currency.USD, result.Currency);
    }
    
    [Theory]
    [InlineData(0, true, false, false)]
    [InlineData(100, false, true, false)]
    [InlineData(-100, false, false, true)]
    public void IsZero_IsPositive_IsNegative_Work(long minorUnits, bool expectedZero, bool expectedPositive, bool expectedNegative)
    {
        // Arrange
        var money = new Money(minorUnits, Currency.USD);
        
        // Assert
        Assert.Equal(expectedZero, money.IsZero);
        Assert.Equal(expectedPositive, money.IsPositive);
        Assert.Equal(expectedNegative, money.IsNegative);
    }
    
    [Fact]
    public void ToString_FormatsCorrectly()
    {
        // Arrange
        var money = new Money(1234, Currency.USD);
        
        // Act
        var result = money.ToString();
        
        // Assert
        Assert.Equal("12.34 USD", result);
    }
}