using System.ComponentModel.DataAnnotations;
using LedgerLite.Domain;

namespace LedgerLite.Api.Models;

public class CreateAccountRequest
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string OwnerRef { get; set; } = string.Empty;
    
    [Required]
    public Currency Currency { get; set; }
}

public class AccountResponse
{
    public Guid Id { get; set; }
    public string OwnerRef { get; set; } = string.Empty;
    public Currency Currency { get; set; }
    public string Kind { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public DateTime CreatedAt { get; set; }
    
    public static AccountResponse FromAccount(Account account)
    {
        return new AccountResponse
        {
            Id = account.Id,
            OwnerRef = account.OwnerRef,
            Currency = account.Currency,
            Kind = account.Kind.ToString(),
            Balance = account.Balance.ToDecimal(),
            CreatedAt = account.CreatedAt
        };
    }
}