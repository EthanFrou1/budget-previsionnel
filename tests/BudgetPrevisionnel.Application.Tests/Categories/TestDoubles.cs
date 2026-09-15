using BudgetPrevisionnel.Application.Categories;
using BudgetPrevisionnel.Domain.Entities;

namespace BudgetPrevisionnel.Application.Tests.Categories;

internal sealed class FakeCategoryRepository : ICategoryRepository
{
    private readonly List<Category> _categories = [];
    private int _nextId = 1;

    public FakeCategoryRepository Seed(int id, string name, int? parentCategoryId = null, int? ownerId = null)
    {
        _categories.Add(new Category
        {
            Id = id,
            Name = name,
            IsSystemDefault = ownerId is null,
            OwnerId = ownerId,
            ParentCategoryId = parentCategoryId
        });
        _nextId = Math.Max(_nextId, id + 1);
        return this;
    }

    public IReadOnlyList<Category> All => _categories;

    public Task<Category?> FindSystemCategoryByNameAsync(string name, CancellationToken cancellationToken = default) =>
        Task.FromResult(_categories.SingleOrDefault(c => c.IsSystemDefault && c.Name == name));

    public Task<Category?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_categories.SingleOrDefault(c => c.Id == id));

    public Task<IReadOnlyList<Category>> GetVisibleToUserAsync(int userId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Category>>(
            _categories.Where(c => c.IsSystemDefault || c.OwnerId == userId).OrderBy(c => c.Name).ToList());

    public Task<bool> HasSubCategoriesAsync(int categoryId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_categories.Any(c => c.ParentCategoryId == categoryId));

    public Task AddAsync(Category category, CancellationToken cancellationToken = default)
    {
        category.Id = _nextId++;
        _categories.Add(category);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Category category, CancellationToken cancellationToken = default)
    {
        _categories.RemoveAll(c => c.Id == category.Id);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

internal sealed class FakeCategoryRuleRepository : ICategoryRuleRepository
{
    private readonly List<CategoryRule> _rules = [];
    private int _nextId = 1;

    public FakeCategoryRuleRepository Seed(int ownerId, string matchPattern, int categoryId, int priority)
    {
        _rules.Add(new CategoryRule
        {
            Id = _nextId++,
            OwnerId = ownerId,
            MatchPattern = matchPattern,
            CategoryId = categoryId,
            Priority = priority
        });
        return this;
    }

    public Task<IReadOnlyList<CategoryRule>> GetByOwnerOrderedByPriorityAsync(int ownerId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<CategoryRule>>(
            _rules.Where(r => r.OwnerId == ownerId).OrderBy(r => r.Priority).ToList());

    public Task<CategoryRule?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_rules.SingleOrDefault(r => r.Id == id));

    public Task AddAsync(CategoryRule rule, CancellationToken cancellationToken = default)
    {
        rule.Id = _nextId++;
        _rules.Add(rule);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(CategoryRule rule, CancellationToken cancellationToken = default)
    {
        _rules.RemoveAll(r => r.Id == rule.Id);
        return Task.CompletedTask;
    }
}
