using BudgetPrevisionnel.Application.Common;

namespace BudgetPrevisionnel.Application.RecurringExpenses;

public sealed class RecurringExpenseNotFoundException(int recurringExpenseId)
    : NotFoundException($"Recurring expense {recurringExpenseId} was not found.");

public sealed class InvalidRecurringExpenseException(string message) : ValidationException(message);
