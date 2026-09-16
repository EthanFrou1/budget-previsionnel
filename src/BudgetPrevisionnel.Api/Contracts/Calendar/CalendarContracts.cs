using BudgetPrevisionnel.Application.Calendar;

namespace BudgetPrevisionnel.Api.Contracts.Calendar;

public sealed record CalendarEntryResponse(
    DateOnly Date, string Label, decimal Amount, CalendarEntryType Type, bool IsLastOccurrence)
{
    public static CalendarEntryResponse FromEntry(CalendarEntry entry) => new(
        entry.Date, entry.Label, entry.Amount, entry.Type, entry.IsLastOccurrence);
}
