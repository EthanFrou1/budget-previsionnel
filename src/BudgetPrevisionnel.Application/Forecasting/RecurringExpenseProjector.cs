using BudgetPrevisionnel.Domain.Entities;
using BudgetPrevisionnel.Domain.Enums;

namespace BudgetPrevisionnel.Application.Forecasting;

/// <summary>One concrete occurrence of a RecurringExpense. IsLastOccurrence is true when
/// the NEXT occurrence (one period later) would fall after EndDate - i.e. this is the
/// last payment before the subscription/expense stops, which the calendar view highlights.</summary>
public sealed record RecurringExpenseOccurrence(DateOnly Date, bool IsLastOccurrence);

/// <summary>
/// Where and how often a RecurringExpense actually lands. Pure and independently
/// testable because the weekly case is easy to get subtly wrong: a flat "4.33 weeks/
/// month" approximation is off by a real payment in months with 5 occurrences of the
/// expense's weekday, which matters when this feeds a number someone plans a budget
/// around. Instead this counts the exact occurrences of StartDate's weekday within the
/// month, clipped to [StartDate, EndDate].
///
/// GetMonthlyContribution (the forecast's total) is deliberately implemented in terms of
/// GetOccurrenceDates (the calendar's actual dates) rather than as two separate
/// calculations - the two features can never disagree on whether/how many times an
/// expense lands in a given month.
/// </summary>
public static class RecurringExpenseProjector
{
    public static decimal GetMonthlyContribution(RecurringExpense expense, DateOnly month) =>
        expense.Amount * GetOccurrenceDates(expense, month).Count;

    public static IReadOnlyList<RecurringExpenseOccurrence> GetOccurrenceDates(RecurringExpense expense, DateOnly month)
    {
        var monthStart = new DateOnly(month.Year, month.Month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        if (expense.StartDate > monthEnd)
        {
            return []; // Hasn't started yet as of this month.
        }

        if (expense.EndDate is not null && expense.EndDate < monthStart)
        {
            return []; // Already ended before this month began.
        }

        return expense.Frequency switch
        {
            RecurrenceFrequency.Monthly => GetPeriodicOccurrence(expense, monthStart, periodMonths: 1),
            RecurrenceFrequency.Yearly => expense.StartDate.Month == monthStart.Month
                ? GetPeriodicOccurrence(expense, monthStart, periodMonths: 12)
                : [],
            RecurrenceFrequency.Weekly => GetWeeklyOccurrenceDates(expense, monthStart, monthEnd),
            _ => []
        };
    }

    /// <summary>Monthly/yearly land on the same day-of-month as StartDate (clamped to
    /// however many days the target month actually has, e.g. day 31 becomes day 30 in a
    /// 30-day month) - at most one occurrence, since the caller already filtered to the
    /// right month for Yearly.</summary>
    private static IReadOnlyList<RecurringExpenseOccurrence> GetPeriodicOccurrence(
        RecurringExpense expense, DateOnly monthStart, int periodMonths)
    {
        var day = Math.Min(expense.StartDate.Day, DateTime.DaysInMonth(monthStart.Year, monthStart.Month));
        var occurrenceDate = new DateOnly(monthStart.Year, monthStart.Month, day);

        if (occurrenceDate < expense.StartDate || (expense.EndDate is not null && occurrenceDate > expense.EndDate))
        {
            return [];
        }

        var isLast = expense.EndDate is not null && occurrenceDate.AddMonths(periodMonths) > expense.EndDate;
        return [new RecurringExpenseOccurrence(occurrenceDate, isLast)];
    }

    private static IReadOnlyList<RecurringExpenseOccurrence> GetWeeklyOccurrenceDates(
        RecurringExpense expense, DateOnly monthStart, DateOnly monthEnd)
    {
        var rangeStart = expense.StartDate > monthStart ? expense.StartDate : monthStart;
        var rangeEnd = expense.EndDate is not null && expense.EndDate.Value < monthEnd ? expense.EndDate.Value : monthEnd;

        if (rangeStart > rangeEnd)
        {
            return [];
        }

        // First date >= rangeStart that falls on the same weekday as startDate.
        var daysUntilFirstOccurrence = ((int)expense.StartDate.DayOfWeek - (int)rangeStart.DayOfWeek + 7) % 7;
        var firstOccurrence = rangeStart.AddDays(daysUntilFirstOccurrence);

        var occurrences = new List<RecurringExpenseOccurrence>();
        for (var date = firstOccurrence; date <= rangeEnd; date = date.AddDays(7))
        {
            var isLast = expense.EndDate is not null && date.AddDays(7) > expense.EndDate;
            occurrences.Add(new RecurringExpenseOccurrence(date, isLast));
        }

        return occurrences;
    }
}
