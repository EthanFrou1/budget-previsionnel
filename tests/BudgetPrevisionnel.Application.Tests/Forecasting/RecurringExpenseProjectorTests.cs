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
}
