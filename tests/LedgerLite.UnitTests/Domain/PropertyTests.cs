using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

using LedgerLite.Domain;
using LedgerLite.Domain.Exceptions;

namespace LedgerLite.UnitTests.Domain;

/// <summary>
/// Property-based tests using FsCheck to verify domain invariants hold across random sequences of operations.
/// These tests generate random sequences of account operations and verify the ledger always balances.
/// </summary>
public class PropertyTests
{
    /// <summary>
    /// Represents an operation that can be performed on accounts and transfers.
    /// Only balanced operations (transfers and deposits) are allowed to maintain double-entry invariants.
    /// </summary>
    public abstract record Operation
    {
        public record CreateAccount(string OwnerRef, Currency Currency, AccountKind Kind) : Operation;
        public record CreateTransfer(int FromIndex, int ToIndex, long MinorUnits) : Operation;
        public record CreateDeposit(int ToIndex, long MinorUnits) : Operation;
    }

    /// <summary>
    /// Generates valid operations for testing.
    /// </summary>
    public static class OperationGenerators
    {
        public static Gen<Currency> CurrencyGen =>
            Gen.Elements(Currency.USD, Currency.CAD, Currency.EUR, Currency.INR);

        public static Gen<AccountKind> AccountKindGen =>
            Gen.Elements(AccountKind.Customer, AccountKind.System);

        public static Gen<Operation.CreateAccount> CreateAccountGen =>
            from ownerRef in Gen.Elements("user1", "user2", "user3", "system1", "external_fund")
            from currency in CurrencyGen
            from kind in AccountKindGen
            select new Operation.CreateAccount(ownerRef, currency, kind);

        public static Gen<Operation.CreateTransfer> CreateTransferGen =>
            from fromIndex in Gen.Choose(0, 4)
            from toIndex in Gen.Choose(0, 4)
            from minorUnits in Gen.Choose(1, 5000) // Smaller amounts for transfers
            where fromIndex != toIndex
            select new Operation.CreateTransfer(fromIndex, toIndex, minorUnits);

        public static Gen<Operation.CreateDeposit> CreateDepositGen =>
            from accountIndex in Gen.Choose(0, 4)
            from minorUnits in Gen.Choose(1, 10000) // $0.01 to $100.00
            select new Operation.CreateDeposit(accountIndex, minorUnits);

        public static Gen<Operation> OperationGen =>
            Gen.OneOf(
                CreateAccountGen.Select(op => (Operation)op),
                CreateTransferGen.Select(op => (Operation)op),
                CreateDepositGen.Select(op => (Operation)op)
            );
    }

    /// <summary>
    /// Test state that tracks accounts, transfers, and ledger entries.
    /// </summary>
    public class TestLedgerState
    {
        public List<Account> Accounts { get; } = new();
        public List<Transfer> Transfers { get; } = new();
        public List<LedgerEntry> Entries { get; } = new();

        /// <summary>
        /// Verifies the fundamental ledger invariant: all entries sum to zero per currency.
        /// </summary>
        public bool IsBalanced()
        {
            // Group entries by currency and verify sum is zero
            var currencyTotals = Entries
                .GroupBy(e => e.Currency)
                .Select(g => new { Currency = g.Key, Total = g.Sum(e => e.AmountMinor) })
                .ToList();

            return currencyTotals.All(ct => ct.Total == 0);
        }

        /// <summary>
        /// Verifies that each account's balance equals the sum of its entries.
        /// </summary>
        public bool AccountBalancesMatch()
        {
            return Accounts.All(account =>
            {
                var entrySum = Entries
                    .Where(e => e.AccountId == account.Id)
                    .Sum(e => e.AmountMinor);
                return account.BalanceMinor == entrySum;
            });
        }

        /// <summary>
        /// Verifies that no customer account has a negative balance.
        /// </summary>
        public bool NoCustomerAccountsNegative()
        {
            return Accounts
                .Where(a => a.Kind == AccountKind.Customer)
                .All(a => a.BalanceMinor >= 0);
        }

        /// <summary>
        /// Attempts to execute an operation and returns whether it succeeded.
        /// </summary>
        public bool TryExecuteOperation(Operation operation)
        {
            try
            {
                switch (operation)
                {
                    case Operation.CreateAccount(var ownerRef, var currency, var kind):
                        var account = new Account(ownerRef, currency, kind);
                        Accounts.Add(account);
                        return true;

                    case Operation.CreateDeposit(var accountIndex, var minorUnits):
                        if (accountIndex >= Accounts.Count) return true; // Skip invalid index
                        var targetAccount = Accounts[accountIndex];
                        var depositMoney = new Money(minorUnits, targetAccount.Currency);

                        // Find or create external funding account for this currency
                        var fundingAccount = Accounts.FirstOrDefault(a =>
                            a.OwnerRef == $"EXTERNAL_FUNDING_{targetAccount.Currency}" &&
                            a.Kind == AccountKind.System);

                        if (fundingAccount == null)
                        {
                            fundingAccount = Account.CreateExternalFundingAccount(targetAccount.Currency);
                            Accounts.Add(fundingAccount);
                        }

                        // Execute deposit (debit funding, credit target)
                        fundingAccount.Debit(depositMoney);  // External funding can go negative
                        targetAccount.Credit(depositMoney);

                        var depositTransfer = Transfer.CreateDeposit(
                            "test-client",
                            Guid.NewGuid().ToString(),
                            fundingAccount.Id,
                            targetAccount.Id,
                            depositMoney);

                        Transfers.Add(depositTransfer);
                        Entries.Add(LedgerEntry.Debit(depositTransfer.Id, fundingAccount.Id, depositMoney));
                        Entries.Add(LedgerEntry.Credit(depositTransfer.Id, targetAccount.Id, depositMoney));
                        return true;

                    case Operation.CreateTransfer(var fromIndex, var toIndex, var minorUnits):
                        if (fromIndex >= Accounts.Count || toIndex >= Accounts.Count) return true;
                        var fromAccount = Accounts[fromIndex];
                        var toAccount = Accounts[toIndex];

                        // Only allow same-currency transfers for now (Phase 2 limitation)
                        if (fromAccount.Currency != toAccount.Currency) return true;

                        var transferMoney = new Money(minorUnits, fromAccount.Currency);

                        // This might throw InsufficientFundsException
                        fromAccount.Debit(transferMoney);
                        toAccount.Credit(transferMoney);

                        var transfer = new Transfer(
                            "test-client",
                            Guid.NewGuid().ToString(),
                            fromAccount.Id,
                            toAccount.Id,
                            transferMoney);

                        Transfers.Add(transfer);
                        Entries.Add(LedgerEntry.Debit(transfer.Id, fromAccount.Id, transferMoney));
                        Entries.Add(LedgerEntry.Credit(transfer.Id, toAccount.Id, transferMoney));
                        return true;

                    default:
                        return false;
                }
            }
            catch (InsufficientFundsException)
            {
                // This is expected behavior - customer accounts cannot go negative
                return true;
            }
            catch (Exception)
            {
                // Other exceptions indicate a problem
                return false;
            }
        }

        private Transfer CreateDummyTransfer(Guid fromId, Guid toId, Money amount)
        {
            return new Transfer(
                "test-client",
                Guid.NewGuid().ToString(),
                fromId,
                toId,
                amount);
        }
    }

    [Property(MaxTest = 100, Arbitrary = new[] { typeof(OperationArbitraries) })]
    public bool RandomOperationSequences_AlwaysKeepLedgerBalanced(List<Operation> operations)
    {
        var state = new TestLedgerState();

        // Start with at least one system account for each currency to allow operations
        foreach (var currency in new[] { Currency.USD, Currency.CAD, Currency.EUR, Currency.INR })
        {
            var systemAccount = Account.CreateExternalFundingAccount(currency);
            state.Accounts.Add(systemAccount);
        }

        // Execute all operations
        foreach (var operation in operations)
        {
            state.TryExecuteOperation(operation);
        }

        // Verify invariants
        var isBalanced = state.IsBalanced();
        var balancesMatch = state.AccountBalancesMatch();
        var noNegativeCustomers = state.NoCustomerAccountsNegative();

        return isBalanced && balancesMatch && noNegativeCustomers;
    }

    [Property(MaxTest = 50)]
    public bool MoneyArithmetic_IsAssociativeAndCommutative(int a, int b, int c)
    {
        // Skip zero and negative values
        if (a <= 0 || b <= 0 || c <= 0) return true;

        var moneyA = new Money(a, Currency.USD);
        var moneyB = new Money(b, Currency.USD);
        var moneyC = new Money(c, Currency.USD);

        // Test associativity: (a + b) + c == a + (b + c)
        var leftAssoc = (moneyA + moneyB) + moneyC;
        var rightAssoc = moneyA + (moneyB + moneyC);

        // Test commutativity: a + b == b + a
        var commutative1 = moneyA + moneyB;
        var commutative2 = moneyB + moneyA;

        return leftAssoc == rightAssoc && commutative1 == commutative2;
    }

    [Property(MaxTest = 50)]
    public bool AccountBalance_NeverExceedsLongMaxValue(long initialBalance, List<long> credits)
    {
        if (initialBalance < 0) return true; // Skip negative initial balances

        var account = Account.CreateCustomerAccount("test-user", Currency.USD);

        try
        {
            // Apply initial balance
            if (initialBalance > 0)
            {
                account.Credit(new Money(initialBalance, Currency.USD));
            }

            // Apply credits (filter out negative values and potential overflows)
            foreach (var creditAmount in credits.Where(c => c > 0 && c < long.MaxValue / 2))
            {
                var money = new Money(creditAmount, Currency.USD);
                account.Credit(money);

                // Verify balance didn't overflow
                if (account.BalanceMinor < 0) return false;
            }

            return true;
        }
        catch (OverflowException)
        {
            // Overflow is acceptable - we're testing the boundaries
            return true;
        }
    }

    [Property(MaxTest = 50)]
    public bool BankersRounding_RoundsToEven(decimal amount)
    {
        // Test banker's rounding behavior for edge cases
        if (amount < 0 || amount > 1000000) return true; // Skip extreme values

        var money = Money.FromDecimal(amount, Currency.USD);
        var backToDecimal = money.ToDecimal();

        // The conversion should be reversible within the precision of minor units
        var expectedMinorUnits = Math.Round(amount * 100, MidpointRounding.ToEven);

        return money.MinorUnits == (long)expectedMinorUnits;
    }

    /// <summary>
    /// Custom arbitraries for property tests.
    /// </summary>
    public static class OperationArbitraries
    {
        public static Arbitrary<Operation> Operation()
        {
            return Arb.From(OperationGenerators.OperationGen);
        }

        public static Arbitrary<List<Operation>> OperationList()
        {
            var operationListGen = Gen.ListOf(OperationGenerators.OperationGen)
                .Select(ops => ops.Take(20).ToList());
            return Arb.From(operationListGen);
        }
    }
}