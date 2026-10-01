using LedgerLite.Domain;
using LedgerLite.Domain.Exceptions;
using LedgerLite.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LedgerLite.UnitTests.Infrastructure;

public class LedgerContextTests
{
    private LedgerContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<LedgerContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        
        return new LedgerContext(options);
    }
    
    [Fact]
    public void SaveChanges_ModifyingLedgerEntry_ThrowsException()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        
        var account = Account.CreateCustomerAccount("test-user", Currency.USD);
        context.Accounts.Add(account);
        
        var entry = new LedgerEntry(Guid.NewGuid(), account.Id, Currency.USD, 1000);
        context.LedgerEntries.Add(entry);
        
        context.SaveChanges();
        
        // Act: Try to modify the entry
        entry.GetType().GetProperty("AmountMinor")?.SetValue(entry, 2000);
        context.Entry(entry).State = EntityState.Modified;
        
        // Assert
        Assert.Throws<LedgerEntryImmutableException>(() => context.SaveChanges());
    }
    
    [Fact]
    public void SaveChanges_DeletingLedgerEntry_ThrowsException()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        
        var account = Account.CreateCustomerAccount("test-user", Currency.USD);
        context.Accounts.Add(account);
        
        var entry = new LedgerEntry(Guid.NewGuid(), account.Id, Currency.USD, 1000);
        context.LedgerEntries.Add(entry);
        
        context.SaveChanges();
        
        // Act: Try to delete the entry
        context.LedgerEntries.Remove(entry);
        
        // Assert
        Assert.Throws<LedgerEntryImmutableException>(() => context.SaveChanges());
    }
    
    [Fact] 
    public void SaveChanges_AddingNewLedgerEntry_Succeeds()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        
        var account = Account.CreateCustomerAccount("test-user", Currency.USD);
        context.Accounts.Add(account);
        context.SaveChanges();
        
        // Act: Add new entry (this should work)
        var entry = new LedgerEntry(Guid.NewGuid(), account.Id, Currency.USD, 1000);
        context.LedgerEntries.Add(entry);
        
        // Assert: Should not throw
        var result = context.SaveChanges();
        Assert.Equal(1, result); // One row affected
    }
}