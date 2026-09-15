using BudgetPrevisionnel.Application.Budgets;
using BudgetPrevisionnel.Application.Common;
using BudgetPrevisionnel.Application.Tests.BankAccounts;
using BudgetPrevisionnel.Application.Tests.Categories;
using BudgetPrevisionnel.Application.Tests.Transactions;
using BudgetPrevisionnel.Domain.Entities;

namespace BudgetPrevisionnel.Application.Tests.Budgets;

public class BudgetServiceTests
{
    private static (FakeBankAccountRepository Accounts, FakeTransactionRepository Transactions, FakeCategoryRepository Categories, BudgetService Service)
        CreateSubject()
    {
        var accounts = new FakeBankAccountRepository();
        var transactions = new FakeTransactionRepository(accounts);
        var categories = new FakeCategoryRepository().Seed(id: 1, name: "Alimentation");
        var service = new BudgetService(new FakeBudgetRepository(), categories, transactions);
        return (accounts, transactions, categories, service);
    }

    [Fact]
    public async Task CreateAsync_ValidBudget_Persists()
    {
        var (_, _, _, service) = CreateSubject();

        var line = await service.CreateAsync(1, new DateOnly(2026, 9, 15), categoryId: 1, plannedAmount: 300m);

        Assert.Equal(300m, line.Budget.PlannedAmount);
        Assert.Equal(new DateOnly(2026, 9, 1), line.Budget.Month); // normalized to first of month
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    public async Task CreateAsync_PlannedAmountNotPositive_Throws(decimal plannedAmount)
    {
        var (_, _, _, service) = CreateSubject();

        await Assert.ThrowsAsync<InvalidBudgetException>(
            () => service.CreateAsync(1, new DateOnly(2026, 9, 1), 1, plannedAmount));
    }

    [Fact]
    public async Task CreateAsync_UnknownCategory_ThrowsInvalidReference()
    {
        var (_, _, _, service) = CreateSubject();

        await Assert.ThrowsAsync<InvalidReferenceException>(
            () => service.CreateAsync(1, new DateOnly(2026, 9, 1), categoryId: 999, plannedAmount: 300m));
    }

    [Fact]
    public async Task CreateAsync_DuplicateForSameMonthAndCategory_ThrowsDuplicateBudget()
    {
        var (_, _, _, service) = CreateSubject();
        await service.CreateAsync(1, new DateOnly(2026, 9, 1), 1, 300m);

        // Different day within the same month - must still collide after normalization.
        await Assert.ThrowsAsync<DuplicateBudgetException>(
            () => service.CreateAsync(1, new DateOnly(2026, 9, 28), 1, 350m));
    }

    [Fact]
    public async Task CreateAsync_SameCategoryDifferentMonth_IsAllowed()
    {
        var (_, _, _, service) = CreateSubject();
        await service.CreateAsync(1, new DateOnly(2026, 9, 1), 1, 300m);

        var line = await service.CreateAsync(1, new DateOnly(2026, 10, 1), 1, 300m);

        Assert.Equal(new DateOnly(2026, 10, 1), line.Budget.Month);
    }

    [Fact]
    public async Task GetForMonthAsync_ComputesActualAsPositiveSpendFromNegativeTransactions()
    {
        var (accounts, transactions, _, service) = CreateSubject();
        var account = accounts.Add(1, "BoursoBank", "Compte courant");
        await transactions.AddRangeAsync(
        [
            new Transaction { BankAccountId = account.Id, Date = new DateOnly(2026, 9, 5), RawLabel = "Courses", Amount = -60m, CategoryId = 1 },
            new Transaction { BankAccountId = account.Id, Date = new DateOnly(2026, 9, 20), RawLabel = "Courses", Amount = -40m, CategoryId = 1 }
        ]);
        await service.CreateAsync(1, new DateOnly(2026, 9, 1), 1, plannedAmount: 300m);

        var lines = await service.GetForMonthAsync(1, new DateOnly(2026, 9, 1));

        var line = Assert.Single(lines);
        Assert.Equal(300m, line.Budget.PlannedAmount);
        Assert.Equal(100m, line.ActualAmount); // -(-60 + -40) = 100
    }

    [Fact]
    public async Task GetForMonthAsync_TransactionsOutsideMonth_AreExcludedFromActual()
    {
        var (accounts, transactions, _, service) = CreateSubject();
        var account = accounts.Add(1, "BoursoBank", "Compte courant");
        await transactions.AddRangeAsync(
        [
            new Transaction { BankAccountId = account.Id, Date = new DateOnly(2026, 8, 31), RawLabel = "Courses", Amount = -60m, CategoryId = 1 },
            new Transaction { BankAccountId = account.Id, Date = new DateOnly(2026, 10, 1), RawLabel = "Courses", Amount = -60m, CategoryId = 1 }
        ]);
        await service.CreateAsync(1, new DateOnly(2026, 9, 1), 1, plannedAmount: 300m);

        var lines = await service.GetForMonthAsync(1, new DateOnly(2026, 9, 1));

        Assert.Equal(0m, Assert.Single(lines).ActualAmount);
    }

    [Fact]
    public async Task GetForMonthAsync_InternalTransfer_ExcludedFromActual()
    {
        var (accounts, transactions, _, service) = CreateSubject();
        var account = accounts.Add(1, "BoursoBank", "Compte courant");
        await transactions.AddRangeAsync(
        [
            new Transaction { BankAccountId = account.Id, Date = new DateOnly(2026, 9, 5), RawLabel = "Virement", Amount = -200m, CategoryId = 1, IsInternalTransfer = true }
        ]);
        await service.CreateAsync(1, new DateOnly(2026, 9, 1), 1, plannedAmount: 300m);

        var lines = await service.GetForMonthAsync(1, new DateOnly(2026, 9, 1));

        Assert.Equal(0m, Assert.Single(lines).ActualAmount);
    }

    [Fact]
    public async Task UpdateAsync_UnknownBudget_ThrowsBudgetNotFound()
    {
        var (_, _, _, service) = CreateSubject();

        await Assert.ThrowsAsync<BudgetNotFoundException>(() => service.UpdateAsync(1, 999, 300m));
    }

    [Fact]
    public async Task UpdateAsync_ValidChange_Persists()
    {
        var (_, _, _, service) = CreateSubject();
        var created = await service.CreateAsync(1, new DateOnly(2026, 9, 1), 1, 300m);

        var updated = await service.UpdateAsync(1, created.Budget.Id, 400m);

        Assert.Equal(400m, updated.Budget.PlannedAmount);
    }

    [Fact]
    public async Task DeleteAsync_UnknownBudget_ThrowsBudgetNotFound()
    {
        var (_, _, _, service) = CreateSubject();

        await Assert.ThrowsAsync<BudgetNotFoundException>(() => service.DeleteAsync(1, 999));
    }
}
