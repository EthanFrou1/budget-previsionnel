using BudgetPrevisionnel.Application.Transactions;
using BudgetPrevisionnel.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BudgetPrevisionnel.Infrastructure.Persistence.Repositories;

public sealed class TransactionRepository(BudgetDbContext dbContext) : ITransactionRepository
{
    public async Task<IReadOnlyDictionary<TransactionFingerprint, int>> GetFingerprintCountsAsync(
        int bankAccountId, CancellationToken cancellationToken = default)
    {
        var rows = await dbContext.Transactions
            .Where(t => t.BankAccountId == bankAccountId)
            .Select(t => new { t.Date, t.RawLabel, t.Amount })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => new TransactionFingerprint(r.Date, r.RawLabel, r.Amount))
            .ToDictionary(g => g.Key, g => g.Count());
    }

    public async Task AddRangeAsync(IEnumerable<Transaction> transactions, CancellationToken cancellationToken = default)
    {
        dbContext.Transactions.AddRange(transactions);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Transaction>> GetForOtherAccountsAsync(
        int userId, int excludeBankAccountId, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default) =>
        await dbContext.Transactions
            .Where(t => t.BankAccount.UserId == userId
                && t.BankAccountId != excludeBankAccountId
                && !t.IsInternalTransfer
                && t.Date >= fromDate && t.Date <= toDate)
            .ToListAsync(cancellationToken);

    public async Task<TransactionPage> SearchAsync(TransactionQuery query, CancellationToken cancellationToken = default)
    {
        var filtered = dbContext.Transactions
            .Where(t => t.BankAccount.UserId == query.UserId);

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
            var pattern = $"%{query.Search}%";
            filtered = filtered.Where(t =>
                EF.Functions.ILike(t.RawLabel, pattern) ||
                (t.CleanedLabel != null && EF.Functions.ILike(t.CleanedLabel, pattern)));
        }

        var totalCount = await filtered.CountAsync(cancellationToken);

        // Id is always the tie-breaker (same direction as the primary column) so paging
        // stays stable when many rows share the same sorted value (e.g. same Date).
        IOrderedQueryable<Transaction> ordered = query.SortBy switch
        {
            TransactionSortColumn.BankAccount => query.SortDescending
                ? filtered.OrderByDescending(t => t.BankAccount.Label)
                : filtered.OrderBy(t => t.BankAccount.Label),
            TransactionSortColumn.Label => query.SortDescending
                ? filtered.OrderByDescending(t => t.CleanedLabel ?? t.RawLabel)
                : filtered.OrderBy(t => t.CleanedLabel ?? t.RawLabel),
            TransactionSortColumn.Amount => query.SortDescending
                ? filtered.OrderByDescending(t => t.Amount)
                : filtered.OrderBy(t => t.Amount),
            TransactionSortColumn.Category => query.SortDescending
                ? filtered.OrderByDescending(t => t.Category!.Name)
                : filtered.OrderBy(t => t.Category!.Name),
            _ => query.SortDescending
                ? filtered.OrderByDescending(t => t.Date)
                : filtered.OrderBy(t => t.Date),
        };
        ordered = query.SortDescending ? ordered.ThenByDescending(t => t.Id) : ordered.ThenBy(t => t.Id);

        var items = await ordered
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Include(t => t.BankAccount)
            .Include(t => t.Category)
            .ToListAsync(cancellationToken);

        return new TransactionPage(items, totalCount, query.Page, query.PageSize);
    }

    public Task<Transaction?> GetByIdForUserAsync(int userId, int transactionId, CancellationToken cancellationToken = default) =>
        dbContext.Transactions
            .Include(t => t.BankAccount)
            .Include(t => t.Category)
            .FirstOrDefaultAsync(t => t.Id == transactionId && t.BankAccount.UserId == userId, cancellationToken);

    public async Task<decimal> GetNetAmountAsync(
        int userId, int categoryId, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Transactions
            .Where(t => t.BankAccount.UserId == userId
                && t.CategoryId == categoryId
                && !t.IsInternalTransfer
                && t.Date >= fromDate && t.Date <= toDate);

        return await query.SumAsync(t => (decimal?)t.Amount, cancellationToken) ?? 0m;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
