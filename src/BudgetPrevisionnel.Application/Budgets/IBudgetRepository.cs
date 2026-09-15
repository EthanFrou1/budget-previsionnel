using BudgetPrevisionnel.Domain.Entities;

namespace BudgetPrevisionnel.Application.Budgets;

public interface IBudgetRepository
{
    Task<Budget?> GetByIdForUserAsync(int userId, int budgetId, CancellationToken cancellationToken = default);

    Task<Budget?> FindAsync(int userId, DateOnly month, int categoryId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Budget>> GetForMonthAsync(int userId, DateOnly month, CancellationToken cancellationToken = default);

    Task AddAsync(Budget budget, CancellationToken cancellationToken = default);

    Task DeleteAsync(Budget budget, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
