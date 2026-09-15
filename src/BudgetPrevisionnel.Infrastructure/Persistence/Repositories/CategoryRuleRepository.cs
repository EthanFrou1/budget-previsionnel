using BudgetPrevisionnel.Application.Categories;
using BudgetPrevisionnel.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BudgetPrevisionnel.Infrastructure.Persistence.Repositories;

public sealed class CategoryRuleRepository(BudgetDbContext dbContext) : ICategoryRuleRepository
{
    public async Task<IReadOnlyList<CategoryRule>> GetByOwnerOrderedByPriorityAsync(
        int ownerId, CancellationToken cancellationToken = default) =>
        await dbContext.CategoryRules
            .Where(r => r.OwnerId == ownerId)
            .OrderBy(r => r.Priority)
            .ToListAsync(cancellationToken);

    public Task<CategoryRule?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        dbContext.CategoryRules.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task AddAsync(CategoryRule rule, CancellationToken cancellationToken = default)
    {
        dbContext.CategoryRules.Add(rule);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(CategoryRule rule, CancellationToken cancellationToken = default)
    {
        dbContext.CategoryRules.Remove(rule);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
