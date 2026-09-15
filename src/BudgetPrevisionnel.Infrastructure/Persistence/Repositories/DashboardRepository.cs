using BudgetPrevisionnel.Application.Dashboard;
using Microsoft.EntityFrameworkCore;

namespace BudgetPrevisionnel.Infrastructure.Persistence.Repositories;

public sealed class DashboardRepository(BudgetDbContext dbContext) : IDashboardRepository
{
    public async Task<IReadOnlyList<DailyNetChange>> GetDailyNetChangesAsync(
        int userId, int? bankAccountId, bool includeInternalTransfers, DateOnly fromDate, DateOnly toDate,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Transactions
            .Where(t => t.BankAccount.UserId == userId && t.Date >= fromDate && t.Date <= toDate);

        if (!includeInternalTransfers)
        {
            query = query.Where(t => !t.IsInternalTransfer);
        }

        if (bankAccountId is not null)
        {
            query = query.Where(t => t.BankAccountId == bankAccountId);
        }

        var rows = await query.Select(t => new { t.Date, t.Amount }).ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => r.Date)
            .Select(g => new DailyNetChange(g.Key, g.Sum(r => r.Amount)))
            .OrderBy(d => d.Date)
            .ToList();
    }

    public async Task<IReadOnlyList<CategoryBreakdownEntry>> GetCategoryBreakdownAsync(
        int userId, int? bankAccountId, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Transactions
            .Where(t => t.BankAccount.UserId == userId
                && !t.IsInternalTransfer
                && t.Amount < 0
                && t.Date >= fromDate && t.Date <= toDate);

        if (bankAccountId is not null)
        {
            query = query.Where(t => t.BankAccountId == bankAccountId);
        }

        var rows = await query
            .Select(t => new { t.CategoryId, CategoryName = t.Category != null ? t.Category.Name : null, t.Amount })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => (r.CategoryId, r.CategoryName))
            .Select(g => new CategoryBreakdownEntry(g.Key.CategoryId, g.Key.CategoryName, -g.Sum(r => r.Amount)))
            .OrderByDescending(e => e.Amount)
            .ToList();
    }

    public async Task<IReadOnlyList<MonthlyComparisonEntry>> GetMonthlyTotalsAsync(
        int userId, int? bankAccountId, int year, CancellationToken cancellationToken = default)
    {
        var fromDate = new DateOnly(year, 1, 1);
        var toDate = new DateOnly(year, 12, 31);

        var query = dbContext.Transactions
            .Where(t => t.BankAccount.UserId == userId
                && !t.IsInternalTransfer
                && t.Date >= fromDate && t.Date <= toDate);

        if (bankAccountId is not null)
        {
            query = query.Where(t => t.BankAccountId == bankAccountId);
        }

        var rows = await query.Select(t => new { t.Date, t.Amount }).ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => new DateOnly(r.Date.Year, r.Date.Month, 1))
            .Select(g => new MonthlyComparisonEntry(
                g.Key,
                Income: g.Where(r => r.Amount > 0).Sum(r => r.Amount),
                Expense: -g.Where(r => r.Amount < 0).Sum(r => r.Amount),
                Net: g.Sum(r => r.Amount)))
            .ToList();
    }
}
