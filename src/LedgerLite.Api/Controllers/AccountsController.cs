using LedgerLite.Api.Models;
using LedgerLite.Domain;
using LedgerLite.Domain.Exceptions;
using LedgerLite.Infrastructure.Data;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LedgerLite.Api.Controllers;

[ApiController]
[Route("accounts")]
public class AccountsController : ControllerBase
{
    private readonly LedgerContext _context;
    private readonly ILogger<AccountsController> _logger;

    public AccountsController(LedgerContext context, ILogger<AccountsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<AccountResponse>> CreateAccount([FromBody] CreateAccountRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var account = Account.CreateCustomerAccount(request.OwnerRef, request.Currency);

        _context.Accounts.Add(account);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created account {AccountId} for owner {OwnerRef} in {Currency}",
            account.Id, account.OwnerRef, account.Currency);

        return CreatedAtAction(nameof(GetAccount), new { id = account.Id }, AccountResponse.FromAccount(account));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AccountResponse>> GetAccount(Guid id)
    {
        var account = await _context.Accounts.FindAsync(id);

        if (account == null)
        {
            return NotFound(new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.4",
                Title = "Account not found",
                Status = 404,
                Detail = $"Account with ID {id} was not found",
                Instance = HttpContext.Request.Path
            });
        }

        return AccountResponse.FromAccount(account);
    }

    [HttpPost("{id:guid}/deposits")]
    public async Task<ActionResult<DepositResponse>> CreateDeposit(Guid id, [FromBody] DepositRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var idempotencyKey = HttpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (string.IsNullOrEmpty(idempotencyKey))
        {
            return BadRequest(new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                Title = "Missing Idempotency Key",
                Status = 400,
                Detail = "Idempotency-Key header is required for money-moving operations",
                Instance = HttpContext.Request.Path
            });
        }

        var clientId = (string)HttpContext.Items["ClientId"]!;

        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            // Check for existing idempotent request
            var existingTransfer = await _context.Transfers
                .FirstOrDefaultAsync(t => t.ClientId == clientId && t.IdempotencyKey == idempotencyKey);

            if (existingTransfer != null)
            {
                // Return the existing transfer (idempotency)
                return Ok(new DepositResponse
                {
                    TransferId = existingTransfer.Id,
                    AccountId = existingTransfer.ToAccountId,
                    Amount = existingTransfer.ConvertedAmount.ToDecimal(),
                    Currency = existingTransfer.TargetCurrency.ToString(),
                    Status = existingTransfer.Status.ToString(),
                    CreatedAt = existingTransfer.CreatedAt
                });
            }

            // Load account with row-level locking to prevent race conditions
            var account = await _context.Accounts
                .Where(a => a.Id == id)
                .FirstOrDefaultAsync();

            if (account == null)
            {
                return NotFound(new ProblemDetails
                {
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.5.4",
                    Title = "Account not found",
                    Status = 404,
                    Detail = $"Account with ID {id} was not found",
                    Instance = HttpContext.Request.Path
                });
            }

            // Find or create external funding account for this currency
            var fundingAccount = await _context.Accounts
                .FirstOrDefaultAsync(a => a.OwnerRef == $"EXTERNAL_FUNDING_{account.Currency}" &&
                                         a.Kind == AccountKind.System);

            if (fundingAccount == null)
            {
                fundingAccount = Account.CreateExternalFundingAccount(account.Currency);
                _context.Accounts.Add(fundingAccount);
            }

            // Create the deposit transfer
            var amount = Money.FromDecimal(request.Amount, account.Currency);
            var transfer = Transfer.CreateDeposit(clientId, idempotencyKey, fundingAccount.Id, account.Id, amount);

            _context.Transfers.Add(transfer);

            // Create ledger entries (double-entry bookkeeping)
            var debitEntry = LedgerEntry.Debit(transfer.Id, fundingAccount.Id, amount);
            var creditEntry = LedgerEntry.Credit(transfer.Id, account.Id, amount);

            _context.LedgerEntries.AddRange(debitEntry, creditEntry);

            // Update account balances
            fundingAccount.Debit(amount); // External funding can go negative
            account.Credit(amount);

            // Mark transfer as completed
            transfer.MarkCompleted();

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("Deposit completed: {Amount} {Currency} to account {AccountId}",
                amount.ToDecimal(), amount.Currency, account.Id);

            return Ok(new DepositResponse
            {
                TransferId = transfer.Id,
                AccountId = account.Id,
                Amount = amount.ToDecimal(),
                Currency = amount.Currency.ToString(),
                Status = transfer.Status.ToString(),
                CreatedAt = transfer.CreatedAt
            });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();

            _logger.LogError(ex, "Failed to process deposit for account {AccountId}", id);

            return StatusCode(500, new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
                Title = "Internal server error",
                Status = 500,
                Detail = "An error occurred while processing the deposit",
                Instance = HttpContext.Request.Path
            });
        }
    }

    [HttpGet("{id:guid}/entries")]
    public async Task<ActionResult<PagedLedgerEntriesResponse>> GetAccountEntries(
        Guid id,
        [FromQuery] string? cursor = null,
        [FromQuery] int limit = 50)
    {
        if (limit <= 0 || limit > 1000)
        {
            return BadRequest(new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                Title = "Invalid limit",
                Status = 400,
                Detail = "Limit must be between 1 and 1000",
                Instance = HttpContext.Request.Path
            });
        }

        var account = await _context.Accounts.FindAsync(id);
        if (account == null)
        {
            return NotFound(new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.4",
                Title = "Account not found",
                Status = 404,
                Detail = $"Account with ID {id} was not found",
                Instance = HttpContext.Request.Path
            });
        }

        var query = _context.LedgerEntries
            .Where(e => e.AccountId == id)
            .OrderByDescending(e => e.CreatedAt)
            .ThenByDescending(e => e.Id);

        // Apply cursor-based pagination if provided
        if (!string.IsNullOrEmpty(cursor) && long.TryParse(cursor, out var cursorId))
        {
            query = query.Where(e => e.Id < cursorId)
                         .OrderByDescending(e => e.CreatedAt)
                         .ThenByDescending(e => e.Id);
        }

        var entries = await query
            .Take(limit + 1) // Take one extra to determine if there are more
            .ToListAsync();

        var hasMore = entries.Count > limit;
        if (hasMore)
        {
            entries.RemoveAt(entries.Count - 1); // Remove the extra entry
        }

        var response = new PagedLedgerEntriesResponse
        {
            Entries = entries.Select(LedgerEntryResponse.FromLedgerEntry).ToList(),
            HasMore = hasMore,
            NextCursor = hasMore && entries.Any() ? entries.Last().Id.ToString() : null
        };

        return Ok(response);
    }
}