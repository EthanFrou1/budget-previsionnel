using BudgetPrevisionnel.Application.BankAccounts;

namespace BudgetPrevisionnel.Application.Dashboard;

public sealed class DashboardService(IDashboardRepository dashboardRepository, IBankAccountRepository bankAccountRepository)
{
    public async Task<IReadOnlyList<BalancePoint>> GetBalanceEvolutionAsync(
        int userId, int? bankAccountId, DateOnly fromDate, DateOnly toDate, decimal startingBalance,
        CancellationToken cancellationToken = default)
    {
        if (bankAccountId is not null)
        {
            await EnsureAccountOwnedByUserAsync(userId, bankAccountId.Value, cancellationToken);
        }

        // A transfer between two of the user's own accounts is a real balance change for
        // the single account it's viewed from, but would distort a consolidated,
        // cross-account view (same reasoning as TransactionQuery.ExcludeInternalTransfers).
        var includeInternalTransfers = bankAccountId is not null;

        var dailyChanges = await dashboardRepository.GetDailyNetChangesAsync(
            userId, bankAccountId, includeInternalTransfers, fromDate, toDate, cancellationToken);

        var points = new List<BalancePoint>(dailyChanges.Count);
        var running = startingBalance;

        foreach (var change in dailyChanges)
        {
            running += change.NetChange;
            points.Add(new BalancePoint(change.Date, change.NetChange, running));
        }

        return points;
    }

    public async Task<IReadOnlyList<CategoryBreakdownEntry>> GetCategoryBreakdownAsync(
        int userId, int? bankAccountId, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default)
    {
        if (bankAccountId is not null)
        {
            await EnsureAccountOwnedByUserAsync(userId, bankAccountId.Value, cancellationToken);
        }

        return await dashboardRepository.GetCategoryBreakdownAsync(userId, bankAccountId, fromDate, toDate, cancellationToken);
    }

    public async Task<IReadOnlyList<MonthlyComparisonEntry>> GetMonthlyComparisonAsync(
        int userId, int? bankAccountId, int year, CancellationToken cancellationToken = default)
    {
        if (bankAccountId is not null)
        {
            await EnsureAccountOwnedByUserAsync(userId, bankAccountId.Value, cancellationToken);
        }

        var totals = await dashboardRepository.GetMonthlyTotalsAsync(userId, bankAccountId, year, cancellationToken);
        var byMonth = totals.ToDictionary(t => t.Month);

        var result = new List<MonthlyComparisonEntry>(12);
        for (var month = 1; month <= 12; month++)
        {
            var key = new DateOnly(year, month, 1);
            result.Add(byMonth.TryGetValue(key, out var entry) ? entry : new MonthlyComparisonEntry(key, 0m, 0m, 0m));
        }

        return result;
    }

    private async Task EnsureAccountOwnedByUserAsync(int userId, int bankAccountId, CancellationToken cancellationToken)
    {
        var account = await bankAccountRepository.GetByIdForUserAsync(userId, bankAccountId, cancellationToken);
        if (account is null)
        {
            throw new BankAccountNotFoundException(bankAccountId);
        }
    }
}
