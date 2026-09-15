using BudgetPrevisionnel.Application.Dashboard;

namespace BudgetPrevisionnel.Application.Tests.Dashboard;

internal sealed class FakeDashboardRepository : IDashboardRepository
{
    public List<DailyNetChange> DailyNetChangesWithTransfers { get; } = [];
    public List<DailyNetChange> DailyNetChangesWithoutTransfers { get; } = [];
    public List<CategoryBreakdownEntry> CategoryBreakdown { get; } = [];
    public List<MonthlyComparisonEntry> MonthlyTotals { get; } = [];

    public bool? LastIncludeInternalTransfersRequested { get; private set; }

    public Task<IReadOnlyList<DailyNetChange>> GetDailyNetChangesAsync(
        int userId, int? bankAccountId, bool includeInternalTransfers, DateOnly fromDate, DateOnly toDate,
        CancellationToken cancellationToken = default)
    {
        LastIncludeInternalTransfersRequested = includeInternalTransfers;
        var source = includeInternalTransfers ? DailyNetChangesWithTransfers : DailyNetChangesWithoutTransfers;
        return Task.FromResult<IReadOnlyList<DailyNetChange>>(source);
    }

    public Task<IReadOnlyList<CategoryBreakdownEntry>> GetCategoryBreakdownAsync(
        int userId, int? bankAccountId, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<CategoryBreakdownEntry>>(CategoryBreakdown);

    public Task<IReadOnlyList<MonthlyComparisonEntry>> GetMonthlyTotalsAsync(
        int userId, int? bankAccountId, int year, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<MonthlyComparisonEntry>>(MonthlyTotals);
}
