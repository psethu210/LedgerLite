using LedgerLite.Domain;
using LedgerLite.Domain.Exceptions;
using LedgerLite.Infrastructure.Data;
using LedgerLite.Infrastructure.Services;

using Microsoft.EntityFrameworkCore;

namespace LedgerLite.Api.Services;

public class TransferService
{
    private readonly LedgerContext _context;
    private readonly IdempotencyService _idempotencyService;
    private readonly ILogger<TransferService> _logger;

    public TransferService(
        LedgerContext context,
        IdempotencyService idempotencyService,
        ILogger<TransferService> logger)
    {
        _context = context;
        _idempotencyService = idempotencyService;
        _logger = logger;
    }

    /// <summary>
    /// Executes a same-currency transfer with proper concurrency control and idempotency.
    /// </summary>
    public async Task<Transfer> ExecuteTransferAsync(
        string clientId,
        string idempotencyKey,
        Guid fromAccountId,
        Guid toAccountId,
        decimal amount)
    {
        // Check for existing idempotent request first (outside transaction for performance)
        var existingTransfer = await _context.Transfers
            .FirstOrDefaultAsync(t => t.ClientId == clientId && t.IdempotencyKey == idempotencyKey);

        if (existingTransfer != null)
        {
            _logger.LogDebug("Returning existing transfer for idempotency key {IdempotencyKey}", idempotencyKey);
            return existingTransfer;
        }

        // Start transaction with retry logic for deadlock handling
        return await ExecuteWithRetryAsync(async () =>
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Double-check for idempotency inside the transaction
                var existingInTransaction = await _context.Transfers
                    .FirstOrDefaultAsync(t => t.ClientId == clientId && t.IdempotencyKey == idempotencyKey);

                if (existingInTransaction != null)
                {
                    await transaction.CommitAsync();
                    return existingInTransaction;
                }

                // Lock accounts in ascending ID order to prevent deadlocks
                var accountIds = new[] { fromAccountId, toAccountId }.OrderBy(x => x).ToArray();
                var accounts = new Account[2];

                for (int i = 0; i < accountIds.Length; i++)
                {
                    // Lock account with UPDLOCK, ROWLOCK to prevent race conditions
                    var account = await _context.Accounts
                        .FromSqlRaw("SELECT * FROM Accounts WITH (UPDLOCK, ROWLOCK) WHERE Id = {0}", accountIds[i])
                        .FirstOrDefaultAsync();

                    if (account == null)
                    {
                        throw new ArgumentException($"Account {accountIds[i]} not found");
                    }

                    accounts[i] = account;
                }

                // Find the from and to accounts in the locked results
                var fromAccount = accounts.First(a => a.Id == fromAccountId);
                var toAccount = accounts.First(a => a.Id == toAccountId);

                // Validate transfer
                if (fromAccount.Currency != toAccount.Currency)
                {
                    throw new InvalidOperationException("Cross-currency transfers not supported in Phase 2");
                }

                var transferAmount = Money.FromDecimal(amount, fromAccount.Currency);

                // Check funds before creating any records
                if (!fromAccount.AllowNegative && fromAccount.BalanceMinor < transferAmount.MinorUnits)
                {
                    throw new InsufficientFundsException(fromAccountId, fromAccount.Balance, transferAmount);
                }

                // Create transfer record
                var transfer = new Transfer(
                    clientId,
                    idempotencyKey,
                    fromAccountId,
                    toAccountId,
                    transferAmount);

                _context.Transfers.Add(transfer);

                // Create ledger entries (double-entry bookkeeping)
                var debitEntry = LedgerEntry.Debit(transfer.Id, fromAccountId, transferAmount);
                var creditEntry = LedgerEntry.Credit(transfer.Id, toAccountId, transferAmount);

                _context.LedgerEntries.AddRange(debitEntry, creditEntry);

                // Update account balances
                fromAccount.Debit(transferAmount);
                toAccount.Credit(transferAmount);

                // Mark transfer as completed
                transfer.MarkCompleted();

                // Save all changes
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Transfer completed: {TransferId} - {Amount} {Currency} from {FromAccountId} to {ToAccountId}",
                    transfer.Id, transferAmount.ToDecimal(), transferAmount.Currency, fromAccountId, toAccountId);

                return transfer;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });
    }

    /// <summary>
    /// Executes an operation with retry logic for handling SQL deadlocks and transient failures.
    /// </summary>
    private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation, int maxRetries = 3)
    {
        var attempt = 0;

        while (true)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex) when (attempt < maxRetries && IsRetriableException(ex))
            {
                attempt++;
                var delay = TimeSpan.FromMilliseconds(50 * Math.Pow(2, attempt)); // Exponential backoff

                _logger.LogWarning("Transfer attempt {Attempt} failed with retriable error: {Error}. Retrying in {Delay}ms",
                    attempt, ex.Message, delay.TotalMilliseconds);

                await Task.Delay(delay);
            }
        }
    }

    /// <summary>
    /// Determines if an exception is retriable (deadlocks, timeouts, etc.).
    /// </summary>
    private static bool IsRetriableException(Exception ex)
    {
        // SQL Server deadlock error number is 1205
        // Timeout errors and other transient failures should also be retried
        return ex.Message.Contains("deadlock") ||
               ex.Message.Contains("timeout") ||
               ex.Message.Contains("1205");
    }
}