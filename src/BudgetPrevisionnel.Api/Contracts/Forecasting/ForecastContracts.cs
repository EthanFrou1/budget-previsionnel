using BudgetPrevisionnel.Application.Forecasting;

namespace BudgetPrevisionnel.Api.Contracts.Forecasting;

public sealed record ForecastCategoryLineResponse(int? CategoryId, string? CategoryLabel, decimal Amount, ForecastSource Source)
{
    public static ForecastCategoryLineResponse FromLine(ForecastCategoryLine line) =>
        new(line.CategoryId, line.CategoryLabel, line.Amount, line.Source);
}

public sealed record MonthlyForecastResponse(
    DateOnly Month, IReadOnlyList<ForecastCategoryLineResponse> CategoryLines, decimal LoanPayments, decimal Total)
{
    public static MonthlyForecastResponse FromForecast(MonthlyForecast forecast) => new(
        forecast.Month, forecast.CategoryLines.Select(ForecastCategoryLineResponse.FromLine).ToList(),
        forecast.LoanPayments, forecast.Total);
}
