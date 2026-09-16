using BudgetPrevisionnel.Application.Forecasting;
using BudgetPrevisionnel.Domain.Entities;
using BudgetPrevisionnel.Domain.Enums;

namespace BudgetPrevisionnel.Application.Tests.Forecasting;

// Same occurrence math as RecurringExpenseProjectorTests (RecurringIncomeProjector is a
// deliberate duplicate, see its own doc comment) - not re-deriving every edge case here,
// just confirming the copy behaves the same for the cases that matter for income.
public class RecurringIncomeProjectorTests
{
    [Fact]
    public void GetMonthlyContribution_Monthly_ReturnsAmountOnce()
    {
        var income = new RecurringIncome
        {
            Amount = 2200m, Frequency = RecurrenceFrequency.Monthly, StartDate = new DateOnly(2026, 1, 5)
        };

        var amount = RecurringIncomeProjector.GetMonthlyContribution(income, new DateOnly(2026, 9, 1));

        Assert.Equal(2200m, amount);
    }

    [Fact]
    public void GetMonthlyContribution_Weekly_MultipliesByOccurrenceCount()
    {
        // February 2026 starts on a Sunday; Mondays fall on 2, 9, 16, 23 - 4 occurrences.
        var income = new RecurringIncome
        {
            Amount = 50m, Frequency = RecurrenceFrequency.Weekly, StartDate = new DateOnly(2026, 1, 5)
        };

        var amount = RecurringIncomeProjector.GetMonthlyContribution(income, new DateOnly(2026, 2, 1));

        Assert.Equal(200m, amount);
    }

    [Fact]
    public void GetMonthlyContribution_YearlyOutsideAnniversaryMonth_ReturnsZero()
    {
        var income = new RecurringIncome
        {
            Amount = 1000m, Frequency = RecurrenceFrequency.Yearly, StartDate = new DateOnly(2026, 12, 1)
        };

        var amount = RecurringIncomeProjector.GetMonthlyContribution(income, new DateOnly(2026, 9, 1));

        Assert.Equal(0m, amount);
    }

    [Fact]
    public void GetOccurrenceDates_EndingThisMonth_IsFlaggedAsLastOccurrence()
    {
        var income = new RecurringIncome
        {
            Amount = 2200m, Frequency = RecurrenceFrequency.Monthly, StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 9, 20)
        };

        var occurrence = Assert.Single(RecurringIncomeProjector.GetOccurrenceDates(income, new DateOnly(2026, 9, 1)));

        Assert.True(occurrence.IsLastOccurrence);
    }
}
