using BudgetPrevisionnel.Application.Common;
using BudgetPrevisionnel.Domain.Entities;

namespace BudgetPrevisionnel.Application.Categories;

public sealed class CategoryRuleService(ICategoryRuleRepository ruleRepository, ICategoryRepository categoryRepository)
{
    public Task<IReadOnlyList<CategoryRule>> GetForUserAsync(int userId, CancellationToken cancellationToken = default) =>
        ruleRepository.GetByOwnerOrderedByPriorityAsync(userId, cancellationToken);

    public async Task<CategoryRule> CreateAsync(
        int userId, string matchPattern, int categoryId, int priority, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(matchPattern))
        {
            throw new InvalidCategoryRuleException("Match pattern cannot be empty.");
        }

        var category = await categoryRepository.GetByIdAsync(categoryId, cancellationToken);
        if (category is null || (!category.IsSystemDefault && category.OwnerId != userId))
        {
            throw new InvalidReferenceException(nameof(Category), categoryId);
        }

        var rule = new CategoryRule
        {
            OwnerId = userId,
            MatchPattern = matchPattern.Trim(),
            CategoryId = categoryId,
            Priority = priority
        };

        await ruleRepository.AddAsync(rule, cancellationToken);
        return rule;
    }

    public async Task DeleteAsync(int userId, int ruleId, CancellationToken cancellationToken = default)
    {
        var rule = await ruleRepository.GetByIdAsync(ruleId, cancellationToken);

        // Same exception for "doesn't exist" and "belongs to someone else" - don't leak
        // which rule ids exist for other users.
        if (rule is null || rule.OwnerId != userId)
        {
            throw new CategoryRuleNotFoundException(ruleId);
        }

        await ruleRepository.DeleteAsync(rule, cancellationToken);
    }
}
