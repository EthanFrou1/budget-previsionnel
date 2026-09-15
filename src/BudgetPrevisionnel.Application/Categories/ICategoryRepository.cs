using BudgetPrevisionnel.Domain.Entities;

namespace BudgetPrevisionnel.Application.Categories;

public interface ICategoryRepository
{
    Task<Category?> FindSystemCategoryByNameAsync(string name, CancellationToken cancellationToken = default);

    Task<Category?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>All system categories, plus this user's own custom categories - never
    /// another user's, per the brief's "no sharing of custom categories" decision.</summary>
    Task<IReadOnlyList<Category>> GetVisibleToUserAsync(int userId, CancellationToken cancellationToken = default);

    Task<bool> HasSubCategoriesAsync(int categoryId, CancellationToken cancellationToken = default);

    Task AddAsync(Category category, CancellationToken cancellationToken = default);

    Task DeleteAsync(Category category, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
