using BudgetPrevisionnel.Application.Common;

namespace BudgetPrevisionnel.Application.Categories;

/// <summary>Only for Category as the PRIMARY resource (PUT/DELETE /api/categories/{id}).
/// An invalid category reference inside another request (ParentCategoryId, a
/// RecurringExpense/CategoryRule/Budget's CategoryId) throws InvalidReferenceException
/// instead - see its doc comment.</summary>
public sealed class CategoryNotFoundException(int categoryId)
    : NotFoundException($"Category {categoryId} was not found.");

public sealed class CategoryNotEditableException(int categoryId)
    : ForbiddenException($"Category {categoryId} is a system category or not owned by the current user.");

public sealed class CategoryHasSubCategoriesException(int categoryId)
    : ConflictException($"Category {categoryId} has sub-categories; delete those first.");

public sealed class InvalidCategoryParentException(string message) : ValidationException(message);

public sealed class CategoryRuleNotFoundException(int ruleId)
    : NotFoundException($"Category rule {ruleId} was not found.");

public sealed class InvalidCategoryRuleException(string message) : ValidationException(message);
