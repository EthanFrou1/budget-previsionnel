using BudgetPrevisionnel.Application.SavingsGoals;
using BudgetPrevisionnel.Domain.Entities;

namespace BudgetPrevisionnel.Application.Tests.SavingsGoals;

internal sealed class FakeSavingsGoalRepository : ISavingsGoalRepository
{
    private readonly List<SavingsGoal> _goals = [];
    private int _nextId = 1;

    public Task<SavingsGoal?> GetByIdForUserAsync(int userId, int savingsGoalId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_goals.SingleOrDefault(g => g.Id == savingsGoalId && g.UserId == userId));

    public Task<IReadOnlyList<SavingsGoal>> GetAllForUserAsync(int userId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<SavingsGoal>>(_goals.Where(g => g.UserId == userId).ToList());

    public Task AddAsync(SavingsGoal savingsGoal, CancellationToken cancellationToken = default)
    {
        savingsGoal.Id = _nextId++;
        _goals.Add(savingsGoal);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(SavingsGoal savingsGoal, CancellationToken cancellationToken = default)
    {
        _goals.RemoveAll(g => g.Id == savingsGoal.Id);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
