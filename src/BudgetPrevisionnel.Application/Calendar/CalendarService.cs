using BudgetPrevisionnel.Application.Forecasting;
using BudgetPrevisionnel.Application.Loans;
using BudgetPrevisionnel.Application.RecurringExpenses;

namespace BudgetPrevisionnel.Application.Calendar;

/// <summary>
/// Turns RecurringExpense + Loan into actual dated occurrences for a calendar view -
/// the same two sources ForecastService combines into monthly totals, but at
/// per-occurrence-date granularity instead of a summed figure. Deliberately doesn't
/// consider Budget: a Budget line is a planned total for a category/month, never tied to
/// a specific day, so it has nothing to contribute to a calendar of dated events.
/// </summary>
public sealed class CalendarService(IRecurringExpenseRepository recurringExpenseRepository, ILoanRepository loanRepository)
{
    public async Task<IReadOnlyList<CalendarEntry>> GetMonthlyCalendarAsync(
        int userId, DateOnly month, CancellationToken cancellationToken = default)
    {
        var normalizedMonth = new DateOnly(month.Year, month.Month, 1);

        var recurringExpenses = await recurringExpenseRepository.GetAllForUserAsync(userId, cancellationToken);
        var loans = await loanRepository.GetAllForUserAsync(userId, cancellationToken);

        var entries = new List<CalendarEntry>();

        foreach (var expense in recurringExpenses)
        {
            foreach (var occurrence in RecurringExpenseProjector.GetOccurrenceDates(expense, normalizedMonth))
            {
                entries.Add(new CalendarEntry(
                    occurrence.Date, expense.Label, expense.Amount,
                    CalendarEntryType.RecurringExpense, occurrence.IsLastOccurrence));
            }
        }

        foreach (var loan in loans)
        {
            var occurrence = LoanProjector.GetMonthlyOccurrence(loan, normalizedMonth);
            if (occurrence is not null)
            {
                entries.Add(new CalendarEntry(
                    occurrence.Date, loan.Label, loan.MonthlyPayment, CalendarEntryType.Loan, occurrence.IsLastOccurrence));
            }
        }

        return entries.OrderBy(e => e.Date).ThenBy(e => e.Label).ToList();
    }

    public async Task<IReadOnlyList<CalendarEntry>> GetAnnualCalendarAsync(
        int userId, int year, CancellationToken cancellationToken = default)
    {
        var entries = new List<CalendarEntry>();

        for (var monthNumber = 1; monthNumber <= 12; monthNumber++)
        {
            entries.AddRange(await GetMonthlyCalendarAsync(userId, new DateOnly(year, monthNumber, 1), cancellationToken));
        }

        return entries;
    }
}
