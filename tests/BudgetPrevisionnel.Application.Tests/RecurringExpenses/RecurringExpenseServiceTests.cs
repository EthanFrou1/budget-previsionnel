using BudgetPrevisionnel.Application.Common;
using BudgetPrevisionnel.Application.RecurringExpenses;
using BudgetPrevisionnel.Application.Tests.Categories;
using BudgetPrevisionnel.Domain.Enums;

namespace BudgetPrevisionnel.Application.Tests.RecurringExpenses;

public class RecurringExpenseServiceTests
{
    private static RecurringExpenseService CreateService(FakeCategoryRepository? categories = null) =>
        new(new FakeRecurringExpenseRepository(), categories ?? new FakeCategoryRepository());

    [Fact]
    public async Task CreateAsync_ValidExpense_Persists()
    {
        var service = CreateService();

        var expense = await service.CreateAsync(
            1, "Loyer", 800m, null, RecurrenceFrequency.Monthly, new DateOnly(2026, 1, 1), null);

        Assert.Equal("Loyer", expense.Label);
        Assert.Equal(RecurrenceFrequency.Monthly, expense.Frequency);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public async Task CreateAsync_AmountNotPositive_Throws(decimal amount)
    {
        var service = CreateService();

        await Assert.ThrowsAsync<InvalidRecurringExpenseException>(() => service.CreateAsync(
            1, "Loyer", amount, null, RecurrenceFrequency.Monthly, new DateOnly(2026, 1, 1), null));
    }

    [Fact]
    public async Task CreateAsync_EndDateBeforeStartDate_Throws()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<InvalidRecurringExpenseException>(() => service.CreateAsync(
            1, "Loyer", 800m, null, RecurrenceFrequency.Monthly,
            startDate: new DateOnly(2026, 6, 1), endDate: new DateOnly(2026, 1, 1)));
    }

    [Fact]
    public async Task CreateAsync_EndDateEqualsStartDate_IsAllowed()
    {
        var service = CreateService();

        var expense = await service.CreateAsync(
            1, "Abonnement d'un jour", 10m, null, RecurrenceFrequency.Monthly,
            startDate: new DateOnly(2026, 6, 1), endDate: new DateOnly(2026, 6, 1));

        Assert.Equal(expense.StartDate, expense.EndDate);
    }

    [Fact]
    public async Task CreateAsync_SystemCategory_IsAllowed()
    {
        var categories = new FakeCategoryRepository().Seed(id: 2, name: "Logement");
        var service = CreateService(categories);

        var expense = await service.CreateAsync(
            1, "Loyer", 800m, categoryId: 2, RecurrenceFrequency.Monthly, new DateOnly(2026, 1, 1), null);

        Assert.Equal(2, expense.CategoryId);
    }

    [Fact]
    public async Task CreateAsync_CategoryBelongsToAnotherUser_ThrowsInvalidReference()
    {
        var categories = new FakeCategoryRepository().Seed(id: 5, name: "Perso", ownerId: 2);
        var service = CreateService(categories);

        await Assert.ThrowsAsync<InvalidReferenceException>(() => service.CreateAsync(
            1, "Loyer", 800m, categoryId: 5, RecurrenceFrequency.Monthly, new DateOnly(2026, 1, 1), null));
    }

    [Fact]
    public async Task UpdateAsync_AnotherUsersExpense_ThrowsRecurringExpenseNotFound()
    {
        var service = CreateService();
        var expense = await service.CreateAsync(
            2, "Pas à moi", 800m, null, RecurrenceFrequency.Monthly, new DateOnly(2026, 1, 1), null);

        await Assert.ThrowsAsync<RecurringExpenseNotFoundException>(() => service.UpdateAsync(
            1, expense.Id, "Modifié", 800m, null, RecurrenceFrequency.Monthly, new DateOnly(2026, 1, 1), null));
    }

    [Fact]
    public async Task DeleteAsync_OwnExpense_Removes()
    {
        var service = CreateService();
        var expense = await service.CreateAsync(
            1, "Loyer", 800m, null, RecurrenceFrequency.Monthly, new DateOnly(2026, 1, 1), null);

        await service.DeleteAsync(1, expense.Id);

        Assert.Empty(await service.GetAllForUserAsync(1));
    }
}
