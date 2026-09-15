using BudgetPrevisionnel.Application.RecurringExpenses;
using BudgetPrevisionnel.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BudgetPrevisionnel.Infrastructure.Persistence.Repositories;

public sealed class RecurringExpenseRepository(BudgetDbContext dbContext) : IRecurringExpenseRepository
{
    public Task<RecurringExpense?> GetByIdForUserAsync(int userId, int recurringExpenseId, CancellationToken cancellationToken = default) =>
        dbContext.RecurringExpenses
            .Include(e => e.Category)
            .SingleOrDefaultAsync(e => e.Id == recurringExpenseId && e.UserId == userId, cancellationToken);

    public async Task<IReadOnlyList<RecurringExpense>> GetAllForUserAsync(int userId, CancellationToken cancellationToken = default) =>
        await dbContext.RecurringExpenses
            .Include(e => e.Category)
            .Where(e => e.UserId == userId)
            .OrderBy(e => e.Label)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(RecurringExpense recurringExpense, CancellationToken cancellationToken = default)
    {
        dbContext.RecurringExpenses.Add(recurringExpense);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(RecurringExpense recurringExpense, CancellationToken cancellationToken = default)
    {
        dbContext.RecurringExpenses.Remove(recurringExpense);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
