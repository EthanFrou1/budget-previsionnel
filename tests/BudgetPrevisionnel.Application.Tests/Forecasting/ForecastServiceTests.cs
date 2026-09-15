using BudgetPrevisionnel.Application.Forecasting;
using BudgetPrevisionnel.Application.Tests.Budgets;
using BudgetPrevisionnel.Application.Tests.Loans;
using BudgetPrevisionnel.Application.Tests.RecurringExpenses;
using BudgetPrevisionnel.Domain.Entities;
using BudgetPrevisionnel.Domain.Enums;

namespace BudgetPrevisionnel.Application.Tests.Forecasting;

public class ForecastServiceTests
{
    private static readonly Category Logement = new() { Id = 2, Name = "Logement", IsSystemDefault = true };
    private static readonly Category Alimentation = new() { Id = 1, Name = "Alimentation", IsSystemDefault = true };

    private static (FakeBudgetRepository Budgets, FakeRecurringExpenseRepository RecurringExpenses, FakeLoanRepository Loans, ForecastService Service)
        CreateSubject()
    {
        var budgets = new FakeBudgetRepository();
        var recurringExpenses = new FakeRecurringExpenseRepository();
        var loans = new FakeLoanRepository();
        var service = new ForecastService(budgets, recurringExpenses, loans);
        return (budgets, recurringExpenses, loans, service);
    }

    [Fact]
    public async Task GetMonthlyForecastAsync_BudgetLine_IsIncludedAsBudgetSource()
    {
        var (budgets, _, _, service) = CreateSubject();
        var month = new DateOnly(2026, 9, 1);
        budgets.Seed(1, month, Logement, plannedAmount: 800m);

        var forecast = await service.GetMonthlyForecastAsync(1, month);

        var line = Assert.Single(forecast.CategoryLines);
        Assert.Equal(ForecastSource.Budget, line.Source);
        Assert.Equal(800m, line.Amount);
        Assert.Equal(800m, forecast.Total);
    }

    [Fact]
    public async Task GetMonthlyForecastAsync_RecurringExpenseWithoutBudget_FillsInAsRecurringExpenseSource()
    {
        var (_, recurringExpenses, _, service) = CreateSubject();
        var month = new DateOnly(2026, 9, 1);
        await recurringExpenses.AddAsync(new RecurringExpense
        {
            UserId = 1, Label = "Loyer", Amount = 800m, CategoryId = Logement.Id, Category = Logement,
            Frequency = RecurrenceFrequency.Monthly, StartDate = new DateOnly(2026, 1, 1)
        });

        var forecast = await service.GetMonthlyForecastAsync(1, month);

        var line = Assert.Single(forecast.CategoryLines);
        Assert.Equal(ForecastSource.RecurringExpense, line.Source);
        Assert.Equal(800m, line.Amount);
    }

    [Fact]
    public async Task GetMonthlyForecastAsync_CategoryHasBothBudgetAndRecurringExpense_BudgetWinsNoDoubleCounting()
    {
        var (budgets, recurringExpenses, _, service) = CreateSubject();
        var month = new DateOnly(2026, 9, 1);
        budgets.Seed(1, month, Logement, plannedAmount: 900m); // user's deliberate override
        await recurringExpenses.AddAsync(new RecurringExpense
        {
            UserId = 1, Label = "Loyer", Amount = 800m, CategoryId = Logement.Id, Category = Logement,
            Frequency = RecurrenceFrequency.Monthly, StartDate = new DateOnly(2026, 1, 1)
        });

        var forecast = await service.GetMonthlyForecastAsync(1, month);

        var line = Assert.Single(forecast.CategoryLines);
        Assert.Equal(ForecastSource.Budget, line.Source);
        Assert.Equal(900m, line.Amount); // not 900 + 800
    }

    [Fact]
    public async Task GetMonthlyForecastAsync_UncategorizedRecurringExpense_IsIncludedWithNullLabel()
    {
        var (_, recurringExpenses, _, service) = CreateSubject();
        var month = new DateOnly(2026, 9, 1);
        await recurringExpenses.AddAsync(new RecurringExpense
        {
            UserId = 1, Label = "Divers", Amount = 15m, CategoryId = null,
            Frequency = RecurrenceFrequency.Monthly, StartDate = new DateOnly(2026, 1, 1)
        });

        var forecast = await service.GetMonthlyForecastAsync(1, month);

        var line = Assert.Single(forecast.CategoryLines);
        Assert.Null(line.CategoryId);
        Assert.Null(line.CategoryLabel);
        Assert.Equal(15m, line.Amount);
    }

    [Fact]
    public async Task GetMonthlyForecastAsync_MultipleRecurringExpensesSameCategory_AreSummed()
    {
        var (_, recurringExpenses, _, service) = CreateSubject();
        var month = new DateOnly(2026, 9, 1);
        await recurringExpenses.AddAsync(new RecurringExpense
        {
            UserId = 1, Label = "Netflix", Amount = 15m, CategoryId = Alimentation.Id, Category = Alimentation,
            Frequency = RecurrenceFrequency.Monthly, StartDate = new DateOnly(2026, 1, 1)
        });
        await recurringExpenses.AddAsync(new RecurringExpense
        {
            UserId = 1, Label = "Spotify", Amount = 10m, CategoryId = Alimentation.Id, Category = Alimentation,
            Frequency = RecurrenceFrequency.Monthly, StartDate = new DateOnly(2026, 1, 1)
        });

        var forecast = await service.GetMonthlyForecastAsync(1, month);

        var line = Assert.Single(forecast.CategoryLines);
        Assert.Equal(25m, line.Amount);
    }

    [Fact]
    public async Task GetMonthlyForecastAsync_LoanActiveInMonth_AddedToLoanPaymentsAndTotal()
    {
        var (_, _, loans, service) = CreateSubject();
        var month = new DateOnly(2026, 9, 1);
        await loans.AddAsync(new Loan
        {
            UserId = 1, Label = "Pret auto", PrincipalAmount = 10000m, RemainingAmount = 5000m,
            InterestRate = 3m, MonthlyPayment = 300m, EndDate = new DateOnly(2029, 1, 1)
        });

        var forecast = await service.GetMonthlyForecastAsync(1, month);

        Assert.Equal(300m, forecast.LoanPayments);
        Assert.Equal(300m, forecast.Total);
        Assert.Empty(forecast.CategoryLines); // loans have no CategoryId in the Domain model
    }

    [Fact]
    public async Task GetMonthlyForecastAsync_LoanEndedBeforeMonth_ExcludedFromLoanPayments()
    {
        var (_, _, loans, service) = CreateSubject();
        await loans.AddAsync(new Loan
        {
            UserId = 1, Label = "Pret solde", PrincipalAmount = 10000m, RemainingAmount = 0m,
            InterestRate = 3m, MonthlyPayment = 300m, EndDate = new DateOnly(2026, 8, 15)
        });

        var forecast = await service.GetMonthlyForecastAsync(1, new DateOnly(2026, 9, 1));

        Assert.Equal(0m, forecast.LoanPayments);
    }

    [Fact]
    public async Task GetMonthlyForecastAsync_LoanEndingDuringMonth_StillIncluded()
    {
        var (_, _, loans, service) = CreateSubject();
        await loans.AddAsync(new Loan
        {
            UserId = 1, Label = "Dernier mois", PrincipalAmount = 10000m, RemainingAmount = 300m,
            InterestRate = 3m, MonthlyPayment = 300m, EndDate = new DateOnly(2026, 9, 15)
        });

        var forecast = await service.GetMonthlyForecastAsync(1, new DateOnly(2026, 9, 1));

        Assert.Equal(300m, forecast.LoanPayments);
    }

    [Fact]
    public async Task GetMonthlyForecastAsync_CombinesBudgetRecurringExpenseAndLoan_IntoOneTotal()
    {
        var (budgets, recurringExpenses, loans, service) = CreateSubject();
        var month = new DateOnly(2026, 9, 1);
        budgets.Seed(1, month, Alimentation, plannedAmount: 400m);
        await recurringExpenses.AddAsync(new RecurringExpense
        {
            UserId = 1, Label = "Loyer", Amount = 800m, CategoryId = Logement.Id, Category = Logement,
            Frequency = RecurrenceFrequency.Monthly, StartDate = new DateOnly(2026, 1, 1)
        });
        await loans.AddAsync(new Loan
        {
            UserId = 1, Label = "Pret auto", PrincipalAmount = 10000m, RemainingAmount = 5000m,
            InterestRate = 3m, MonthlyPayment = 300m, EndDate = new DateOnly(2029, 1, 1)
        });

        var forecast = await service.GetMonthlyForecastAsync(1, month);

        Assert.Equal(400m + 800m + 300m, forecast.Total);
    }

    [Fact]
    public async Task GetAnnualForecastAsync_ReturnsTwelveMonths()
    {
        var (_, _, _, service) = CreateSubject();

        var forecasts = await service.GetAnnualForecastAsync(1, 2026);

        Assert.Equal(12, forecasts.Count);
        Assert.Equal(new DateOnly(2026, 1, 1), forecasts[0].Month);
        Assert.Equal(new DateOnly(2026, 12, 1), forecasts[11].Month);
    }
}
