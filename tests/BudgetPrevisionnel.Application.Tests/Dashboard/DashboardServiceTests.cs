using BudgetPrevisionnel.Application.BankAccounts;
using BudgetPrevisionnel.Application.Dashboard;
using BudgetPrevisionnel.Application.Tests.BankAccounts;

namespace BudgetPrevisionnel.Application.Tests.Dashboard;

public class DashboardServiceTests
{
    [Fact]
    public async Task GetBalanceEvolutionAsync_ComputesRunningCumulativeTotal()
    {
        var repository = new FakeDashboardRepository();
        repository.DailyNetChangesWithoutTransfers.AddRange(
        [
            new DailyNetChange(new DateOnly(2026, 9, 1), -50m),
            new DailyNetChange(new DateOnly(2026, 9, 2), 200m),
            new DailyNetChange(new DateOnly(2026, 9, 3), -30m)
        ]);
        var service = new DashboardService(repository, new FakeBankAccountRepository());

        var points = await service.GetBalanceEvolutionAsync(
            1, bankAccountId: null, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), startingBalance: 1000m);

        Assert.Equal([1000m + -50m, 1000m + -50m + 200m, 1000m + -50m + 200m + -30m], points.Select(p => p.CumulativeBalance));
    }

    [Fact]
    public async Task GetBalanceEvolutionAsync_NoStartingBalance_DefaultsToZero()
    {
        var repository = new FakeDashboardRepository();
        repository.DailyNetChangesWithoutTransfers.Add(new DailyNetChange(new DateOnly(2026, 9, 1), 100m));
        var service = new DashboardService(repository, new FakeBankAccountRepository());

        var points = await service.GetBalanceEvolutionAsync(
            1, null, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), startingBalance: 0m);

        Assert.Equal(100m, Assert.Single(points).CumulativeBalance);
    }

    [Fact]
    public async Task GetBalanceEvolutionAsync_ConsolidatedView_ExcludesInternalTransfers()
    {
        var repository = new FakeDashboardRepository();
        var accounts = new FakeBankAccountRepository();
        var service = new DashboardService(repository, accounts);

        await service.GetBalanceEvolutionAsync(1, bankAccountId: null, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), 0m);

        Assert.False(repository.LastIncludeInternalTransfersRequested);
    }

    [Fact]
    public async Task GetBalanceEvolutionAsync_PerAccountView_IncludesInternalTransfers()
    {
        var repository = new FakeDashboardRepository();
        var accounts = new FakeBankAccountRepository();
        var account = accounts.Add(1, "BoursoBank", "Compte courant");
        var service = new DashboardService(repository, accounts);

        await service.GetBalanceEvolutionAsync(1, bankAccountId: account.Id, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), 0m);

        Assert.True(repository.LastIncludeInternalTransfersRequested);
    }

    [Fact]
    public async Task GetBalanceEvolutionAsync_AnotherUsersAccount_ThrowsBankAccountNotFound()
    {
        var accounts = new FakeBankAccountRepository();
        var notMine = accounts.Add(2, "BoursoBank", "Pas à moi");
        var service = new DashboardService(new FakeDashboardRepository(), accounts);

        await Assert.ThrowsAsync<BankAccountNotFoundException>(() => service.GetBalanceEvolutionAsync(
            1, notMine.Id, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), 0m));
    }

    [Fact]
    public async Task GetCategoryBreakdownAsync_AnotherUsersAccount_ThrowsBankAccountNotFound()
    {
        var accounts = new FakeBankAccountRepository();
        var notMine = accounts.Add(2, "BoursoBank", "Pas à moi");
        var service = new DashboardService(new FakeDashboardRepository(), accounts);

        await Assert.ThrowsAsync<BankAccountNotFoundException>(() => service.GetCategoryBreakdownAsync(
            1, notMine.Id, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30)));
    }

    [Fact]
    public async Task GetMonthlyComparisonAsync_AlwaysReturnsTwelveMonths_ZeroFilledWhenMissing()
    {
        var repository = new FakeDashboardRepository();
        repository.MonthlyTotals.Add(new MonthlyComparisonEntry(new DateOnly(2026, 3, 1), 2000m, 1500m, 500m));
        var service = new DashboardService(repository, new FakeBankAccountRepository());

        var months = await service.GetMonthlyComparisonAsync(1, null, 2026);

        Assert.Equal(12, months.Count);
        Assert.Equal(new DateOnly(2026, 1, 1), months[0].Month);
        Assert.Equal(0m, months[0].Income);
        Assert.Equal(new DateOnly(2026, 3, 1), months[2].Month);
        Assert.Equal(2000m, months[2].Income);
        Assert.Equal(new DateOnly(2026, 12, 1), months[11].Month);
    }

    [Fact]
    public async Task GetMonthlyComparisonAsync_AnotherUsersAccount_ThrowsBankAccountNotFound()
    {
        var accounts = new FakeBankAccountRepository();
        var notMine = accounts.Add(2, "BoursoBank", "Pas à moi");
        var service = new DashboardService(new FakeDashboardRepository(), accounts);

        await Assert.ThrowsAsync<BankAccountNotFoundException>(() => service.GetMonthlyComparisonAsync(1, notMine.Id, 2026));
    }
}
