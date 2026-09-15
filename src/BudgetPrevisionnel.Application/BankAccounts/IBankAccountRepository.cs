using BudgetPrevisionnel.Domain.Entities;

namespace BudgetPrevisionnel.Application.BankAccounts;

public interface IBankAccountRepository
{
    Task<BankAccount?> GetByIdForUserAsync(int userId, int bankAccountId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BankAccount>> GetAllForUserAsync(int userId, CancellationToken cancellationToken = default);

    Task AddAsync(BankAccount account, CancellationToken cancellationToken = default);

    Task DeleteAsync(BankAccount account, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
