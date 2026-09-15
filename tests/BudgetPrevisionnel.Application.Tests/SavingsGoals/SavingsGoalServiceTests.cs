using BudgetPrevisionnel.Application.Common;
using BudgetPrevisionnel.Application.SavingsGoals;
using BudgetPrevisionnel.Application.Tests.BankAccounts;

namespace BudgetPrevisionnel.Application.Tests.SavingsGoals;

public class SavingsGoalServiceTests
{
    private static SavingsGoalService CreateService(FakeBankAccountRepository? accounts = null) =>
        new(new FakeSavingsGoalRepository(), accounts ?? new FakeBankAccountRepository());

    [Fact]
    public async Task CreateAsync_ValidGoal_Persists()
    {
        var service = CreateService();

        var goal = await service.CreateAsync(1, "Vacances", targetAmount: 2000m, currentAmount: 500m, null, null);

        Assert.Equal("Vacances", goal.Label);
        Assert.Equal(1, goal.UserId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public async Task CreateAsync_TargetAmountNotPositive_Throws(decimal targetAmount)
    {
        var service = CreateService();

        await Assert.ThrowsAsync<InvalidSavingsGoalException>(
            () => service.CreateAsync(1, "Vacances", targetAmount, 0m, null, null));
    }

    [Fact]
    public async Task CreateAsync_NegativeCurrentAmount_Throws()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<InvalidSavingsGoalException>(
            () => service.CreateAsync(1, "Vacances", 2000m, currentAmount: -1m, null, null));
    }

    [Fact]
    public async Task CreateAsync_CurrentAmountExceedingTarget_IsAllowed()
    {
        // Overshooting a goal is a legitimate state, not an error.
        var service = CreateService();

        var goal = await service.CreateAsync(1, "Vacances", targetAmount: 1000m, currentAmount: 1500m, null, null);

        Assert.Equal(1500m, goal.CurrentAmount);
    }

    [Fact]
    public async Task CreateAsync_LinkedAccountBelongsToAnotherUser_ThrowsInvalidReference()
    {
        var accounts = new FakeBankAccountRepository();
        var theirs = accounts.Add(userId: 2, bankName: "BoursoBank", label: "Pas à moi");
        var service = CreateService(accounts);

        await Assert.ThrowsAsync<InvalidReferenceException>(
            () => service.CreateAsync(1, "Vacances", 2000m, 0m, null, theirs.Id));
    }

    [Fact]
    public async Task UpdateAsync_AnotherUsersGoal_ThrowsSavingsGoalNotFound()
    {
        var repository = new FakeSavingsGoalRepository();
        var service = new SavingsGoalService(repository, new FakeBankAccountRepository());
        var goal = await service.CreateAsync(2, "Pas à moi", 2000m, 0m, null, null);

        await Assert.ThrowsAsync<SavingsGoalNotFoundException>(
            () => service.UpdateAsync(1, goal.Id, "Modifié", 2000m, 0m, null, null));
    }

    [Fact]
    public async Task DeleteAsync_OwnGoal_Removes()
    {
        var service = CreateService();
        var goal = await service.CreateAsync(1, "Vacances", 2000m, 0m, null, null);

        await service.DeleteAsync(1, goal.Id);

        Assert.Empty(await service.GetAllForUserAsync(1));
    }
}
