using LedgerLite.Domain;

namespace LedgerLite.UnitTests.Domain;

public class TransferTests
{
    [Fact]
    public void Constructor_SameCurrency_SetsPropertiesCorrectly()
    {
        // Arrange
        var clientId = "test-client";
        var idempotencyKey = "test-key";
        var fromAccountId = Guid.NewGuid();
        var toAccountId = Guid.NewGuid();
        var amount = Money.FromDecimal(25.50m, Currency.USD);

        // Act
        var transfer = new Transfer(clientId, idempotencyKey, fromAccountId, toAccountId, amount);

        // Assert
        Assert.Equal(clientId, transfer.ClientId);
        Assert.Equal(idempotencyKey, transfer.IdempotencyKey);
        Assert.Equal(fromAccountId, transfer.FromAccountId);
        Assert.Equal(toAccountId, transfer.ToAccountId);
        Assert.Equal(2550, transfer.AmountMinor);
        Assert.Equal(Currency.USD, transfer.SourceCurrency);
        Assert.Equal(Currency.USD, transfer.TargetCurrency);
        Assert.Equal(2550, transfer.ConvertedAmountMinor);
        Assert.False(transfer.IsCrossCurrency);
        Assert.Equal(TransferStatus.Pending, transfer.Status);
        Assert.Null(transfer.FxRateId);
    }

    [Fact]
    public void Constructor_CrossCurrency_SetsPropertiesCorrectly()
    {
        // Arrange
        var fromAccountId = Guid.NewGuid();
        var toAccountId = Guid.NewGuid();
        var sourceAmount = Money.FromDecimal(100.00m, Currency.USD);
        var convertedAmount = Money.FromDecimal(135.00m, Currency.CAD);
        var fxRateId = Guid.NewGuid();

        // Act
        var transfer = new Transfer(
            "test-client",
            "test-key",
            fromAccountId,
            toAccountId,
            sourceAmount,
            convertedAmount,
            fxRateId);

        // Assert
        Assert.Equal(10000, transfer.AmountMinor);
        Assert.Equal(Currency.USD, transfer.SourceCurrency);
        Assert.Equal(13500, transfer.ConvertedAmountMinor);
        Assert.Equal(Currency.CAD, transfer.TargetCurrency);
        Assert.True(transfer.IsCrossCurrency);
        Assert.Equal(fxRateId, transfer.FxRateId);
    }

    [Fact]
    public void CreateDeposit_CreatesCorrectTransfer()
    {
        // Arrange
        var clientId = "deposit-client";
        var idempotencyKey = "deposit-key";
        var fundingAccountId = Guid.NewGuid();
        var customerAccountId = Guid.NewGuid();
        var amount = Money.FromDecimal(50.00m, Currency.EUR);

        // Act
        var transfer = Transfer.CreateDeposit(
            clientId,
            idempotencyKey,
            fundingAccountId,
            customerAccountId,
            amount);

        // Assert
        Assert.Equal(clientId, transfer.ClientId);
        Assert.Equal(idempotencyKey, transfer.IdempotencyKey);
        Assert.Equal(fundingAccountId, transfer.FromAccountId);
        Assert.Equal(customerAccountId, transfer.ToAccountId);
        Assert.Equal(amount, transfer.Amount);
        Assert.Equal(amount, transfer.ConvertedAmount);
        Assert.False(transfer.IsCrossCurrency);
    }

    [Fact]
    public void MarkCompleted_ChangesStatus()
    {
        // Arrange
        var transfer = CreateSampleTransfer();
        Assert.Equal(TransferStatus.Pending, transfer.Status);

        // Act
        transfer.MarkCompleted();

        // Assert
        Assert.Equal(TransferStatus.Completed, transfer.Status);
    }

    [Fact]
    public void MarkFailed_ChangesStatus()
    {
        // Arrange
        var transfer = CreateSampleTransfer();
        Assert.Equal(TransferStatus.Pending, transfer.Status);

        // Act
        transfer.MarkFailed();

        // Assert
        Assert.Equal(TransferStatus.Failed, transfer.Status);
    }

    [Fact]
    public void Amount_Property_ReturnsCorrectMoney()
    {
        // Arrange
        var expectedAmount = Money.FromDecimal(75.25m, Currency.CAD);
        var transfer = new Transfer(
            "test", "key", Guid.NewGuid(), Guid.NewGuid(), expectedAmount);

        // Act & Assert
        Assert.Equal(expectedAmount, transfer.Amount);
    }

    [Fact]
    public void ConvertedAmount_Property_ReturnsCorrectMoney()
    {
        // Arrange
        var sourceAmount = Money.FromDecimal(100.00m, Currency.USD);
        var convertedAmount = Money.FromDecimal(135.50m, Currency.CAD);
        var transfer = new Transfer(
            "test", "key", Guid.NewGuid(), Guid.NewGuid(), sourceAmount, convertedAmount);

        // Act & Assert
        Assert.Equal(convertedAmount, transfer.ConvertedAmount);
    }

    [Fact]
    public void Constructor_NullClientId_ThrowsException()
    {
        // Arrange
        var amount = Money.FromDecimal(10.00m, Currency.USD);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new Transfer(null!, "key", Guid.NewGuid(), Guid.NewGuid(), amount));
    }

    [Fact]
    public void Constructor_NullIdempotencyKey_ThrowsException()
    {
        // Arrange
        var amount = Money.FromDecimal(10.00m, Currency.USD);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new Transfer("client", null!, Guid.NewGuid(), Guid.NewGuid(), amount));
    }

    [Fact]
    public void Constructor_EmptyClientId_AllowedButNotRecommended()
    {
        // Arrange
        var amount = Money.FromDecimal(10.00m, Currency.USD);

        // Act
        var transfer = new Transfer("", "key", Guid.NewGuid(), Guid.NewGuid(), amount);

        // Assert
        Assert.Equal("", transfer.ClientId);
    }

    [Fact]
    public void Constructor_EmptyIdempotencyKey_AllowedButNotRecommended()
    {
        // Arrange
        var amount = Money.FromDecimal(10.00m, Currency.USD);

        // Act
        var transfer = new Transfer("client", "", Guid.NewGuid(), Guid.NewGuid(), amount);

        // Assert
        Assert.Equal("", transfer.IdempotencyKey);
    }

    private static Transfer CreateSampleTransfer()
    {
        return new Transfer(
            "test-client",
            "test-key",
            Guid.NewGuid(),
            Guid.NewGuid(),
            Money.FromDecimal(10.00m, Currency.USD));
    }
}