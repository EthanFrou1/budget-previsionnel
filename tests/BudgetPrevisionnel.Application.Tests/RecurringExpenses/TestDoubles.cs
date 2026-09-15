using BudgetPrevisionnel.Application.RecurringExpenses;
using BudgetPrevisionnel.Domain.Entities;

namespace BudgetPrevisionnel.Application.Tests.RecurringExpenses;

internal sealed class FakeRecurringExpenseRepository : IRecurringExpenseRepository
{
    private readonly List<RecurringExpense> _expenses = [];
    private int _nextId = 1;

    public Task<RecurringExpense?> GetByIdForUserAsync(int userId, int recurringExpenseId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_expenses.SingleOrDefault(e => e.Id == recurringExpenseId && e.UserId == userId));

    public Task<IReadOnlyList<RecurringExpense>> GetAllForUserAsync(int userId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<RecurringExpense>>(_expenses.Where(e => e.UserId == userId).ToList());

    public Task AddAsync(RecurringExpense recurringExpense, CancellationToken cancellationToken = default)
    {
        recurringExpense.Id = _nextId++;
        _expenses.Add(recurringExpense);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(RecurringExpense recurringExpense, CancellationToken cancellationToken = default)
    {
        _expenses.RemoveAll(e => e.Id == recurringExpense.Id);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
