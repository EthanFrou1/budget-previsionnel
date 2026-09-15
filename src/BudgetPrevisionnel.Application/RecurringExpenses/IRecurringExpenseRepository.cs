using BudgetPrevisionnel.Domain.Entities;

namespace BudgetPrevisionnel.Application.RecurringExpenses;

public interface IRecurringExpenseRepository
{
    Task<RecurringExpense?> GetByIdForUserAsync(int userId, int recurringExpenseId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RecurringExpense>> GetAllForUserAsync(int userId, CancellationToken cancellationToken = default);

    Task AddAsync(RecurringExpense recurringExpense, CancellationToken cancellationToken = default);

    Task DeleteAsync(RecurringExpense recurringExpense, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
