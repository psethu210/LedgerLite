using LedgerLite.Domain;

namespace LedgerLite.UnitTests.Domain;

public class LedgerEntryTests
{
    [Fact]
    public void Credit_CreatesPositiveEntry()
    {
        // Arrange
        var transferId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var amount = Money.FromDecimal(10.00m, Currency.USD);

        // Act
        var entry = LedgerEntry.Credit(transferId, accountId, amount);

        // Assert
        Assert.Equal(transferId, entry.TransferId);
        Assert.Equal(accountId, entry.AccountId);
        Assert.Equal(Currency.USD, entry.Currency);
        Assert.Equal(1000, entry.AmountMinor);
        Assert.True(entry.IsCredit);
        Assert.False(entry.IsDebit);
    }

    [Fact]
    public void Debit_CreatesNegativeEntry()
    {
        // Arrange
        var transferId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var amount = Money.FromDecimal(10.00m, Currency.USD);

        // Act
        var entry = LedgerEntry.Debit(transferId, accountId, amount);

        // Assert
        Assert.Equal(transferId, entry.TransferId);
        Assert.Equal(accountId, entry.AccountId);
        Assert.Equal(Currency.USD, entry.Currency);
        Assert.Equal(-1000, entry.AmountMinor);
        Assert.False(entry.IsCredit);
        Assert.True(entry.IsDebit);
    }

    [Fact]
    public void Credit_ZeroAmount_ThrowsException()
    {
        // Arrange
        var transferId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var zeroAmount = Money.Zero(Currency.USD);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => LedgerEntry.Credit(transferId, accountId, zeroAmount));
    }

    [Fact]
    public void Debit_NegativeAmount_ThrowsException()
    {
        // Arrange
        var transferId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var negativeAmount = new Money(-1000, Currency.USD);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => LedgerEntry.Debit(transferId, accountId, negativeAmount));
    }

    [Fact]
    public void Amount_Property_ReturnsCorrectMoney()
    {
        // Arrange
        var transferId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var originalAmount = Money.FromDecimal(15.75m, Currency.EUR);

        // Act
        var creditEntry = LedgerEntry.Credit(transferId, accountId, originalAmount);
        var debitEntry = LedgerEntry.Debit(transferId, accountId, originalAmount);

        // Assert
        Assert.Equal(originalAmount, creditEntry.Amount);
        Assert.Equal(-originalAmount, debitEntry.Amount);
    }
}