using BudgetPrevisionnel.Application.RecurringIncomes;
using BudgetPrevisionnel.Domain.Entities;

namespace BudgetPrevisionnel.Application.Tests.RecurringIncomes;

internal sealed class FakeRecurringIncomeRepository : IRecurringIncomeRepository
{
    private readonly List<RecurringIncome> _incomes = [];
    private int _nextId = 1;

    public Task<RecurringIncome?> GetByIdForUserAsync(int userId, int recurringIncomeId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_incomes.SingleOrDefault(e => e.Id == recurringIncomeId && e.UserId == userId));

    public Task<IReadOnlyList<RecurringIncome>> GetAllForUserAsync(int userId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<RecurringIncome>>(_incomes.Where(e => e.UserId == userId).ToList());

    public Task AddAsync(RecurringIncome recurringIncome, CancellationToken cancellationToken = default)
    {
        recurringIncome.Id = _nextId++;
        _incomes.Add(recurringIncome);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(RecurringIncome recurringIncome, CancellationToken cancellationToken = default)
    {
        _incomes.RemoveAll(e => e.Id == recurringIncome.Id);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
