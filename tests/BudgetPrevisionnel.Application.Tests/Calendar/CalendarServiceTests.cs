using BudgetPrevisionnel.Application.Calendar;
using BudgetPrevisionnel.Application.Tests.Loans;
using BudgetPrevisionnel.Application.Tests.RecurringExpenses;
using BudgetPrevisionnel.Domain.Entities;
using BudgetPrevisionnel.Domain.Enums;

namespace BudgetPrevisionnel.Application.Tests.Calendar;

public class CalendarServiceTests
{
    private static (FakeRecurringExpenseRepository RecurringExpenses, FakeLoanRepository Loans, CalendarService Service)
        CreateSubject()
    {
        var recurringExpenses = new FakeRecurringExpenseRepository();
        var loans = new FakeLoanRepository();
        var service = new CalendarService(recurringExpenses, loans);
        return (recurringExpenses, loans, service);
    }

    [Fact]
    public async Task GetMonthlyCalendarAsync_RecurringExpense_ReturnsItsOccurrenceDateAndLabel()
    {
        var (recurringExpenses, _, service) = CreateSubject();
        await recurringExpenses.AddAsync(new RecurringExpense
        {
            UserId = 1, Label = "Loyer", Amount = 800m,
            Frequency = RecurrenceFrequency.Monthly, StartDate = new DateOnly(2026, 1, 5)
        });

        var entries = await service.GetMonthlyCalendarAsync(1, new DateOnly(2026, 9, 1));

        var entry = Assert.Single(entries);
        Assert.Equal(new DateOnly(2026, 9, 5), entry.Date);
        Assert.Equal("Loyer", entry.Label);
        Assert.Equal(800m, entry.Amount);
        Assert.Equal(CalendarEntryType.RecurringExpense, entry.Type);
        Assert.False(entry.IsLastOccurrence);
    }

    [Fact]
    public async Task GetMonthlyCalendarAsync_WeeklyRecurringExpense_ReturnsEveryOccurrenceThatMonth()
    {
        var (recurringExpenses, _, service) = CreateSubject();
        // February 2026 starts on a Sunday; Mondays fall on 2, 9, 16, 23.
        await recurringExpenses.AddAsync(new RecurringExpense
        {
            UserId = 1, Label = "Marché", Amount = 20m,
            Frequency = RecurrenceFrequency.Weekly, StartDate = new DateOnly(2026, 1, 5)
        });

        var entries = await service.GetMonthlyCalendarAsync(1, new DateOnly(2026, 2, 1));

        Assert.Equal(4, entries.Count);
        Assert.All(entries, e => Assert.Equal("Marché", e.Label));
    }

    [Fact]
    public async Task GetMonthlyCalendarAsync_RecurringExpenseEndingThisMonth_IsFlaggedAsLastOccurrence()
    {
        var (recurringExpenses, _, service) = CreateSubject();
        await recurringExpenses.AddAsync(new RecurringExpense
        {
            UserId = 1, Label = "Essai gratuit", Amount = 9.99m,
            Frequency = RecurrenceFrequency.Monthly, StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 9, 20)
        });

        var entries = await service.GetMonthlyCalendarAsync(1, new DateOnly(2026, 9, 1));

        var entry = Assert.Single(entries);
        Assert.True(entry.IsLastOccurrence);
    }

    [Fact]
    public async Task GetMonthlyCalendarAsync_Loan_LandsOnEndDatesDayOfMonth()
    {
        var (_, loans, service) = CreateSubject();
        await loans.AddAsync(new Loan
        {
            UserId = 1, Label = "Prêt auto", PrincipalAmount = 10000m, RemainingAmount = 5000m,
            InterestRate = 3m, MonthlyPayment = 300m, EndDate = new DateOnly(2029, 6, 22)
        });

        var entries = await service.GetMonthlyCalendarAsync(1, new DateOnly(2026, 9, 1));

        var entry = Assert.Single(entries);
        Assert.Equal(new DateOnly(2026, 9, 22), entry.Date);
        Assert.Equal(300m, entry.Amount);
        Assert.Equal(CalendarEntryType.Loan, entry.Type);
        Assert.False(entry.IsLastOccurrence);
    }

    [Fact]
    public async Task GetMonthlyCalendarAsync_LoanEndingThisMonth_IsFlaggedAsLastOccurrence()
    {
        var (_, loans, service) = CreateSubject();
        await loans.AddAsync(new Loan
        {
            UserId = 1, Label = "Dernier mois", PrincipalAmount = 10000m, RemainingAmount = 300m,
            InterestRate = 3m, MonthlyPayment = 300m, EndDate = new DateOnly(2026, 9, 15)
        });

        var entries = await service.GetMonthlyCalendarAsync(1, new DateOnly(2026, 9, 1));

        var entry = Assert.Single(entries);
        Assert.Equal(new DateOnly(2026, 9, 15), entry.Date);
        Assert.True(entry.IsLastOccurrence);
    }

    [Fact]
    public async Task GetMonthlyCalendarAsync_LoanEndedBeforeMonth_IsExcluded()
    {
        var (_, loans, service) = CreateSubject();
        await loans.AddAsync(new Loan
        {
            UserId = 1, Label = "Pret solde", PrincipalAmount = 10000m, RemainingAmount = 0m,
            InterestRate = 3m, MonthlyPayment = 300m, EndDate = new DateOnly(2026, 8, 15)
        });

        var entries = await service.GetMonthlyCalendarAsync(1, new DateOnly(2026, 9, 1));

        Assert.Empty(entries);
    }

    [Fact]
    public async Task GetMonthlyCalendarAsync_AnotherUsersData_NeverIncluded()
    {
        var (recurringExpenses, loans, service) = CreateSubject();
        await recurringExpenses.AddAsync(new RecurringExpense
        {
            UserId = 2, Label = "Pas à moi", Amount = 10m,
            Frequency = RecurrenceFrequency.Monthly, StartDate = new DateOnly(2026, 1, 1)
        });
        await loans.AddAsync(new Loan
        {
            UserId = 2, Label = "Pas à moi non plus", PrincipalAmount = 1000m, RemainingAmount = 500m,
            InterestRate = 0m, MonthlyPayment = 100m, EndDate = new DateOnly(2029, 1, 1)
        });

        var entries = await service.GetMonthlyCalendarAsync(1, new DateOnly(2026, 9, 1));

        Assert.Empty(entries);
    }

    [Fact]
    public async Task GetMonthlyCalendarAsync_CombinesRecurringExpensesAndLoans_SortedByDate()
    {
        var (recurringExpenses, loans, service) = CreateSubject();
        await recurringExpenses.AddAsync(new RecurringExpense
        {
            UserId = 1, Label = "Loyer", Amount = 800m,
            Frequency = RecurrenceFrequency.Monthly, StartDate = new DateOnly(2026, 1, 20)
        });
        await loans.AddAsync(new Loan
        {
            UserId = 1, Label = "Prêt auto", PrincipalAmount = 10000m, RemainingAmount = 5000m,
            InterestRate = 3m, MonthlyPayment = 300m, EndDate = new DateOnly(2029, 6, 5)
        });

        var entries = await service.GetMonthlyCalendarAsync(1, new DateOnly(2026, 9, 1));

        Assert.Equal(2, entries.Count);
        Assert.Equal(new DateOnly(2026, 9, 5), entries[0].Date); // Loan (5th) before RecurringExpense (20th)
        Assert.Equal(new DateOnly(2026, 9, 20), entries[1].Date);
    }

    [Fact]
    public async Task GetAnnualCalendarAsync_ReturnsOccurrencesAcrossAllTwelveMonths()
    {
        var (recurringExpenses, _, service) = CreateSubject();
        await recurringExpenses.AddAsync(new RecurringExpense
        {
            UserId = 1, Label = "Loyer", Amount = 800m,
            Frequency = RecurrenceFrequency.Monthly, StartDate = new DateOnly(2026, 1, 1)
        });

        var entries = await service.GetAnnualCalendarAsync(1, 2026);

        Assert.Equal(12, entries.Count);
        Assert.Equal(new DateOnly(2026, 1, 1), entries[0].Date);
        Assert.Equal(new DateOnly(2026, 12, 1), entries[11].Date);
    }
}
