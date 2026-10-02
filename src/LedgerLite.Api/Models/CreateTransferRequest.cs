using System.ComponentModel.DataAnnotations;

namespace LedgerLite.Api.Models;

public class CreateTransferRequest
{
    [Required]
    public Guid FromAccountId { get; set; }

    [Required]
    public Guid ToAccountId { get; set; }

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be positive")]
    public decimal Amount { get; set; }
}

public class TransferResponse
{
    public Guid Id { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public Guid FromAccountId { get; set; }
    public Guid ToAccountId { get; set; }
    public decimal Amount { get; set; }
    public string SourceCurrency { get; set; } = string.Empty;
    public string TargetCurrency { get; set; } = string.Empty;
    public decimal ConvertedAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public static TransferResponse FromTransfer(LedgerLite.Domain.Transfer transfer)
    {
        return new TransferResponse
        {
            Id = transfer.Id,
            ClientId = transfer.ClientId,
            IdempotencyKey = transfer.IdempotencyKey,
            FromAccountId = transfer.FromAccountId,
            ToAccountId = transfer.ToAccountId,
            Amount = transfer.Amount.ToDecimal(),
            SourceCurrency = transfer.SourceCurrency.ToString(),
            TargetCurrency = transfer.TargetCurrency.ToString(),
            ConvertedAmount = transfer.ConvertedAmount.ToDecimal(),
            Status = transfer.Status.ToString(),
            CreatedAt = transfer.CreatedAt
        };
    }
}