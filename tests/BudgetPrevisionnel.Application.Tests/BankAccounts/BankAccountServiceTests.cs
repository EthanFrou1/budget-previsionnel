using BudgetPrevisionnel.Application.BankAccounts;

namespace BudgetPrevisionnel.Application.Tests.BankAccounts;

public class BankAccountServiceTests
{
    [Fact]
    public async Task CreateAsync_SetsOwnerToCurrentUser()
    {
        var repository = new FakeBankAccountRepository();
        var service = new BankAccountService(repository);

        var account = await service.CreateAsync(userId: 1, "BoursoBank", "Compte courant", iban: null);

        Assert.Equal(1, account.UserId);
        Assert.Equal("Compte courant", account.Label);
    }

    [Fact]
    public async Task UpdateAsync_OwnAccount_ChangesFields()
    {
        var repository = new FakeBankAccountRepository();
        var account = repository.Add(userId: 1, bankName: "BoursoBank", label: "Ancien nom");
        var service = new BankAccountService(repository);

        var updated = await service.UpdateAsync(userId: 1, account.Id, "BoursoBank", "Nouveau nom", "FR7600000000000000000000000");

        Assert.Equal("Nouveau nom", updated.Label);
        Assert.Equal("FR7600000000000000000000000", updated.Iban);
    }

    [Fact]
    public async Task UpdateAsync_AnotherUsersAccount_ThrowsBankAccountNotFound()
    {
        var repository = new FakeBankAccountRepository();
        var account = repository.Add(userId: 2, bankName: "BoursoBank", label: "Pas à moi");
        var service = new BankAccountService(repository);

        await Assert.ThrowsAsync<BankAccountNotFoundException>(
            () => service.UpdateAsync(userId: 1, account.Id, "BoursoBank", "Nouveau nom", null));
    }

    [Fact]
    public async Task DeleteAsync_OwnAccount_Removes()
    {
        var repository = new FakeBankAccountRepository();
        var account = repository.Add(userId: 1, bankName: "BoursoBank", label: "Compte courant");
        var service = new BankAccountService(repository);

        await service.DeleteAsync(userId: 1, account.Id);

        Assert.Empty(await repository.GetAllForUserAsync(1));
    }

    [Fact]
    public async Task DeleteAsync_AnotherUsersAccount_ThrowsBankAccountNotFound()
    {
        var repository = new FakeBankAccountRepository();
        var account = repository.Add(userId: 2, bankName: "BoursoBank", label: "Pas à moi");
        var service = new BankAccountService(repository);

        await Assert.ThrowsAsync<BankAccountNotFoundException>(
            () => service.DeleteAsync(userId: 1, account.Id));
    }
}
