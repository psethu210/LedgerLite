using LedgerLite.Domain;
using LedgerLite.Domain.Exceptions;

namespace LedgerLite.UnitTests.Domain;

public class AccountTests
{
    [Fact]
    public void CreateCustomerAccount_SetsPropertiesCorrectly()
    {
        // Arrange & Act
        var account = Account.CreateCustomerAccount("user123", Currency.USD);

        // Assert
        Assert.Equal("user123", account.OwnerRef);
        Assert.Equal(Currency.USD, account.Currency);
        Assert.Equal(AccountKind.Customer, account.Kind);
        Assert.Equal(0, account.BalanceMinor);
        Assert.False(account.AllowNegative);
        Assert.NotEqual(Guid.Empty, account.Id);
    }

    [Fact]
    public void CreateExternalFundingAccount_SetsPropertiesCorrectly()
    {
        // Arrange & Act
        var account = Account.CreateExternalFundingAccount(Currency.USD);

        // Assert
        Assert.Equal("EXTERNAL_FUNDING_USD", account.OwnerRef);
        Assert.Equal(Currency.USD, account.Currency);
        Assert.Equal(AccountKind.System, account.Kind);
        Assert.True(account.AllowNegative);
    }

    [Fact]
    public void Credit_IncreasesBalance()
    {
        // Arrange
        var account = Account.CreateCustomerAccount("user123", Currency.USD);
        var amount = Money.FromDecimal(10.00m, Currency.USD);

        // Act
        account.Credit(amount);

        // Assert
        Assert.Equal(1000, account.BalanceMinor);
        Assert.Equal(10.00m, account.Balance.ToDecimal());
    }

    [Fact]
    public void Debit_DecreasesBalance()
    {
        // Arrange
        var account = Account.CreateCustomerAccount("user123", Currency.USD);
        account.Credit(Money.FromDecimal(20.00m, Currency.USD)); // Start with $20
        var debitAmount = Money.FromDecimal(5.00m, Currency.USD);

        // Act
        account.Debit(debitAmount);

        // Assert
        Assert.Equal(1500, account.BalanceMinor); // $15 remaining
    }

    [Fact]
    public void Debit_CustomerAccount_InsufficientFunds_ThrowsException()
    {
        // Arrange
        var account = Account.CreateCustomerAccount("user123", Currency.USD);
        account.Credit(Money.FromDecimal(5.00m, Currency.USD)); // Only $5
        var debitAmount = Money.FromDecimal(10.00m, Currency.USD); // Try to debit $10

        // Act & Assert
        Assert.Throws<InsufficientFundsException>(() => account.Debit(debitAmount));
    }

    [Fact]
    public void Debit_SystemAccount_CanGoNegative()
    {
        // Arrange
        var account = Account.CreateExternalFundingAccount(Currency.USD);
        var debitAmount = Money.FromDecimal(10.00m, Currency.USD);

        // Act
        account.Debit(debitAmount);

        // Assert
        Assert.Equal(-1000, account.BalanceMinor);
        Assert.True(account.Balance.IsNegative);
    }

    [Fact]
    public void Credit_WrongCurrency_ThrowsException()
    {
        // Arrange
        var usdAccount = Account.CreateCustomerAccount("user123", Currency.USD);
        var cadAmount = Money.FromDecimal(10.00m, Currency.CAD);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => usdAccount.Credit(cadAmount));
    }

    [Fact]
    public void Debit_NegativeAmount_ThrowsException()
    {
        // Arrange
        var account = Account.CreateCustomerAccount("user123", Currency.USD);
        var negativeAmount = new Money(-1000, Currency.USD);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => account.Debit(negativeAmount));
    }

    [Fact]
    public void CreateCustomerAccount_EmptyOwnerRef_ThrowsException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => Account.CreateCustomerAccount("", Currency.USD));
        Assert.Throws<ArgumentException>(() => Account.CreateCustomerAccount("   ", Currency.USD));
    }
}