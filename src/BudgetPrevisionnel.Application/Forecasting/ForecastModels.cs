namespace BudgetPrevisionnel.Application.Forecasting;

public enum ForecastSource
{
    Budget,
    RecurringExpense
}

public sealed record ForecastCategoryLine(int? CategoryId, string? CategoryLabel, decimal Amount, ForecastSource Source);

public sealed record MonthlyForecast(
    DateOnly Month, IReadOnlyList<ForecastCategoryLine> CategoryLines, decimal LoanPayments, decimal Total);
