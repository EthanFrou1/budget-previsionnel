using BudgetPrevisionnel.Application.Common;
using BudgetPrevisionnel.Domain.Entities;

namespace BudgetPrevisionnel.Application.Categories;

public sealed class CategoryService(ICategoryRepository categoryRepository)
{
    private const int MaxHierarchyDepth = 100; // Defensive cap for EnsureNoCycleAsync; brief only calls for 2 levels.

    public Task<IReadOnlyList<Category>> GetVisibleToUserAsync(int userId, CancellationToken cancellationToken = default) =>
        categoryRepository.GetVisibleToUserAsync(userId, cancellationToken);

    public async Task<Category> CreateAsync(
        int userId, string name, string? icon, string? color, int? parentCategoryId,
        CancellationToken cancellationToken = default)
    {
        if (parentCategoryId is not null)
        {
            await EnsureParentIsVisibleAsync(userId, parentCategoryId.Value, cancellationToken);
        }

        var category = new Category
        {
            Name = name.Trim(),
            Icon = icon,
            Color = color,
            ParentCategoryId = parentCategoryId,
            IsSystemDefault = false,
            OwnerId = userId
        };

        await categoryRepository.AddAsync(category, cancellationToken);
        return category;
    }

    public async Task<Category> UpdateAsync(
        int userId, int categoryId, string name, string? icon, string? color, int? parentCategoryId,
        CancellationToken cancellationToken = default)
    {
        var category = await categoryRepository.GetByIdAsync(categoryId, cancellationToken)
            ?? throw new CategoryNotFoundException(categoryId);

        EnsureOwnedByUser(category, userId);

        if (parentCategoryId is not null)
        {
            if (parentCategoryId == categoryId)
            {
                throw new InvalidCategoryParentException("A category cannot be its own parent.");
            }

            await EnsureParentIsVisibleAsync(userId, parentCategoryId.Value, cancellationToken);
            await EnsureNoCycleAsync(categoryId, parentCategoryId.Value, cancellationToken);
        }

        category.Name = name.Trim();
        category.Icon = icon;
        category.Color = color;
        category.ParentCategoryId = parentCategoryId;

        await categoryRepository.SaveChangesAsync(cancellationToken);
        return category;
    }

    public async Task DeleteAsync(int userId, int categoryId, CancellationToken cancellationToken = default)
    {
        var category = await categoryRepository.GetByIdAsync(categoryId, cancellationToken)
            ?? throw new CategoryNotFoundException(categoryId);

        EnsureOwnedByUser(category, userId);

        if (await categoryRepository.HasSubCategoriesAsync(categoryId, cancellationToken))
        {
            throw new CategoryHasSubCategoriesException(categoryId);
        }

        // Budgets planned against this category cascade-delete with it (Lot 0 schema
        // decision: a planned amount has no meaning once its category is gone).
        // Transactions/RecurringExpenses referencing it just lose the reference (SetNull).
        await categoryRepository.DeleteAsync(category, cancellationToken);
    }

    private static void EnsureOwnedByUser(Category category, int userId)
    {
        if (category.IsSystemDefault || category.OwnerId != userId)
        {
            throw new CategoryNotEditableException(category.Id);
        }
    }

    private async Task EnsureParentIsVisibleAsync(int userId, int parentCategoryId, CancellationToken cancellationToken)
    {
        var parent = await categoryRepository.GetByIdAsync(parentCategoryId, cancellationToken);
        if (parent is null || (!parent.IsSystemDefault && parent.OwnerId != userId))
        {
            // Same exception as "doesn't exist" for a parent owned by someone else -
            // don't leak whether another user's category id is in use.
            throw new InvalidReferenceException(nameof(Category), parentCategoryId);
        }
    }

    private async Task EnsureNoCycleAsync(int categoryId, int newParentId, CancellationToken cancellationToken)
    {
        int? currentId = newParentId;
        var depth = 0;

        while (currentId is not null)
        {
            if (currentId == categoryId)
            {
                throw new InvalidCategoryParentException("This would create a cycle in the category hierarchy.");
            }

            if (++depth > MaxHierarchyDepth)
            {
                throw new InvalidCategoryParentException("Category hierarchy is too deep.");
            }

            var parent = await categoryRepository.GetByIdAsync(currentId.Value, cancellationToken);
            currentId = parent?.ParentCategoryId;
        }
    }
}
