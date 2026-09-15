using BudgetPrevisionnel.Domain.Entities;
using BudgetPrevisionnel.Domain.Enums;

namespace BudgetPrevisionnel.Application.Forecasting;

/// <summary>
/// How much a single RecurringExpense contributes to a given calendar month. Pure and
/// independently testable because the weekly case is easy to get subtly wrong: a flat
/// "4.33 weeks/month" approximation is off by a real payment in months with 5
/// occurrences of the expense's weekday, which matters when this feeds a number someone
/// plans a budget around. Instead this counts the exact occurrences of StartDate's
/// weekday within the month, clipped to [StartDate, EndDate].
/// </summary>
public static class RecurringExpenseProjector
{
    public static decimal GetMonthlyContribution(RecurringExpense expense, DateOnly month)
    {
        var monthEnd = month.AddMonths(1).AddDays(-1);

        if (expense.StartDate > monthEnd)
        {
            return 0m; // Hasn't started yet as of this month.
        }

        if (expense.EndDate is not null && expense.EndDate < month)
        {
            return 0m; // Already ended before this month began.
        }

        return expense.Frequency switch
        {
            RecurrenceFrequency.Monthly => expense.Amount,
            RecurrenceFrequency.Yearly => expense.StartDate.Month == month.Month ? expense.Amount : 0m,
            RecurrenceFrequency.Weekly => expense.Amount * CountWeeklyOccurrencesInMonth(expense.StartDate, expense.EndDate, month),
            _ => 0m
        };
    }

    private static int CountWeeklyOccurrencesInMonth(DateOnly startDate, DateOnly? endDate, DateOnly month)
    {
        var monthStart = month;
        var monthEnd = month.AddMonths(1).AddDays(-1);

        var rangeStart = startDate > monthStart ? startDate : monthStart;
        var rangeEnd = endDate is not null && endDate.Value < monthEnd ? endDate.Value : monthEnd;

        if (rangeStart > rangeEnd)
        {
            return 0;
        }

        // First date >= rangeStart that falls on the same weekday as startDate.
        var daysUntilFirstOccurrence = ((int)startDate.DayOfWeek - (int)rangeStart.DayOfWeek + 7) % 7;
        var firstOccurrence = rangeStart.AddDays(daysUntilFirstOccurrence);

        if (firstOccurrence > rangeEnd)
        {
            return 0;
        }

        return ((rangeEnd.DayNumber - firstOccurrence.DayNumber) / 7) + 1;
    }
}
