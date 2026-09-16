using BudgetPrevisionnel.Domain.Entities;
using BudgetPrevisionnel.Domain.Enums;

namespace BudgetPrevisionnel.Application.Forecasting;

/// <summary>Mirrors RecurringExpenseOccurrence - one concrete occurrence of a RecurringIncome.</summary>
public sealed record RecurringIncomeOccurrence(DateOnly Date, bool IsLastOccurrence);

/// <summary>
/// Same occurrence math as RecurringExpenseProjector, duplicated rather than shared - this
/// codebase's own convention (see TransactionsPage's FREQUENCY_LABELS/presetRange notes) is
/// to extract only past two consumers, and RecurringExpense/RecurringIncome are exactly two.
/// </summary>
public static class RecurringIncomeProjector
{
    public static decimal GetMonthlyContribution(RecurringIncome income, DateOnly month) =>
        income.Amount * GetOccurrenceDates(income, month).Count;

    public static IReadOnlyList<RecurringIncomeOccurrence> GetOccurrenceDates(RecurringIncome income, DateOnly month)
    {
        var monthStart = new DateOnly(month.Year, month.Month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        if (income.StartDate > monthEnd)
        {
            return []; // Hasn't started yet as of this month.
        }

        if (income.EndDate is not null && income.EndDate < monthStart)
        {
            return []; // Already ended before this month began.
        }

        return income.Frequency switch
        {
            RecurrenceFrequency.Monthly => GetPeriodicOccurrence(income, monthStart, periodMonths: 1),
            RecurrenceFrequency.Yearly => income.StartDate.Month == monthStart.Month
                ? GetPeriodicOccurrence(income, monthStart, periodMonths: 12)
                : [],
            RecurrenceFrequency.Weekly => GetWeeklyOccurrenceDates(income, monthStart, monthEnd),
            _ => []
        };
    }

    private static IReadOnlyList<RecurringIncomeOccurrence> GetPeriodicOccurrence(
        RecurringIncome income, DateOnly monthStart, int periodMonths)
    {
        var day = Math.Min(income.StartDate.Day, DateTime.DaysInMonth(monthStart.Year, monthStart.Month));
        var occurrenceDate = new DateOnly(monthStart.Year, monthStart.Month, day);

        if (occurrenceDate < income.StartDate || (income.EndDate is not null && occurrenceDate > income.EndDate))
        {
            return [];
        }

        var isLast = income.EndDate is not null && occurrenceDate.AddMonths(periodMonths) > income.EndDate;
        return [new RecurringIncomeOccurrence(occurrenceDate, isLast)];
    }

    private static IReadOnlyList<RecurringIncomeOccurrence> GetWeeklyOccurrenceDates(
        RecurringIncome income, DateOnly monthStart, DateOnly monthEnd)
    {
        var rangeStart = income.StartDate > monthStart ? income.StartDate : monthStart;
        var rangeEnd = income.EndDate is not null && income.EndDate.Value < monthEnd ? income.EndDate.Value : monthEnd;

        if (rangeStart > rangeEnd)
        {
            return [];
        }

        var daysUntilFirstOccurrence = ((int)income.StartDate.DayOfWeek - (int)rangeStart.DayOfWeek + 7) % 7;
        var firstOccurrence = rangeStart.AddDays(daysUntilFirstOccurrence);

        var occurrences = new List<RecurringIncomeOccurrence>();
        for (var date = firstOccurrence; date <= rangeEnd; date = date.AddDays(7))
        {
            var isLast = income.EndDate is not null && date.AddDays(7) > income.EndDate;
            occurrences.Add(new RecurringIncomeOccurrence(date, isLast));
        }

        return occurrences;
    }
}
