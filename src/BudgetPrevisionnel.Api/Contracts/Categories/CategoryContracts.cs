using BudgetPrevisionnel.Domain.Entities;

namespace BudgetPrevisionnel.Api.Contracts.Categories;

public sealed record CreateCategoryRequest(string Name, string? Icon, string? Color, int? ParentCategoryId);

public sealed record UpdateCategoryRequest(string Name, string? Icon, string? Color, int? ParentCategoryId);

public sealed record CategoryResponse(
    int Id, string Name, string? Icon, string? Color, int? ParentCategoryId, bool IsSystemDefault, bool IsOwnedByCurrentUser)
{
    public static CategoryResponse FromEntity(Category category, int currentUserId) => new(
        category.Id, category.Name, category.Icon, category.Color, category.ParentCategoryId,
        category.IsSystemDefault, category.OwnerId == currentUserId);
}

public sealed record CreateCategoryRuleRequest(string MatchPattern, int CategoryId, int Priority);

public sealed record CategoryRuleResponse(int Id, string MatchPattern, int CategoryId, int Priority)
{
    public static CategoryRuleResponse FromEntity(CategoryRule rule) =>
        new(rule.Id, rule.MatchPattern, rule.CategoryId, rule.Priority);
}
