using System.ComponentModel.DataAnnotations;

namespace LedgerLite.Api.Models;

public class DepositRequest
{
    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be positive")]
    public decimal Amount { get; set; }
}

public class DepositResponse
{
    public Guid TransferId { get; set; }
    public Guid AccountId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}