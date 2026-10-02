using LedgerLite.Domain;

namespace LedgerLite.Api.Models;

public class LedgerEntryResponse
{
    public long Id { get; set; }
    public Guid TransferId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // "Credit" or "Debit"
    public DateTime CreatedAt { get; set; }

    public static LedgerEntryResponse FromLedgerEntry(LedgerEntry entry)
    {
        return new LedgerEntryResponse
        {
            Id = entry.Id,
            TransferId = entry.TransferId,
            Amount = Math.Abs(entry.Amount.ToDecimal()),
            Currency = entry.Currency.ToString(),
            Type = entry.IsCredit ? "Credit" : "Debit",
            CreatedAt = entry.CreatedAt
        };
    }
}

public class PagedLedgerEntriesResponse
{
    public List<LedgerEntryResponse> Entries { get; set; } = new();
    public string? NextCursor { get; set; }
    public bool HasMore { get; set; }
}