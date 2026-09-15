using BudgetPrevisionnel.Application.Categories;
using BudgetPrevisionnel.Application.Common;

namespace BudgetPrevisionnel.Application.Tests.Categories;

public class CategoryRuleServiceTests
{
    [Fact]
    public async Task CreateAsync_ValidRule_Persists()
    {
        var categories = new FakeCategoryRepository().Seed(id: 3, name: "Transport");
        var rules = new FakeCategoryRuleRepository();
        var service = new CategoryRuleService(rules, categories);

        var rule = await service.CreateAsync(userId: 1, matchPattern: "TOTAL", categoryId: 3, priority: 1);

        Assert.Equal("TOTAL", rule.MatchPattern);
        Assert.Equal(1, rule.OwnerId);
    }

    [Fact]
    public async Task CreateAsync_EmptyPattern_ThrowsInvalidCategoryRule()
    {
        var categories = new FakeCategoryRepository().Seed(id: 3, name: "Transport");
        var rules = new FakeCategoryRuleRepository();
        var service = new CategoryRuleService(rules, categories);

        await Assert.ThrowsAsync<InvalidCategoryRuleException>(
            () => service.CreateAsync(1, "   ", 3, 1));
    }

    [Fact]
    public async Task CreateAsync_CategoryBelongsToAnotherUser_ThrowsInvalidReference()
    {
        var categories = new FakeCategoryRepository().Seed(id: 5, name: "Perso", ownerId: 2);
        var rules = new FakeCategoryRuleRepository();
        var service = new CategoryRuleService(rules, categories);

        await Assert.ThrowsAsync<InvalidReferenceException>(
            () => service.CreateAsync(userId: 1, "TOTAL", categoryId: 5, priority: 1));
    }

    [Fact]
    public async Task DeleteAsync_AnotherUsersRule_ThrowsCategoryRuleNotFound()
    {
        var categories = new FakeCategoryRepository();
        var rules = new FakeCategoryRuleRepository().Seed(ownerId: 2, matchPattern: "TOTAL", categoryId: 3, priority: 1);
        var service = new CategoryRuleService(rules, categories);

        await Assert.ThrowsAsync<CategoryRuleNotFoundException>(
            () => service.DeleteAsync(userId: 1, ruleId: 1));
    }

    [Fact]
    public async Task DeleteAsync_OwnRule_Removes()
    {
        var categories = new FakeCategoryRepository();
        var rules = new FakeCategoryRuleRepository().Seed(ownerId: 1, matchPattern: "TOTAL", categoryId: 3, priority: 1);
        var service = new CategoryRuleService(rules, categories);

        await service.DeleteAsync(userId: 1, ruleId: 1);

        Assert.Empty(await rules.GetByOwnerOrderedByPriorityAsync(1));
    }
}
