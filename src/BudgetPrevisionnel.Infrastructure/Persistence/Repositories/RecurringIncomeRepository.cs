using BudgetPrevisionnel.Application.RecurringIncomes;
using BudgetPrevisionnel.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BudgetPrevisionnel.Infrastructure.Persistence.Repositories;

public sealed class RecurringIncomeRepository(BudgetDbContext dbContext) : IRecurringIncomeRepository
{
    public Task<RecurringIncome?> GetByIdForUserAsync(int userId, int recurringIncomeId, CancellationToken cancellationToken = default) =>
        dbContext.RecurringIncomes
            .Include(e => e.Category)
            .SingleOrDefaultAsync(e => e.Id == recurringIncomeId && e.UserId == userId, cancellationToken);

    public async Task<IReadOnlyList<RecurringIncome>> GetAllForUserAsync(int userId, CancellationToken cancellationToken = default) =>
        await dbContext.RecurringIncomes
            .Include(e => e.Category)
            .Where(e => e.UserId == userId)
            .OrderBy(e => e.Label)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(RecurringIncome recurringIncome, CancellationToken cancellationToken = default)
    {
        dbContext.RecurringIncomes.Add(recurringIncome);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(RecurringIncome recurringIncome, CancellationToken cancellationToken = default)
    {
        dbContext.RecurringIncomes.Remove(recurringIncome);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
