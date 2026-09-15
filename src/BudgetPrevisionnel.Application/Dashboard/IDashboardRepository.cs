namespace BudgetPrevisionnel.Application.Dashboard;

public interface IDashboardRepository
{
    /// <summary>Net Amount summed per day. Internal transfers are a real balance change
    /// for a single account but would distort a cross-account consolidated view - the
    /// caller (DashboardService) decides via includeInternalTransfers.</summary>
    Task<IReadOnlyList<DailyNetChange>> GetDailyNetChangesAsync(
        int userId, int? bankAccountId, bool includeInternalTransfers, DateOnly fromDate, DateOnly toDate,
        CancellationToken cancellationToken = default);

    /// <summary>Expense-only (negative Amount) totals per category, as positive
    /// magnitudes, always excluding internal transfers. A category-less transaction
    /// groups under a null CategoryId/CategoryName entry rather than being dropped.</summary>
    Task<IReadOnlyList<CategoryBreakdownEntry>> GetCategoryBreakdownAsync(
        int userId, int? bankAccountId, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default);

    /// <summary>Income/expense/net totals per month that has at least one transaction
    /// (DashboardService fills in the months with none) for the given year, always
    /// excluding internal transfers.</summary>
    Task<IReadOnlyList<MonthlyComparisonEntry>> GetMonthlyTotalsAsync(
        int userId, int? bankAccountId, int year, CancellationToken cancellationToken = default);
}
