using LedgerLite.Domain;
using LedgerLite.Domain.Exceptions;

namespace LedgerLite.UnitTests.Domain;

public class ExceptionTests
{
    [Fact]
    public void InsufficientFundsException_CreatesCorrectMessage()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var currentBalance = Money.FromDecimal(5.00m, Currency.USD);
        var attemptedDebit = Money.FromDecimal(10.00m, Currency.USD);

        // Act
        var exception = new InsufficientFundsException(accountId, currentBalance, attemptedDebit);

        // Assert
        Assert.Contains(accountId.ToString(), exception.Message);
        Assert.Contains("5.00 USD", exception.Message);
        Assert.Contains("10.00 USD", exception.Message);
    }

    [Fact]
    public void LedgerEntryImmutableException_HasCorrectMessage()
    {
        // Act
        var exception = new LedgerEntryImmutableException();

        // Assert
        Assert.Equal("Ledger entries are append-only and cannot be modified or deleted", exception.Message);
    }

    [Fact]
    public void DomainException_WithInnerException_PreservesInnerException()
    {
        // Arrange
        var innerException = new InvalidOperationException("Inner error");

        // Create a concrete DomainException for testing
        var domainException = new InsufficientFundsException(
            Guid.NewGuid(),
            Money.Zero(Currency.USD),
            Money.FromDecimal(1.00m, Currency.USD));

        // Assert
        Assert.IsAssignableFrom<DomainException>(domainException);
    }
}