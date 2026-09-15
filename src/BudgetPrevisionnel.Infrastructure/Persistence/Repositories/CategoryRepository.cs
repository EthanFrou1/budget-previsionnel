using BudgetPrevisionnel.Application.Categories;
using BudgetPrevisionnel.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BudgetPrevisionnel.Infrastructure.Persistence.Repositories;

public sealed class CategoryRepository(BudgetDbContext dbContext) : ICategoryRepository
{
    public Task<Category?> FindSystemCategoryByNameAsync(string name, CancellationToken cancellationToken = default) =>
        dbContext.Categories.SingleOrDefaultAsync(c => c.IsSystemDefault && c.Name == name, cancellationToken);

    public Task<Category?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        dbContext.Categories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Category>> GetVisibleToUserAsync(int userId, CancellationToken cancellationToken = default) =>
        await dbContext.Categories
            .Where(c => c.IsSystemDefault || c.OwnerId == userId)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

    public Task<bool> HasSubCategoriesAsync(int categoryId, CancellationToken cancellationToken = default) =>
        dbContext.Categories.AnyAsync(c => c.ParentCategoryId == categoryId, cancellationToken);

    public async Task AddAsync(Category category, CancellationToken cancellationToken = default)
    {
        dbContext.Categories.Add(category);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Category category, CancellationToken cancellationToken = default)
    {
        dbContext.Categories.Remove(category);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
