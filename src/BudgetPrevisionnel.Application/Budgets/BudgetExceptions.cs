using BudgetPrevisionnel.Application.Common;

namespace BudgetPrevisionnel.Application.Budgets;

public sealed class BudgetNotFoundException(int budgetId)
    : NotFoundException($"Budget {budgetId} was not found.");

public sealed class InvalidBudgetException(string message) : ValidationException(message);

public sealed class DuplicateBudgetException(DateOnly month, int categoryId)
    : ConflictException($"A budget for category {categoryId} in {month:yyyy-MM} already exists.");
