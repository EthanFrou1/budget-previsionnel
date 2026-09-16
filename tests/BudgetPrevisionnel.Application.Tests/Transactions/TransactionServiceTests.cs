using BudgetPrevisionnel.Application.BankAccounts;
using BudgetPrevisionnel.Application.Common;
using BudgetPrevisionnel.Application.Tests.BankAccounts;
using BudgetPrevisionnel.Application.Tests.Categories;
using BudgetPrevisionnel.Application.Transactions;
using BudgetPrevisionnel.Domain.Entities;

namespace BudgetPrevisionnel.Application.Tests.Transactions;

public class TransactionServiceTests
{
    private static Transaction Tx(int id, int bankAccountId, DateOnly date, string rawLabel, decimal amount,
        int? categoryId = null, bool isInternalTransfer = false, string? cleanedLabel = null) => new()
    {
        Id = id,
        BankAccountId = bankAccountId,
        Date = date,
        RawLabel = rawLabel,
        CleanedLabel = cleanedLabel,
        Amount = amount,
        CategoryId = categoryId,
        IsInternalTransfer = isInternalTransfer
    };

    private static (FakeBankAccountRepository Accounts, FakeTransactionRepository Transactions,
        FakeCategoryRepository Categories, TransactionService Service) CreateSubject()
    {
        var accounts = new FakeBankAccountRepository();
        var transactions = new FakeTransactionRepository(accounts);
        var categories = new FakeCategoryRepository();
        var service = new TransactionService(transactions, accounts, categories);
        return (accounts, transactions, categories, service);
    }

    [Fact]
    public async Task SearchAsync_NoBankAccountFilter_ReturnsConsolidatedAcrossAllOwnAccounts()
    {
        var (accounts, transactions, _, service) = CreateSubject();
        var checking = accounts.Add(1, "BoursoBank", "Compte courant");
        var savings = accounts.Add(1, "BoursoBank", "Livret");
        await transactions.AddRangeAsync(
        [
            Tx(0, checking.Id, new DateOnly(2026, 8, 1), "A", -10m),
            Tx(0, savings.Id, new DateOnly(2026, 8, 2), "B", 20m)
        ]);

        var result = await service.SearchAsync(1, null, null, null, null, null, false, 1, 50);

        Assert.Equal(2, result.TotalCount);
    }

    [Fact]
    public async Task SearchAsync_WithBankAccountFilter_ReturnsOnlyThatAccount()
    {
        var (accounts, transactions, _, service) = CreateSubject();
        var checking = accounts.Add(1, "BoursoBank", "Compte courant");
        var savings = accounts.Add(1, "BoursoBank", "Livret");
        await transactions.AddRangeAsync(
        [
            Tx(0, checking.Id, new DateOnly(2026, 8, 1), "A", -10m),
            Tx(0, savings.Id, new DateOnly(2026, 8, 2), "B", 20m)
        ]);

        var result = await service.SearchAsync(1, checking.Id, null, null, null, null, false, 1, 50);

        Assert.Single(result.Items);
        Assert.Equal(checking.Id, result.Items[0].BankAccountId);
    }

    [Fact]
    public async Task SearchAsync_AnotherUsersAccountId_ThrowsBankAccountNotFound()
    {
        var (accounts, _, _, service) = CreateSubject();
        var notMine = accounts.Add(2, "BoursoBank", "Pas à moi");

        await Assert.ThrowsAsync<BankAccountNotFoundException>(
            () => service.SearchAsync(1, notMine.Id, null, null, null, null, false, 1, 50));
    }

    [Fact]
    public async Task SearchAsync_ExcludeInternalTransfers_FiltersThemOut()
    {
        var (accounts, transactions, _, service) = CreateSubject();
        var account = accounts.Add(1, "BoursoBank", "Compte courant");
        await transactions.AddRangeAsync(
        [
            Tx(0, account.Id, new DateOnly(2026, 8, 1), "Achat", -10m),
            Tx(0, account.Id, new DateOnly(2026, 8, 2), "Virement interne", -200m, isInternalTransfer: true)
        ]);

        var result = await service.SearchAsync(1, null, null, null, null, null, excludeInternalTransfers: true, 1, 50);

        Assert.Single(result.Items);
        Assert.Equal("Achat", result.Items[0].RawLabel);
    }

    [Fact]
    public async Task SearchAsync_DateRange_FiltersOutsideRange()
    {
        var (accounts, transactions, _, service) = CreateSubject();
        var account = accounts.Add(1, "BoursoBank", "Compte courant");
        await transactions.AddRangeAsync(
        [
            Tx(0, account.Id, new DateOnly(2026, 7, 1), "Trop tôt", -10m),
            Tx(0, account.Id, new DateOnly(2026, 8, 15), "Dans la période", -10m),
            Tx(0, account.Id, new DateOnly(2026, 9, 1), "Trop tard", -10m)
        ]);

        var result = await service.SearchAsync(
            1, null, null, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31), null, false, 1, 50);

        Assert.Single(result.Items);
        Assert.Equal("Dans la période", result.Items[0].RawLabel);
    }

    [Fact]
    public async Task SearchAsync_SearchText_MatchesRawOrCleanedLabelCaseInsensitive()
    {
        var (accounts, transactions, _, service) = CreateSubject();
        var account = accounts.Add(1, "BoursoBank", "Compte courant");
        await transactions.AddRangeAsync(
        [
            Tx(0, account.Id, new DateOnly(2026, 8, 1), "CARTE NETFLIX.COM", -10m, cleanedLabel: "Netflix"),
            Tx(0, account.Id, new DateOnly(2026, 8, 2), "CARTE SPOTIFY", -10m, cleanedLabel: "Spotify")
        ]);

        var result = await service.SearchAsync(1, null, null, null, null, "netflix", false, 1, 50);

        Assert.Single(result.Items);
    }

    [Fact]
    public async Task SearchAsync_PageSizeAndPage_AreClampedAndNormalized()
    {
        var (accounts, transactions, _, service) = CreateSubject();
        var account = accounts.Add(1, "BoursoBank", "Compte courant");
        await transactions.AddRangeAsync(
            Enumerable.Range(1, 5).Select(i => Tx(0, account.Id, new DateOnly(2026, 8, i), $"Tx{i}", -1m)));

        // page 0 and pageSize 0 are what an unset query-string parameter binds to -
        // must behave like page 1 / the default page size, not an empty or crashing result.
        var result = await service.SearchAsync(1, null, null, null, null, null, false, page: 0, pageSize: 0);

        Assert.Equal(1, result.Page);
        Assert.Equal(5, result.Items.Count);
    }

    [Fact]
    public async Task SearchAsync_SortByAmountAscending_OrdersFromSmallestToLargest()
    {
        var (accounts, transactions, _, service) = CreateSubject();
        var account = accounts.Add(1, "BoursoBank", "Compte courant");
        await transactions.AddRangeAsync(
        [
            Tx(0, account.Id, new DateOnly(2026, 8, 1), "B", -50m),
            Tx(0, account.Id, new DateOnly(2026, 8, 2), "A", -200m),
            Tx(0, account.Id, new DateOnly(2026, 8, 3), "C", 10m)
        ]);

        var result = await service.SearchAsync(
            1, null, null, null, null, null, false, 1, 50, sortBy: "amount", sortDirection: "asc");

        Assert.Equal(["A", "B", "C"], result.Items.Select(t => t.RawLabel));
    }

    [Fact]
    public async Task SearchAsync_SortByLabelDescending_OrdersReverseAlphabetically()
    {
        var (accounts, transactions, _, service) = CreateSubject();
        var account = accounts.Add(1, "BoursoBank", "Compte courant");
        await transactions.AddRangeAsync(
        [
            Tx(0, account.Id, new DateOnly(2026, 8, 1), "Alpha", -10m),
            Tx(0, account.Id, new DateOnly(2026, 8, 2), "Beta", -10m),
            Tx(0, account.Id, new DateOnly(2026, 8, 3), "Charlie", -10m)
        ]);

        var result = await service.SearchAsync(
            1, null, null, null, null, null, false, 1, 50, sortBy: "label", sortDirection: "desc");

        Assert.Equal(["Charlie", "Beta", "Alpha"], result.Items.Select(t => t.RawLabel));
    }

    [Fact]
    public async Task SearchAsync_SortByDateAscending_ReversesTheDefaultOrder()
    {
        var (accounts, transactions, _, service) = CreateSubject();
        var account = accounts.Add(1, "BoursoBank", "Compte courant");
        await transactions.AddRangeAsync(
        [
            Tx(0, account.Id, new DateOnly(2026, 8, 1), "Oldest", -10m),
            Tx(0, account.Id, new DateOnly(2026, 8, 15), "Newest", -10m)
        ]);

        var result = await service.SearchAsync(
            1, null, null, null, null, null, false, 1, 50, sortBy: "date", sortDirection: "asc");

        Assert.Equal(["Oldest", "Newest"], result.Items.Select(t => t.RawLabel));
    }

    [Fact]
    public async Task SearchAsync_UnknownSortBy_FallsBackToDateDescending()
    {
        var (accounts, transactions, _, service) = CreateSubject();
        var account = accounts.Add(1, "BoursoBank", "Compte courant");
        await transactions.AddRangeAsync(
        [
            Tx(0, account.Id, new DateOnly(2026, 8, 1), "Oldest", -10m),
            Tx(0, account.Id, new DateOnly(2026, 8, 15), "Newest", -10m)
        ]);

        var result = await service.SearchAsync(
            1, null, null, null, null, null, false, 1, 50, sortBy: "not-a-column");

        Assert.Equal(["Newest", "Oldest"], result.Items.Select(t => t.RawLabel));
    }

    [Fact]
    public async Task SearchAsync_AnotherUsersTransactions_NeverIncluded()
    {
        var (accounts, transactions, _, service) = CreateSubject();
        var mine = accounts.Add(1, "BoursoBank", "Compte courant");
        var theirs = accounts.Add(2, "BoursoBank", "Compte courant");
        await transactions.AddRangeAsync(
        [
            Tx(0, mine.Id, new DateOnly(2026, 8, 1), "Mine", -10m),
            Tx(0, theirs.Id, new DateOnly(2026, 8, 1), "Theirs", -10m)
        ]);

        var result = await service.SearchAsync(1, null, null, null, null, null, false, 1, 50);

        Assert.Single(result.Items);
        Assert.Equal("Mine", result.Items[0].RawLabel);
    }

    [Fact]
    public async Task UpdateCategoryAsync_ValidCategory_SetsCategoryIdAndNavigation()
    {
        var (accounts, transactions, categories, service) = CreateSubject();
        var account = accounts.Add(1, "BoursoBank", "Compte courant");
        categories.Seed(1, "Alimentation");
        await transactions.AddRangeAsync([Tx(0, account.Id, new DateOnly(2026, 8, 1), "Supermarché", -20m)]);
        var transactionId = transactions.All[0].Id;

        var updated = await service.UpdateCategoryAsync(1, transactionId, 1);

        Assert.Equal(1, updated.CategoryId);
        Assert.Equal("Alimentation", updated.Category?.Name);
    }

    [Fact]
    public async Task UpdateCategoryAsync_NullCategoryId_UncategorizesTransaction()
    {
        var (accounts, transactions, categories, service) = CreateSubject();
        var account = accounts.Add(1, "BoursoBank", "Compte courant");
        categories.Seed(1, "Alimentation");
        await transactions.AddRangeAsync([Tx(0, account.Id, new DateOnly(2026, 8, 1), "Supermarché", -20m, categoryId: 1)]);
        var transactionId = transactions.All[0].Id;

        var updated = await service.UpdateCategoryAsync(1, transactionId, null);

        Assert.Null(updated.CategoryId);
        Assert.Null(updated.Category);
    }

    [Fact]
    public async Task UpdateCategoryAsync_UnknownTransactionId_ThrowsTransactionNotFound()
    {
        var (_, _, _, service) = CreateSubject();

        await Assert.ThrowsAsync<TransactionNotFoundException>(() => service.UpdateCategoryAsync(1, 999, null));
    }

    [Fact]
    public async Task UpdateCategoryAsync_AnotherUsersTransaction_ThrowsTransactionNotFound()
    {
        var (accounts, transactions, _, service) = CreateSubject();
        var theirs = accounts.Add(2, "BoursoBank", "Compte courant");
        await transactions.AddRangeAsync([Tx(0, theirs.Id, new DateOnly(2026, 8, 1), "Pas à moi", -10m)]);
        var transactionId = transactions.All[0].Id;

        await Assert.ThrowsAsync<TransactionNotFoundException>(() => service.UpdateCategoryAsync(1, transactionId, null));
    }

    [Fact]
    public async Task UpdateCategoryAsync_AnotherUsersCustomCategory_ThrowsInvalidReference()
    {
        var (accounts, transactions, categories, service) = CreateSubject();
        var account = accounts.Add(1, "BoursoBank", "Compte courant");
        categories.Seed(1, "Perso d'un autre", ownerId: 2);
        await transactions.AddRangeAsync([Tx(0, account.Id, new DateOnly(2026, 8, 1), "Achat", -10m)]);
        var transactionId = transactions.All[0].Id;

        await Assert.ThrowsAsync<InvalidReferenceException>(() => service.UpdateCategoryAsync(1, transactionId, 1));
    }

    [Fact]
    public async Task UpdateNotesAsync_ValidNote_TrimsAndSetsNotes()
    {
        var (accounts, transactions, _, service) = CreateSubject();
        var account = accounts.Add(1, "BoursoBank", "Compte courant");
        await transactions.AddRangeAsync([Tx(0, account.Id, new DateOnly(2026, 8, 1), "Achat", -10m)]);
        var transactionId = transactions.All[0].Id;

        var updated = await service.UpdateNotesAsync(1, transactionId, "  Remboursé par Paul  ");

        Assert.Equal("Remboursé par Paul", updated.Notes);
    }

    [Fact]
    public async Task UpdateNotesAsync_BlankNote_ClearsNotes()
    {
        var (accounts, transactions, _, service) = CreateSubject();
        var account = accounts.Add(1, "BoursoBank", "Compte courant");
        await transactions.AddRangeAsync([Tx(0, account.Id, new DateOnly(2026, 8, 1), "Achat", -10m)]);
        var transactionId = transactions.All[0].Id;
        await service.UpdateNotesAsync(1, transactionId, "Une note");

        var updated = await service.UpdateNotesAsync(1, transactionId, "   ");

        Assert.Null(updated.Notes);
    }

    [Fact]
    public async Task UpdateNotesAsync_TooLong_ThrowsInvalidTransaction()
    {
        var (accounts, transactions, _, service) = CreateSubject();
        var account = accounts.Add(1, "BoursoBank", "Compte courant");
        await transactions.AddRangeAsync([Tx(0, account.Id, new DateOnly(2026, 8, 1), "Achat", -10m)]);
        var transactionId = transactions.All[0].Id;

        await Assert.ThrowsAsync<InvalidTransactionException>(
            () => service.UpdateNotesAsync(1, transactionId, new string('a', 501)));
    }

    [Fact]
    public async Task UpdateNotesAsync_UnknownTransactionId_ThrowsTransactionNotFound()
    {
        var (_, _, _, service) = CreateSubject();

        await Assert.ThrowsAsync<TransactionNotFoundException>(() => service.UpdateNotesAsync(1, 999, "Note"));
    }

    [Fact]
    public async Task UpdateNotesAsync_AnotherUsersTransaction_ThrowsTransactionNotFound()
    {
        var (accounts, transactions, _, service) = CreateSubject();
        var theirs = accounts.Add(2, "BoursoBank", "Compte courant");
        await transactions.AddRangeAsync([Tx(0, theirs.Id, new DateOnly(2026, 8, 1), "Pas à moi", -10m)]);
        var transactionId = transactions.All[0].Id;

        await Assert.ThrowsAsync<TransactionNotFoundException>(() => service.UpdateNotesAsync(1, transactionId, "Note"));
    }
}
