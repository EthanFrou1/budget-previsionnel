using BudgetPrevisionnel.Application.Common;
using BudgetPrevisionnel.Application.RecurringIncomes;
using BudgetPrevisionnel.Application.Tests.Categories;
using BudgetPrevisionnel.Domain.Enums;

namespace BudgetPrevisionnel.Application.Tests.RecurringIncomes;

public class RecurringIncomeServiceTests
{
    private static RecurringIncomeService CreateService(FakeCategoryRepository? categories = null) =>
        new(new FakeRecurringIncomeRepository(), categories ?? new FakeCategoryRepository());

    [Fact]
    public async Task CreateAsync_ValidIncome_Persists()
    {
        var service = CreateService();

        var income = await service.CreateAsync(
            1, "Salaire", 2200m, null, RecurrenceFrequency.Monthly, new DateOnly(2026, 1, 1), null);

        Assert.Equal("Salaire", income.Label);
        Assert.Equal(RecurrenceFrequency.Monthly, income.Frequency);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public async Task CreateAsync_AmountNotPositive_Throws(decimal amount)
    {
        var service = CreateService();

        await Assert.ThrowsAsync<InvalidRecurringIncomeException>(() => service.CreateAsync(
            1, "Salaire", amount, null, RecurrenceFrequency.Monthly, new DateOnly(2026, 1, 1), null));
    }

    [Fact]
    public async Task CreateAsync_EndDateBeforeStartDate_Throws()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<InvalidRecurringIncomeException>(() => service.CreateAsync(
            1, "Salaire", 2200m, null, RecurrenceFrequency.Monthly,
            startDate: new DateOnly(2026, 6, 1), endDate: new DateOnly(2026, 1, 1)));
    }

    [Fact]
    public async Task CreateAsync_SystemCategory_IsAllowed()
    {
        var categories = new FakeCategoryRepository().Seed(id: 2, name: "Salaire");
        var service = CreateService(categories);

        var income = await service.CreateAsync(
            1, "Salaire", 2200m, categoryId: 2, RecurrenceFrequency.Monthly, new DateOnly(2026, 1, 1), null);

        Assert.Equal(2, income.CategoryId);
    }

    [Fact]
    public async Task CreateAsync_CategoryBelongsToAnotherUser_ThrowsInvalidReference()
    {
        var categories = new FakeCategoryRepository().Seed(id: 5, name: "Perso", ownerId: 2);
        var service = CreateService(categories);

        await Assert.ThrowsAsync<InvalidReferenceException>(() => service.CreateAsync(
            1, "Salaire", 2200m, categoryId: 5, RecurrenceFrequency.Monthly, new DateOnly(2026, 1, 1), null));
    }

    [Fact]
    public async Task UpdateAsync_AnotherUsersIncome_ThrowsRecurringIncomeNotFound()
    {
        var service = CreateService();
        var income = await service.CreateAsync(
            2, "Pas à moi", 2200m, null, RecurrenceFrequency.Monthly, new DateOnly(2026, 1, 1), null);

        await Assert.ThrowsAsync<RecurringIncomeNotFoundException>(() => service.UpdateAsync(
            1, income.Id, "Modifié", 2200m, null, RecurrenceFrequency.Monthly, new DateOnly(2026, 1, 1), null));
    }

    [Fact]
    public async Task DeleteAsync_OwnIncome_Removes()
    {
        var service = CreateService();
        var income = await service.CreateAsync(
            1, "Salaire", 2200m, null, RecurrenceFrequency.Monthly, new DateOnly(2026, 1, 1), null);

        await service.DeleteAsync(1, income.Id);

        Assert.Empty(await service.GetAllForUserAsync(1));
    }
}
