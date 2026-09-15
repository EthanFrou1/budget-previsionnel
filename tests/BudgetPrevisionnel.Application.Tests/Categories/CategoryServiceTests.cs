using BudgetPrevisionnel.Application.Categories;
using BudgetPrevisionnel.Application.Common;

namespace BudgetPrevisionnel.Application.Tests.Categories;

public class CategoryServiceTests
{
    [Fact]
    public async Task CreateAsync_NoParent_CreatesCustomCategoryOwnedByUser()
    {
        var repository = new FakeCategoryRepository();
        var service = new CategoryService(repository);

        var category = await service.CreateAsync(userId: 1, "Netflix", icon: null, color: null, parentCategoryId: null);

        Assert.False(category.IsSystemDefault);
        Assert.Equal(1, category.OwnerId);
    }

    [Fact]
    public async Task CreateAsync_ParentIsSystemCategory_Allowed()
    {
        var repository = new FakeCategoryRepository().Seed(id: 4, name: "Abonnements & téléphonie");
        var service = new CategoryService(repository);

        var category = await service.CreateAsync(1, "Netflix", null, null, parentCategoryId: 4);

        Assert.Equal(4, category.ParentCategoryId);
    }

    [Fact]
    public async Task CreateAsync_ParentBelongsToAnotherUser_ThrowsInvalidReference()
    {
        var repository = new FakeCategoryRepository().Seed(id: 5, name: "Perso", ownerId: 2);
        var service = new CategoryService(repository);

        await Assert.ThrowsAsync<InvalidReferenceException>(
            () => service.CreateAsync(userId: 1, "Sous-catégorie", null, null, parentCategoryId: 5));
    }

    [Fact]
    public async Task UpdateAsync_SystemCategory_ThrowsCategoryNotEditable()
    {
        var repository = new FakeCategoryRepository().Seed(id: 1, name: "Alimentation");
        var service = new CategoryService(repository);

        await Assert.ThrowsAsync<CategoryNotEditableException>(
            () => service.UpdateAsync(userId: 1, categoryId: 1, "Bouffe", null, null, null));
    }

    [Fact]
    public async Task UpdateAsync_AnotherUsersCategory_ThrowsCategoryNotEditable()
    {
        var repository = new FakeCategoryRepository().Seed(id: 5, name: "Perso", ownerId: 2);
        var service = new CategoryService(repository);

        await Assert.ThrowsAsync<CategoryNotEditableException>(
            () => service.UpdateAsync(userId: 1, categoryId: 5, "Modifié", null, null, null));
    }

    [Fact]
    public async Task UpdateAsync_SelfAsParent_ThrowsInvalidCategoryParent()
    {
        var repository = new FakeCategoryRepository().Seed(id: 5, name: "Perso", ownerId: 1);
        var service = new CategoryService(repository);

        await Assert.ThrowsAsync<InvalidCategoryParentException>(
            () => service.UpdateAsync(userId: 1, categoryId: 5, "Perso", null, null, parentCategoryId: 5));
    }

    [Fact]
    public async Task UpdateAsync_ParentCreatesACycle_ThrowsInvalidCategoryParent()
    {
        // A (id 5) is currently the parent of B (id 6). Making A a child of B would
        // create a cycle A -> B -> A.
        var repository = new FakeCategoryRepository()
            .Seed(id: 5, name: "A", ownerId: 1)
            .Seed(id: 6, name: "B", parentCategoryId: 5, ownerId: 1);
        var service = new CategoryService(repository);

        await Assert.ThrowsAsync<InvalidCategoryParentException>(
            () => service.UpdateAsync(userId: 1, categoryId: 5, "A", null, null, parentCategoryId: 6));
    }

    [Fact]
    public async Task UpdateAsync_ValidChange_Persists()
    {
        var repository = new FakeCategoryRepository().Seed(id: 5, name: "Perso", ownerId: 1);
        var service = new CategoryService(repository);

        var updated = await service.UpdateAsync(userId: 1, categoryId: 5, "Nouveau nom", "icon", "#FFFFFF", null);

        Assert.Equal("Nouveau nom", updated.Name);
        Assert.Equal("icon", updated.Icon);
        Assert.Equal("#FFFFFF", updated.Color);
    }

    [Fact]
    public async Task DeleteAsync_SystemCategory_ThrowsCategoryNotEditable()
    {
        var repository = new FakeCategoryRepository().Seed(id: 1, name: "Alimentation");
        var service = new CategoryService(repository);

        await Assert.ThrowsAsync<CategoryNotEditableException>(
            () => service.DeleteAsync(userId: 1, categoryId: 1));
    }

    [Fact]
    public async Task DeleteAsync_HasSubCategories_ThrowsCategoryHasSubCategories()
    {
        var repository = new FakeCategoryRepository()
            .Seed(id: 5, name: "Parent", ownerId: 1)
            .Seed(id: 6, name: "Enfant", parentCategoryId: 5, ownerId: 1);
        var service = new CategoryService(repository);

        await Assert.ThrowsAsync<CategoryHasSubCategoriesException>(
            () => service.DeleteAsync(userId: 1, categoryId: 5));
    }

    [Fact]
    public async Task DeleteAsync_OwnCategoryNoSubCategories_Removes()
    {
        var repository = new FakeCategoryRepository().Seed(id: 5, name: "Perso", ownerId: 1);
        var service = new CategoryService(repository);

        await service.DeleteAsync(userId: 1, categoryId: 5);

        Assert.Empty(repository.All);
    }

    [Fact]
    public async Task GetVisibleToUserAsync_ExcludesOtherUsersCustomCategories()
    {
        var repository = new FakeCategoryRepository()
            .Seed(id: 1, name: "Alimentation")
            .Seed(id: 5, name: "Mine", ownerId: 1)
            .Seed(id: 6, name: "TheirsNotMine", ownerId: 2);
        var service = new CategoryService(repository);

        var visible = await service.GetVisibleToUserAsync(userId: 1);

        Assert.Equal(["Alimentation", "Mine"], visible.Select(c => c.Name));
    }
}
