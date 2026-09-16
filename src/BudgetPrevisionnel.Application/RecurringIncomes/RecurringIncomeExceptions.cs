using BudgetPrevisionnel.Application.Common;

namespace BudgetPrevisionnel.Application.RecurringIncomes;

public sealed class RecurringIncomeNotFoundException(int recurringIncomeId)
    : NotFoundException($"Recurring income {recurringIncomeId} was not found.");

public sealed class InvalidRecurringIncomeException(string message) : ValidationException(message);
