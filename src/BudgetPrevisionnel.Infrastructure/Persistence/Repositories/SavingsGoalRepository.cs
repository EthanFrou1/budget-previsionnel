using BudgetPrevisionnel.Application.SavingsGoals;
using BudgetPrevisionnel.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BudgetPrevisionnel.Infrastructure.Persistence.Repositories;

public sealed class SavingsGoalRepository(BudgetDbContext dbContext) : ISavingsGoalRepository
{
    public Task<SavingsGoal?> GetByIdForUserAsync(int userId, int savingsGoalId, CancellationToken cancellationToken = default) =>
        dbContext.SavingsGoals.SingleOrDefaultAsync(g => g.Id == savingsGoalId && g.UserId == userId, cancellationToken);

    public async Task<IReadOnlyList<SavingsGoal>> GetAllForUserAsync(int userId, CancellationToken cancellationToken = default) =>
        await dbContext.SavingsGoals
            .Where(g => g.UserId == userId)
            .OrderBy(g => g.Label)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(SavingsGoal savingsGoal, CancellationToken cancellationToken = default)
    {
        dbContext.SavingsGoals.Add(savingsGoal);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(SavingsGoal savingsGoal, CancellationToken cancellationToken = default)
    {
        dbContext.SavingsGoals.Remove(savingsGoal);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
