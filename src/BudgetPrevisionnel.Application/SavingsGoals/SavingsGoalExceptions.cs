using BudgetPrevisionnel.Application.Common;

namespace BudgetPrevisionnel.Application.SavingsGoals;

public sealed class SavingsGoalNotFoundException(int savingsGoalId)
    : NotFoundException($"Savings goal {savingsGoalId} was not found.");

public sealed class InvalidSavingsGoalException(string message) : ValidationException(message);
