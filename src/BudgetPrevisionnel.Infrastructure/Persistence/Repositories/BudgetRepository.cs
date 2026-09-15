using BudgetPrevisionnel.Application.Budgets;
using BudgetPrevisionnel.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BudgetPrevisionnel.Infrastructure.Persistence.Repositories;

public sealed class BudgetRepository(BudgetDbContext dbContext) : IBudgetRepository
{
    public Task<Budget?> GetByIdForUserAsync(int userId, int budgetId, CancellationToken cancellationToken = default) =>
        dbContext.Budgets
            .Include(b => b.Category)
            .SingleOrDefaultAsync(b => b.Id == budgetId && b.UserId == userId, cancellationToken);

    public Task<Budget?> FindAsync(int userId, DateOnly month, int categoryId, CancellationToken cancellationToken = default) =>
        dbContext.Budgets
            .SingleOrDefaultAsync(b => b.UserId == userId && b.Month == month && b.CategoryId == categoryId, cancellationToken);

    public async Task<IReadOnlyList<Budget>> GetForMonthAsync(int userId, DateOnly month, CancellationToken cancellationToken = default) =>
        await dbContext.Budgets
            .Include(b => b.Category)
            .Where(b => b.UserId == userId && b.Month == month)
            .OrderBy(b => b.Category.Name)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(Budget budget, CancellationToken cancellationToken = default)
    {
        dbContext.Budgets.Add(budget);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Budget budget, CancellationToken cancellationToken = default)
    {
        dbContext.Budgets.Remove(budget);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
