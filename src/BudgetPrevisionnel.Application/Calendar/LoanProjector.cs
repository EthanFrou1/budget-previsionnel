using BudgetPrevisionnel.Application.Forecasting;
using BudgetPrevisionnel.Domain.Entities;

namespace BudgetPrevisionnel.Application.Calendar;

/// <summary>
/// Where a Loan's monthly payment lands on the calendar. Loan has no StartDate in the
/// domain model (unlike RecurringExpense) - EndDate.Day is the only day-of-month anchor
/// available, which matches how most loan schedules actually work (a fixed billing day
/// every month, with EndDate literally being the date of the final payment). No lower
/// bound either, same as ForecastService's existing `EndDate >= month` check - a Loan is
/// treated as always active up to EndDate, for lack of a StartDate to bound it from.
/// </summary>
public static class LoanProjector
{
    public static RecurringExpenseOccurrence? GetMonthlyOccurrence(Loan loan, DateOnly month)
    {
        var monthStart = new DateOnly(month.Year, month.Month, 1);
        if (loan.EndDate < monthStart)
        {
            return null; // Already fully repaid before this month began.
        }

        var day = Math.Min(loan.EndDate.Day, DateTime.DaysInMonth(monthStart.Year, monthStart.Month));
        var occurrenceDate = new DateOnly(monthStart.Year, monthStart.Month, day);
        return new RecurringExpenseOccurrence(occurrenceDate, IsLastOccurrence: occurrenceDate >= loan.EndDate);
    }
}
