using BudgetPrevisionnel.Domain.Entities;

namespace BudgetPrevisionnel.Application.Categories;

public interface ICategoryRuleRepository
{
    /// <summary>Ordered ascending by Priority - lower values are evaluated first.</summary>
    Task<IReadOnlyList<CategoryRule>> GetByOwnerOrderedByPriorityAsync(int ownerId, CancellationToken cancellationToken = default);

    Task<CategoryRule?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task AddAsync(CategoryRule rule, CancellationToken cancellationToken = default);

    Task DeleteAsync(CategoryRule rule, CancellationToken cancellationToken = default);
}
