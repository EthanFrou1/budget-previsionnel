using BudgetPrevisionnel.Domain.Entities;

namespace BudgetPrevisionnel.Application.RecurringIncomes;

public interface IRecurringIncomeRepository
{
    Task<RecurringIncome?> GetByIdForUserAsync(int userId, int recurringIncomeId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RecurringIncome>> GetAllForUserAsync(int userId, CancellationToken cancellationToken = default);

    Task AddAsync(RecurringIncome recurringIncome, CancellationToken cancellationToken = default);

    Task DeleteAsync(RecurringIncome recurringIncome, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
