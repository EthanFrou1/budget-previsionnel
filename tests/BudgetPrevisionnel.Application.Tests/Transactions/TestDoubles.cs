using BudgetPrevisionnel.Application.Tests.BankAccounts;
using BudgetPrevisionnel.Application.Transactions;
using BudgetPrevisionnel.Domain.Entities;

namespace BudgetPrevisionnel.Application.Tests.Transactions;

internal sealed class FakeTransactionRepository(FakeBankAccountRepository accounts) : ITransactionRepository
{
    private readonly List<Transaction> _transactions = [];
    private int _nextId = 1;

    public IReadOnlyList<Transaction> All => _transactions;

    public Task<IReadOnlyDictionary<TransactionFingerprint, int>> GetFingerprintCountsAsync(
        int bankAccountId, CancellationToken cancellationToken = default)
    {
        var counts = _transactions
            .Where(t => t.BankAccountId == bankAccountId)
            .GroupBy(t => new TransactionFingerprint(t.Date, t.RawLabel, t.Amount))
            .ToDictionary(g => g.Key, g => g.Count());

        return Task.FromResult<IReadOnlyDictionary<TransactionFingerprint, int>>(counts);
    }

    public Task AddRangeAsync(IEnumerable<Transaction> transactions, CancellationToken cancellationToken = default)
    {
        foreach (var t in transactions)
        {
            t.Id = _nextId++;
            _transactions.Add(t);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Transaction>> GetForOtherAccountsAsync(
        int userId, int excludeBankAccountId, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default)
    {
        var result = _transactions
            .Where(t =>
                accounts.GetUserId(t.BankAccountId) == userId &&
                t.BankAccountId != excludeBankAccountId &&
                !t.IsInternalTransfer &&
                t.Date >= fromDate && t.Date <= toDate)
            .ToList();

        return Task.FromResult<IReadOnlyList<Transaction>>(result);
    }

    public Task<TransactionPage> SearchAsync(TransactionQuery query, CancellationToken cancellationToken = default)
    {
        var filtered = _transactions.Where(t => accounts.GetUserId(t.BankAccountId) == query.UserId);

        if (query.BankAccountId is not null)
        {
            filtered = filtered.Where(t => t.BankAccountId == query.BankAccountId);
        }

        if (query.CategoryId is not null)
        {
            filtered = filtered.Where(t => t.CategoryId == query.CategoryId);
        }

        if (query.FromDate is not null)
        {
            filtered = filtered.Where(t => t.Date >= query.FromDate);
        }

        if (query.ToDate is not null)
        {
            filtered = filtered.Where(t => t.Date <= query.ToDate);
        }

        if (query.ExcludeInternalTransfers)
        {
            filtered = filtered.Where(t => !t.IsInternalTransfer);
        }

        if (query.Search is not null)
        {
            filtered = filtered.Where(t =>
                t.RawLabel.Contains(query.Search, StringComparison.OrdinalIgnoreCase) ||
                (t.CleanedLabel?.Contains(query.Search, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        var all = filtered.OrderByDescending(t => t.Date).ThenByDescending(t => t.Id).ToList();
        var page = all.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList();

        return Task.FromResult(new TransactionPage(page, all.Count, query.Page, query.PageSize));
    }

    public Task<Transaction?> GetByIdForUserAsync(int userId, int transactionId, CancellationToken cancellationToken = default)
    {
        var transaction = _transactions.SingleOrDefault(t => t.Id == transactionId && accounts.GetUserId(t.BankAccountId) == userId);
        return Task.FromResult(transaction);
    }

    public Task<decimal> GetNetAmountAsync(
        int userId, int categoryId, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default)
    {
        var net = _transactions
            .Where(t =>
                accounts.GetUserId(t.BankAccountId) == userId &&
                t.CategoryId == categoryId &&
                !t.IsInternalTransfer &&
                t.Date >= fromDate && t.Date <= toDate)
            .Sum(t => t.Amount);

        return Task.FromResult(net);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
