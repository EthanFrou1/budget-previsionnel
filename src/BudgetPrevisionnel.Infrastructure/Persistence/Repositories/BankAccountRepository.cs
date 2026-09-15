using BudgetPrevisionnel.Application.BankAccounts;
using BudgetPrevisionnel.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BudgetPrevisionnel.Infrastructure.Persistence.Repositories;

public sealed class BankAccountRepository(BudgetDbContext dbContext) : IBankAccountRepository
{
    public Task<BankAccount?> GetByIdForUserAsync(int userId, int bankAccountId, CancellationToken cancellationToken = default) =>
        dbContext.BankAccounts.SingleOrDefaultAsync(a => a.Id == bankAccountId && a.UserId == userId, cancellationToken);

    public async Task<IReadOnlyList<BankAccount>> GetAllForUserAsync(int userId, CancellationToken cancellationToken = default) =>
        await dbContext.BankAccounts
            .Where(a => a.UserId == userId)
            .OrderBy(a => a.Label)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(BankAccount account, CancellationToken cancellationToken = default)
    {
        dbContext.BankAccounts.Add(account);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(BankAccount account, CancellationToken cancellationToken = default)
    {
        dbContext.BankAccounts.Remove(account);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
