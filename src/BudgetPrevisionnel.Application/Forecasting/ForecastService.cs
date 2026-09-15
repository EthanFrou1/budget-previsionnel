using BudgetPrevisionnel.Application.Budgets;
using BudgetPrevisionnel.Application.Loans;
using BudgetPrevisionnel.Application.RecurringExpenses;

namespace BudgetPrevisionnel.Application.Forecasting;

/// <summary>
/// Combines Budget + RecurringExpense + Loan into one projected total for a month, per
/// the brief. Precedence rule: a category with an explicit Budget entry for the month
/// uses that (the user's deliberate plan overrides a guess), and RecurringExpense only
/// fills in categories the user hasn't explicitly budgeted - so a rent RecurringExpense
/// doesn't get double-counted once the user also sets a Logement Budget line. Loan
/// payments have no CategoryId in the Domain model, so they're a separate total, not
/// folded into a category line.
/// </summary>
public sealed class ForecastService(
    IBudgetRepository budgetRepository,
    IRecurringExpenseRepository recurringExpenseRepository,
    ILoanRepository loanRepository)
{
    public async Task<MonthlyForecast> GetMonthlyForecastAsync(
        int userId, DateOnly month, CancellationToken cancellationToken = default)
    {
        var normalizedMonth = new DateOnly(month.Year, month.Month, 1);

        var budgets = await budgetRepository.GetForMonthAsync(userId, normalizedMonth, cancellationToken);
        var recurringExpenses = await recurringExpenseRepository.GetAllForUserAsync(userId, cancellationToken);
        var loans = await loanRepository.GetAllForUserAsync(userId, cancellationToken);

        var budgetedCategoryIds = budgets.Select(b => b.CategoryId).ToHashSet();

        var lines = budgets
            .Select(b => new ForecastCategoryLine(b.CategoryId, b.Category.Name, b.PlannedAmount, ForecastSource.Budget))
            .ToList();

        var recurringContributions = recurringExpenses
            .Where(e => e.CategoryId is null || !budgetedCategoryIds.Contains(e.CategoryId.Value))
            .Select(e => (Expense: e, Amount: RecurringExpenseProjector.GetMonthlyContribution(e, normalizedMonth)))
            .Where(x => x.Amount > 0m)
            .GroupBy(x => x.Expense.CategoryId);

        foreach (var group in recurringContributions)
        {
            var amount = group.Sum(x => x.Amount);
            var categoryLabel = group.Key is null ? null : group.First().Expense.Category?.Name;
            lines.Add(new ForecastCategoryLine(group.Key, categoryLabel, amount, ForecastSource.RecurringExpense));
        }

        var loanPayments = loans.Where(l => l.EndDate >= normalizedMonth).Sum(l => l.MonthlyPayment);
        var total = lines.Sum(l => l.Amount) + loanPayments;

        return new MonthlyForecast(
            normalizedMonth,
            lines.OrderBy(l => l.CategoryLabel, StringComparer.OrdinalIgnoreCase).ToList(),
            loanPayments,
            total);
    }

    public async Task<IReadOnlyList<MonthlyForecast>> GetAnnualForecastAsync(
        int userId, int year, CancellationToken cancellationToken = default)
    {
        var forecasts = new List<MonthlyForecast>(12);

        for (var monthNumber = 1; monthNumber <= 12; monthNumber++)
        {
            forecasts.Add(await GetMonthlyForecastAsync(userId, new DateOnly(year, monthNumber, 1), cancellationToken));
        }

        return forecasts;
    }
}
