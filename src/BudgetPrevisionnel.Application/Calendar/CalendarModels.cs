namespace BudgetPrevisionnel.Application.Calendar;

public enum CalendarEntryType
{
    RecurringExpense,
    Loan
}

/// <summary>One concrete, dated occurrence of a RecurringExpense or a Loan payment -
/// unlike ForecastCategoryLine (one summed figure per category per month), this is
/// exactly the granularity a calendar view needs. IsLastOccurrence marks the last
/// payment before EndDate, so the UI can call out "this ends here" instead of the user
/// having to notice a gap in the following month.</summary>
public sealed record CalendarEntry(DateOnly Date, string Label, decimal Amount, CalendarEntryType Type, bool IsLastOccurrence);
