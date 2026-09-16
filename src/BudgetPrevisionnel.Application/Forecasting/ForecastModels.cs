namespace BudgetPrevisionnel.Application.Forecasting;

public enum ForecastSource
{
    Budget,
    RecurringExpense
}

public sealed record ForecastCategoryLine(int? CategoryId, string? CategoryLabel, decimal Amount, ForecastSource Source);

/// <summary>
/// TotalRecurringIncome/NetBalance are additive to the original Budget+RecurringExpense+
/// Loan total (never folded into Total or CategoryLines) - Total keeps meaning "planned
/// outflow for the month" for every existing caller; NetBalance = TotalRecurringIncome - Total.
/// </summary>
public sealed record MonthlyForecast(
    DateOnly Month, IReadOnlyList<ForecastCategoryLine> CategoryLines, decimal LoanPayments, decimal Total,
    decimal TotalRecurringIncome, decimal NetBalance);
