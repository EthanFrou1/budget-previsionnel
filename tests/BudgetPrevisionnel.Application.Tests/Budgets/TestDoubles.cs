using BudgetPrevisionnel.Application.Budgets;
using BudgetPrevisionnel.Domain.Entities;

namespace BudgetPrevisionnel.Application.Tests.Budgets;

internal sealed class FakeBudgetRepository : IBudgetRepository
{
    private readonly List<Budget> _budgets = [];
    private int _nextId = 1;

    public Task<Budget?> GetByIdForUserAsync(int userId, int budgetId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_budgets.SingleOrDefault(b => b.Id == budgetId && b.UserId == userId));

    public Task<Budget?> FindAsync(int userId, DateOnly month, int categoryId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_budgets.SingleOrDefault(b => b.UserId == userId && b.Month == month && b.CategoryId == categoryId));

    public Task<IReadOnlyList<Budget>> GetForMonthAsync(int userId, DateOnly month, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Budget>>(
            _budgets.Where(b => b.UserId == userId && b.Month == month).ToList());

    public Task AddAsync(Budget budget, CancellationToken cancellationToken = default)
    {
        budget.Id = _nextId++;
        _budgets.Add(budget);
        return Task.CompletedTask;
    }

    /// <summary>For tests that need a Budget with its Category navigation already set,
    /// bypassing the category-visibility checks BudgetService would normally run.</summary>
    public Budget Seed(int userId, DateOnly month, Category category, decimal plannedAmount)
    {
        var budget = new Budget
        {
            Id = _nextId++,
            UserId = userId,
            Month = month,
            CategoryId = category.Id,
            Category = category,
            PlannedAmount = plannedAmount
        };
        _budgets.Add(budget);
        return budget;
    }

    public Task DeleteAsync(Budget budget, CancellationToken cancellationToken = default)
    {
        _budgets.RemoveAll(b => b.Id == budget.Id);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
