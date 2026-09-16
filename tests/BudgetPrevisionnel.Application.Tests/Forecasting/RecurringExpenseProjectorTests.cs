using BudgetPrevisionnel.Application.Forecasting;
using BudgetPrevisionnel.Domain.Entities;
using BudgetPrevisionnel.Domain.Enums;

namespace BudgetPrevisionnel.Application.Tests.Forecasting;

public class RecurringExpenseProjectorTests
{
    private static RecurringExpense Expense(
        decimal amount, RecurrenceFrequency frequency, DateOnly startDate, DateOnly? endDate = null) => new()
    {
        Label = "test",
        Amount = amount,
        Frequency = frequency,
        StartDate = startDate,
        EndDate = endDate
    };

    [Fact]
    public void GetMonthlyContribution_Monthly_ReturnsFullAmount()
    {
        var expense = Expense(800m, RecurrenceFrequency.Monthly, new DateOnly(2026, 1, 1));

        var amount = RecurringExpenseProjector.GetMonthlyContribution(expense, new DateOnly(2026, 6, 1));

        Assert.Equal(800m, amount);
    }

    [Fact]
    public void GetMonthlyContribution_MonthlyBeforeStartDate_ReturnsZero()
    {
        var expense = Expense(800m, RecurrenceFrequency.Monthly, new DateOnly(2026, 6, 1));

        var amount = RecurringExpenseProjector.GetMonthlyContribution(expense, new DateOnly(2026, 1, 1));

        Assert.Equal(0m, amount);
    }

    [Fact]
    public void GetMonthlyContribution_MonthlyAfterEndDate_ReturnsZero()
    {
        var expense = Expense(800m, RecurrenceFrequency.Monthly, new DateOnly(2026, 1, 1), new DateOnly(2026, 5, 31));

        var amount = RecurringExpenseProjector.GetMonthlyContribution(expense, new DateOnly(2026, 6, 1));

        Assert.Equal(0m, amount);
    }

    [Fact]
    public void GetMonthlyContribution_MonthlyEndingMidMonth_StillCountsThatMonth()
    {
        // Ends June 15th - June itself should still get the payment.
        var expense = Expense(800m, RecurrenceFrequency.Monthly, new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 15));

        var amount = RecurringExpenseProjector.GetMonthlyContribution(expense, new DateOnly(2026, 6, 1));

        Assert.Equal(800m, amount);
    }

    [Fact]
    public void GetMonthlyContribution_Yearly_OnAnniversaryMonth_ReturnsFullAmount()
    {
        var expense = Expense(120m, RecurrenceFrequency.Yearly, new DateOnly(2024, 3, 15));

        var amount = RecurringExpenseProjector.GetMonthlyContribution(expense, new DateOnly(2026, 3, 1));

        Assert.Equal(120m, amount);
    }

    [Fact]
    public void GetMonthlyContribution_Yearly_OffAnniversaryMonth_ReturnsZero()
    {
        var expense = Expense(120m, RecurrenceFrequency.Yearly, new DateOnly(2024, 3, 15));

        var amount = RecurringExpenseProjector.GetMonthlyContribution(expense, new DateOnly(2026, 4, 1));

        Assert.Equal(0m, amount);
    }

    [Fact]
    public void GetMonthlyContribution_Weekly_FullMonthWithFourOccurrences()
    {
        // February 2026 starts on a Sunday. Mondays: 2, 9, 16, 23 -> 4 occurrences.
        var expense = Expense(20m, RecurrenceFrequency.Weekly, new DateOnly(2026, 1, 5)); // a Monday

        var amount = RecurringExpenseProjector.GetMonthlyContribution(expense, new DateOnly(2026, 2, 1));

        Assert.Equal(80m, amount); // 4 Mondays * 20
    }

    [Fact]
    public void GetMonthlyContribution_Weekly_MonthWithFiveOccurrences_IsNotApproximated()
    {
        // June 2026: 2026-06-01 is a Monday, so Mondays fall on 1, 8, 15, 22, 29 -> 5 occurrences.
        // A flat "4.33 weeks/month" approximation would get this wrong.
        var expense = Expense(20m, RecurrenceFrequency.Weekly, new DateOnly(2026, 1, 5)); // a Monday

        var amount = RecurringExpenseProjector.GetMonthlyContribution(expense, new DateOnly(2026, 6, 1));

        Assert.Equal(100m, amount); // 5 Mondays * 20
    }

    [Fact]
    public void GetMonthlyContribution_Weekly_StartingMidMonth_OnlyCountsOccurrencesFromStartDate()
    {
        // Starts Monday 2026-06-15: remaining Mondays in June are 15, 22, 29 -> 3 occurrences.
        var expense = Expense(20m, RecurrenceFrequency.Weekly, new DateOnly(2026, 6, 15));

        var amount = RecurringExpenseProjector.GetMonthlyContribution(expense, new DateOnly(2026, 6, 1));

        Assert.Equal(60m, amount);
    }

    [Fact]
    public void GetMonthlyContribution_Weekly_EndingMidMonth_OnlyCountsOccurrencesUpToEndDate()
    {
        // Weekday is Monday, ends Monday 2026-06-15 inclusive: Mondays 1, 8, 15 -> 3 occurrences.
        var expense = Expense(20m, RecurrenceFrequency.Weekly, new DateOnly(2026, 1, 5), new DateOnly(2026, 6, 15));

        var amount = RecurringExpenseProjector.GetMonthlyContribution(expense, new DateOnly(2026, 6, 1));

        Assert.Equal(60m, amount);
    }

    [Fact]
    public void GetMonthlyContribution_Weekly_EndDateCutsRangeBeforeAnyOccurrence_ReturnsZero()
    {
        // Weekday is Monday (from StartDate). EndDate falls on 2026-03-01, a Sunday, and
        // cuts the March range down to just that single day - before the month's first
        // Monday (March 2nd) - so despite technically being "active" in March, no
        // occurrence actually lands within it.
        var expense = Expense(20m, RecurrenceFrequency.Weekly, new DateOnly(2026, 1, 5), new DateOnly(2026, 3, 1));

        var amount = RecurringExpenseProjector.GetMonthlyContribution(expense, new DateOnly(2026, 3, 1));

        Assert.Equal(0m, amount);
    }

    [Fact]
    public void GetOccurrenceDates_Monthly_LandsOnStartDatesDayOfMonth()
    {
        var expense = Expense(800m, RecurrenceFrequency.Monthly, new DateOnly(2026, 1, 15));

        var occurrences = RecurringExpenseProjector.GetOccurrenceDates(expense, new DateOnly(2026, 6, 1));

        var occurrence = Assert.Single(occurrences);
        Assert.Equal(new DateOnly(2026, 6, 15), occurrence.Date);
        Assert.False(occurrence.IsLastOccurrence);
    }

    [Fact]
    public void GetOccurrenceDates_Monthly_DayBeyondMonthLength_ClampsToLastDayOfMonth()
    {
        // Starts on the 31st - February (28 days in 2026) only has a 28th.
        var expense = Expense(50m, RecurrenceFrequency.Monthly, new DateOnly(2026, 1, 31));

        var occurrences = RecurringExpenseProjector.GetOccurrenceDates(expense, new DateOnly(2026, 2, 1));

        var occurrence = Assert.Single(occurrences);
        Assert.Equal(new DateOnly(2026, 2, 28), occurrence.Date);
    }

    [Fact]
    public void GetOccurrenceDates_Monthly_LastMonthBeforeEndDate_IsFlaggedAsLastOccurrence()
    {
        // Ends 2026-06-15, lands on the 1st of each month - July's occurrence (the 1st)
        // would fall after EndDate, so June's is the last one.
        var expense = Expense(800m, RecurrenceFrequency.Monthly, new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 15));

        var occurrences = RecurringExpenseProjector.GetOccurrenceDates(expense, new DateOnly(2026, 6, 1));

        var occurrence = Assert.Single(occurrences);
        Assert.Equal(new DateOnly(2026, 6, 1), occurrence.Date);
        Assert.True(occurrence.IsLastOccurrence);
    }

    [Fact]
    public void GetOccurrenceDates_Monthly_MonthAfterOccurrenceDayPassesEndDate_ReturnsEmpty()
    {
        // Lands on the 20th; EndDate is the 10th of that same month - the cycle already
        // ended before this month's would-be occurrence day.
        var expense = Expense(800m, RecurrenceFrequency.Monthly, new DateOnly(2026, 1, 20), new DateOnly(2026, 6, 10));

        var occurrences = RecurringExpenseProjector.GetOccurrenceDates(expense, new DateOnly(2026, 6, 1));

        Assert.Empty(occurrences);
    }

    [Fact]
    public void GetOccurrenceDates_Yearly_LandsOnAnniversaryDate()
    {
        var expense = Expense(120m, RecurrenceFrequency.Yearly, new DateOnly(2024, 3, 15));

        var occurrences = RecurringExpenseProjector.GetOccurrenceDates(expense, new DateOnly(2026, 3, 1));

        var occurrence = Assert.Single(occurrences);
        Assert.Equal(new DateOnly(2026, 3, 15), occurrence.Date);
    }

    [Fact]
    public void GetOccurrenceDates_Weekly_ReturnsEveryOccurrenceDateInMonth()
    {
        // February 2026 starts on a Sunday; Mondays fall on 2, 9, 16, 23.
        var expense = Expense(20m, RecurrenceFrequency.Weekly, new DateOnly(2026, 1, 5)); // a Monday

        var occurrences = RecurringExpenseProjector.GetOccurrenceDates(expense, new DateOnly(2026, 2, 1));

        Assert.Equal(
            [new DateOnly(2026, 2, 2), new DateOnly(2026, 2, 9), new DateOnly(2026, 2, 16), new DateOnly(2026, 2, 23)],
            occurrences.Select(o => o.Date));
        Assert.All(occurrences, o => Assert.False(o.IsLastOccurrence));
    }

    [Fact]
    public void GetOccurrenceDates_Weekly_LastOccurrenceBeforeEndDate_IsFlagged()
    {
        // Ends Monday 2026-06-15 inclusive: Mondays 1, 8, 15 - the 15th is the last one
        // since the next (22nd) would fall after EndDate.
        var expense = Expense(20m, RecurrenceFrequency.Weekly, new DateOnly(2026, 1, 5), new DateOnly(2026, 6, 15));

        var occurrences = RecurringExpenseProjector.GetOccurrenceDates(expense, new DateOnly(2026, 6, 1));

        Assert.Equal(3, occurrences.Count);
        Assert.False(occurrences[0].IsLastOccurrence);
        Assert.False(occurrences[1].IsLastOccurrence);
        Assert.True(occurrences[2].IsLastOccurrence);
        Assert.Equal(new DateOnly(2026, 6, 15), occurrences[2].Date);
    }
}
