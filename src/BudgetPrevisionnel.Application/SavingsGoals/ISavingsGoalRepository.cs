using BudgetPrevisionnel.Domain.Entities;

namespace BudgetPrevisionnel.Application.SavingsGoals;

public interface ISavingsGoalRepository
{
    Task<SavingsGoal?> GetByIdForUserAsync(int userId, int savingsGoalId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SavingsGoal>> GetAllForUserAsync(int userId, CancellationToken cancellationToken = default);

    Task AddAsync(SavingsGoal savingsGoal, CancellationToken cancellationToken = default);

    Task DeleteAsync(SavingsGoal savingsGoal, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
