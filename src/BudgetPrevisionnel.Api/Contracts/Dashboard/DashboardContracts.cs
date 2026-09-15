using BudgetPrevisionnel.Application.Dashboard;

namespace BudgetPrevisionnel.Api.Contracts.Dashboard;

public sealed record BalancePointResponse(DateOnly Date, decimal NetChange, decimal CumulativeBalance)
{
    public static BalancePointResponse FromModel(BalancePoint point) =>
        new(point.Date, point.NetChange, point.CumulativeBalance);
}

public sealed record CategoryBreakdownEntryResponse(int? CategoryId, string? CategoryName, decimal Amount)
{
    public static CategoryBreakdownEntryResponse FromModel(CategoryBreakdownEntry entry) =>
        new(entry.CategoryId, entry.CategoryName, entry.Amount);
}

public sealed record MonthlyComparisonEntryResponse(DateOnly Month, decimal Income, decimal Expense, decimal Net)
{
    public static MonthlyComparisonEntryResponse FromModel(MonthlyComparisonEntry entry) =>
        new(entry.Month, entry.Income, entry.Expense, entry.Net);
}
