using BudgetPrevisionnel.Application.BankAccounts;
using BudgetPrevisionnel.Domain.Entities;

namespace BudgetPrevisionnel.Application.Tests.BankAccounts;

internal sealed class FakeBankAccountRepository : IBankAccountRepository
{
    private readonly List<BankAccount> _accounts = [];
    private int _nextId = 1;

    public BankAccount Add(int userId, string bankName, string label)
    {
        var account = new BankAccount { Id = _nextId++, UserId = userId, BankName = bankName, Label = label };
        _accounts.Add(account);
        return account;
    }

    public int GetUserId(int bankAccountId) => _accounts.Single(a => a.Id == bankAccountId).UserId;

    public Task<BankAccount?> GetByIdForUserAsync(int userId, int bankAccountId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_accounts.SingleOrDefault(a => a.Id == bankAccountId && a.UserId == userId));

    public Task<IReadOnlyList<BankAccount>> GetAllForUserAsync(int userId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<BankAccount>>(_accounts.Where(a => a.UserId == userId).ToList());

    public Task AddAsync(BankAccount account, CancellationToken cancellationToken = default)
    {
        account.Id = _nextId++;
        _accounts.Add(account);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(BankAccount account, CancellationToken cancellationToken = default)
    {
        _accounts.RemoveAll(a => a.Id == account.Id);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
