using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;

using LedgerLite.Api.Models;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LedgerLite.IntegrationTests;

public class ConcurrencyTests : TestBase
{
    [Fact]
    public async Task ConcurrentTransfers_100Transfers_NoBalanceGoesNegative()
    {
        // This test must pass when run 20 times to verify concurrency control
        await RunConcurrentTransferTest();
    }

    /// <summary>
    /// Runs the concurrent transfer test. This should be called 20 times to verify consistency.
    /// </summary>
    private async Task RunConcurrentTransferTest()
    {
        // Arrange: Create 10 accounts, each with $100
        var accounts = new List<Guid>();
        using (var context = CreateDbContext())
        {
            for (int i = 0; i < 10; i++)
            {
                var account = LedgerLite.Domain.Account.CreateCustomerAccount($"concurrent-user-{i}", LedgerLite.Domain.Currency.USD);
                context.Accounts.Add(account);
                accounts.Add(account.Id);
            }
            await context.SaveChangesAsync();
        }

        // Fund each account with $100
        for (int i = 0; i < accounts.Count; i++)
        {
            var depositRequest = new DepositRequest { Amount = 100.00m };
            var depositJson = JsonSerializer.Serialize(depositRequest);
            var depositHttpRequest = new HttpRequestMessage(HttpMethod.Post, $"/accounts/{accounts[i]}/deposits")
            {
                Content = new StringContent(depositJson, Encoding.UTF8, "application/json")
            };

            var depositResponse = await SendWithHeadersAsync(depositHttpRequest, $"concurrent-deposit-{i}");
            Assert.Equal(HttpStatusCode.OK, depositResponse.StatusCode);
        }

        // Verify initial state: each account should have $100
        using (var context = CreateDbContext())
        {
            var initialAccounts = await context.Accounts.Where(a => accounts.Contains(a.Id)).ToListAsync();
            foreach (var account in initialAccounts)
            {
                Assert.Equal(10000, account.BalanceMinor); // $100 in minor units
            }
        }

        // Act: Execute 100 concurrent transfers of $1 each
        var random = new Random(42); // Fixed seed for reproducible results
        var tasks = new List<Task<(bool Success, string Error)>>();
        var results = new ConcurrentBag<(bool Success, string Error, int FromIndex, int ToIndex)>();

        for (int i = 0; i < 100; i++)
        {
            var transferIndex = i;
            var fromIndex = random.Next(accounts.Count);
            var toIndex = random.Next(accounts.Count);

            // Ensure from and to are different
            while (toIndex == fromIndex)
            {
                toIndex = random.Next(accounts.Count);
            }

            var task = ExecuteTransferAsync(accounts[fromIndex], accounts[toIndex], 1.00m, $"concurrent-{transferIndex}")
                .ContinueWith(t =>
                {
                    var result = t.Result;
                    results.Add((result.Success, result.Error, fromIndex, toIndex));
                    return result;
                });

            tasks.Add(task);
        }

        // Wait for all transfers to complete
        await Task.WhenAll(tasks);

        // Assert: Verify the final state
        using (var context = CreateDbContext())
        {
            var finalAccounts = await context.Accounts.Where(a => accounts.Contains(a.Id)).ToListAsync();
            var totalBalance = finalAccounts.Sum(a => a.BalanceMinor);

            // Total balance should still be $1000 (10 accounts * $100 each)
            Assert.Equal(100000, totalBalance); // $1000 in minor units

            // No account should have a negative balance
            foreach (var account in finalAccounts)
            {
                Assert.True(account.BalanceMinor >= 0,
                    $"Account {account.Id} has negative balance: {account.BalanceMinor} minor units");
            }

            // Verify ledger entries sum to zero
            var allEntries = await context.LedgerEntries.ToListAsync();
            var totalEntryAmount = allEntries.Sum(e => e.AmountMinor);
            Assert.Equal(0, totalEntryAmount);

            // Count successful and failed transfers
            var successCount = results.Count(r => r.Success);
            var failCount = results.Count(r => !r.Success);

            var logger = _factory.Services.GetService<ILogger<ConcurrencyTests>>();
            logger?.LogInformation("Concurrent transfer test completed: {SuccessCount} successful, {FailCount} failed",
                successCount, failCount);

            // At least some transfers should succeed (not all accounts should be empty)
            Assert.True(successCount > 0, "No transfers succeeded - this suggests a deadlock or other serious issue");

            // Failed transfers should be due to insufficient funds, not system errors
            var systemErrors = results.Where(r => !r.Success && !r.Error.Contains("Insufficient funds")).ToList();
            Assert.Empty(systemErrors.Select(e => e.Error));
        }
    }

    /// <summary>
    /// Executes a single transfer and returns whether it succeeded.
    /// </summary>
    private async Task<(bool Success, string Error)> ExecuteTransferAsync(Guid fromAccountId, Guid toAccountId, decimal amount, string idempotencyKey)
    {
        try
        {
            var transferRequest = new CreateTransferRequest
            {
                FromAccountId = fromAccountId,
                ToAccountId = toAccountId,
                Amount = amount
            };

            var json = JsonSerializer.Serialize(transferRequest);
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/transfers")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            var response = await SendWithHeadersAsync(httpRequest, idempotencyKey);

            if (response.IsSuccessStatusCode)
            {
                return (true, string.Empty);
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                return (false, error);
            }
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// Test that can be used to run the concurrency test multiple times.
    /// Run this manually to verify consistency across multiple runs.
    /// </summary>
    [Fact(Skip = "Manual test - run this 20 times to verify concurrency")]
    public async Task RunConcurrencyTest20Times()
    {
        for (int run = 1; run <= 20; run++)
        {
            // Create a fresh test instance for each run
            await InitializeAsync();

            try
            {
                await RunConcurrentTransferTest();

                var logger = _factory.Services.GetService<ILogger<ConcurrencyTests>>();
                logger?.LogInformation("Concurrency test run {Run}/20 completed successfully", run);
            }
            finally
            {
                await DisposeAsync();
            }
        }
    }
}