using BudgetPrevisionnel.Domain.Entities;

namespace BudgetPrevisionnel.Application.BankAccounts;

public sealed class BankAccountService(IBankAccountRepository bankAccountRepository)
{
    public Task<IReadOnlyList<BankAccount>> GetAllForUserAsync(int userId, CancellationToken cancellationToken = default) =>
        bankAccountRepository.GetAllForUserAsync(userId, cancellationToken);

    public async Task<BankAccount> CreateAsync(
        int userId, string bankName, string label, string? iban, CancellationToken cancellationToken = default)
    {
        var account = new BankAccount { UserId = userId, BankName = bankName, Label = label, Iban = iban };
        await bankAccountRepository.AddAsync(account, cancellationToken);
        return account;
    }

    public async Task<BankAccount> UpdateAsync(
        int userId, int bankAccountId, string bankName, string label, string? iban,
        CancellationToken cancellationToken = default)
    {
        var account = await bankAccountRepository.GetByIdForUserAsync(userId, bankAccountId, cancellationToken)
            ?? throw new BankAccountNotFoundException(bankAccountId);

        account.BankName = bankName;
        account.Label = label;
        account.Iban = iban;

        await bankAccountRepository.SaveChangesAsync(cancellationToken);
        return account;
    }

    public async Task DeleteAsync(int userId, int bankAccountId, CancellationToken cancellationToken = default)
    {
        var account = await bankAccountRepository.GetByIdForUserAsync(userId, bankAccountId, cancellationToken)
            ?? throw new BankAccountNotFoundException(bankAccountId);

        // Cascades: all of this account's Transactions are deleted with it (Lot 0 schema
        // decision). Any SavingsGoal.LinkedAccountId pointing here is SetNull instead -
        // the goal itself stays meaningful without a linked account.
        await bankAccountRepository.DeleteAsync(account, cancellationToken);
    }
}
