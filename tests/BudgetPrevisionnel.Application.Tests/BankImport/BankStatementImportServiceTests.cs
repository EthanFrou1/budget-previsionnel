using BudgetPrevisionnel.Application.BankAccounts;
using BudgetPrevisionnel.Application.BankImport;
using BudgetPrevisionnel.Application.Tests.BankAccounts;
using BudgetPrevisionnel.Application.Tests.Categories;
using BudgetPrevisionnel.Application.Tests.Transactions;

namespace BudgetPrevisionnel.Application.Tests.BankImport;

public class BankStatementImportServiceTests
{
    [Fact]
    public async Task PreviewAsync_NewTransactions_ResolvesSystemCategoryNameAndDoesNotPersist()
    {
        var bankAccounts = new FakeBankAccountRepository();
        var account = bankAccounts.Add(userId: 1, bankName: "BoursoBank", label: "Compte courant");
        var transactions = new FakeTransactionRepository(bankAccounts);
        var categories = new FakeCategoryRepository().Seed(id: 2, name: "Logement");
        var rules = new FakeCategoryRuleRepository();

        var parsed = new ParsedBankTransaction[]
        {
            new(new DateOnly(2026, 8, 5), "PRLV SEPA MSF", "Msf", -10m, "Logement"),
            new(new DateOnly(2026, 8, 6), "CARTE UNKNOWN", "Unknown", -5m, null)
        };
        var service = new BankStatementImportService(
            [new FakeBankStatementParser("BoursoBank", parsed)], bankAccounts, transactions, categories, rules);

        var rows = await service.PreviewAsync(userId: 1, bankAccountId: account.Id, Stream.Null);

        Assert.Equal(2, rows.Count);
        var msfRow = rows.Single(r => r.RawLabel == "PRLV SEPA MSF");
        Assert.Equal(2, msfRow.CategoryId);
        Assert.Equal("Logement", msfRow.CategoryName);
        var unknownRow = rows.Single(r => r.RawLabel == "CARTE UNKNOWN");
        Assert.Null(unknownRow.CategoryId);
        Assert.Empty(transactions.All); // nothing persisted yet
    }

    [Fact]
    public async Task PreviewAsync_NoExactCategoryMatch_FallsBackToUserCategoryRule()
    {
        // "Auto & Moto" (BoursoBank's Catégorie Parente) has no matching system category,
        // but the user has taught a rule that "TOTAL" in the label means "Transport".
        var bankAccounts = new FakeBankAccountRepository();
        var account = bankAccounts.Add(userId: 1, bankName: "BoursoBank", label: "Compte courant");
        var transactions = new FakeTransactionRepository(bankAccounts);
        var categories = new FakeCategoryRepository().Seed(id: 3, name: "Transport");
        var rules = new FakeCategoryRuleRepository().Seed(ownerId: 1, matchPattern: "TOTAL", categoryId: 3, priority: 1);

        var parsed = new ParsedBankTransaction[]
        {
            new(new DateOnly(2026, 8, 3), "CARTE 01/08/26 TOTAL 4 CB*2856", "TotalEnergies", -80m, "Auto & Moto")
        };
        var service = new BankStatementImportService(
            [new FakeBankStatementParser("BoursoBank", parsed)], bankAccounts, transactions, categories, rules);

        var rows = await service.PreviewAsync(1, account.Id, Stream.Null);

        var row = Assert.Single(rows);
        Assert.Equal(3, row.CategoryId);
        Assert.Equal("Transport", row.CategoryName);
    }

    [Fact]
    public async Task PreviewAsync_RowAlreadyImported_IsExcludedFromThePreview()
    {
        var bankAccounts = new FakeBankAccountRepository();
        var account = bankAccounts.Add(1, "BoursoBank", "Compte courant");
        var transactions = new FakeTransactionRepository(bankAccounts);
        var categories = new FakeCategoryRepository();
        var rules = new FakeCategoryRuleRepository();

        var parsed = new ParsedBankTransaction[]
        {
            new(new DateOnly(2026, 8, 5), "PRLV SEPA MSF", "Msf", -10m, null)
        };
        var service = new BankStatementImportService(
            [new FakeBankStatementParser("BoursoBank", parsed)], bankAccounts, transactions, categories, rules);

        var firstPreview = await service.PreviewAsync(1, account.Id, Stream.Null);
        await service.CommitAsync(1, account.Id, firstPreview);
        var secondPreview = await service.PreviewAsync(1, account.Id, Stream.Null);

        Assert.Empty(secondPreview);
    }

    [Fact]
    public async Task PreviewAsync_UnknownBank_ThrowsNoParserAvailable()
    {
        var bankAccounts = new FakeBankAccountRepository();
        var account = bankAccounts.Add(1, "UnknownBank", "Compte courant");
        var transactions = new FakeTransactionRepository(bankAccounts);
        var categories = new FakeCategoryRepository();
        var rules = new FakeCategoryRuleRepository();
        var service = new BankStatementImportService(
            [new FakeBankStatementParser("BoursoBank", [])], bankAccounts, transactions, categories, rules);

        await Assert.ThrowsAsync<NoParserAvailableException>(
            () => service.PreviewAsync(1, account.Id, Stream.Null));
    }

    [Fact]
    public async Task PreviewAsync_AccountBelongsToAnotherUser_ThrowsBankAccountNotFound()
    {
        var bankAccounts = new FakeBankAccountRepository();
        var account = bankAccounts.Add(userId: 1, bankName: "BoursoBank", label: "Compte courant");
        var transactions = new FakeTransactionRepository(bankAccounts);
        var categories = new FakeCategoryRepository();
        var rules = new FakeCategoryRuleRepository();
        var service = new BankStatementImportService(
            [new FakeBankStatementParser("BoursoBank", [])], bankAccounts, transactions, categories, rules);

        await Assert.ThrowsAsync<BankAccountNotFoundException>(
            () => service.PreviewAsync(userId: 999, account.Id, Stream.Null));
    }

    [Fact]
    public async Task CommitAsync_AllRowsKept_PersistsThemAndReturnsTheirCount()
    {
        var bankAccounts = new FakeBankAccountRepository();
        var account = bankAccounts.Add(1, "BoursoBank", "Compte courant");
        var transactions = new FakeTransactionRepository(bankAccounts);
        var categories = new FakeCategoryRepository();
        var rules = new FakeCategoryRuleRepository();
        var service = new BankStatementImportService(
            [new FakeBankStatementParser("BoursoBank", [])], bankAccounts, transactions, categories, rules);

        var rows = new[]
        {
            new ImportRow(new DateOnly(2026, 8, 5), "PRLV SEPA MSF", "Msf", -10m, CategoryId: 2, CategoryName: "Logement"),
            new ImportRow(new DateOnly(2026, 8, 6), "CARTE UNKNOWN", "Unknown", -5m, CategoryId: null, CategoryName: null)
        };

        var summary = await service.CommitAsync(1, account.Id, rows);

        Assert.Equal(2, summary.NewTransactionsImported);
        Assert.Equal(0, summary.DuplicatesSkipped);
        Assert.Equal(2, transactions.All.Single(t => t.RawLabel == "PRLV SEPA MSF").CategoryId);
        Assert.Null(transactions.All.Single(t => t.RawLabel == "CARTE UNKNOWN").CategoryId);
    }

    [Fact]
    public async Task CommitAsync_RowExcludedByTheUser_IsNeverPersisted()
    {
        var bankAccounts = new FakeBankAccountRepository();
        var account = bankAccounts.Add(1, "BoursoBank", "Compte courant");
        var transactions = new FakeTransactionRepository(bankAccounts);
        var categories = new FakeCategoryRepository();
        var rules = new FakeCategoryRuleRepository();
        var service = new BankStatementImportService(
            [new FakeBankStatementParser("BoursoBank", [])], bankAccounts, transactions, categories, rules);

        // The client only ever sends the rows it wants kept - an excluded row is simply
        // never in this list, no separate "excluded" flag needed.
        var keptRow = new ImportRow(new DateOnly(2026, 8, 5), "PRLV SEPA MSF", "Msf", -10m, null, null);

        await service.CommitAsync(1, account.Id, [keptRow]);

        Assert.Single(transactions.All);
    }

    [Fact]
    public async Task CommitAsync_UserOverridesTheSuggestedCategory_PersistsTheOverride()
    {
        var bankAccounts = new FakeBankAccountRepository();
        var account = bankAccounts.Add(1, "BoursoBank", "Compte courant");
        var transactions = new FakeTransactionRepository(bankAccounts);
        var categories = new FakeCategoryRepository();
        var rules = new FakeCategoryRuleRepository();
        var service = new BankStatementImportService(
            [new FakeBankStatementParser("BoursoBank", [])], bankAccounts, transactions, categories, rules);

        // Preview suggested CategoryId 2, but the user picked a different one before commit.
        var editedRow = new ImportRow(new DateOnly(2026, 8, 5), "PRLV SEPA MSF", "Msf", -10m, CategoryId: 7, CategoryName: null);

        await service.CommitAsync(1, account.Id, [editedRow]);

        Assert.Equal(7, Assert.Single(transactions.All).CategoryId);
    }

    [Fact]
    public async Task CommitAsync_RowAlreadyImportedSincePreview_IsSkippedAgain()
    {
        var bankAccounts = new FakeBankAccountRepository();
        var account = bankAccounts.Add(1, "BoursoBank", "Compte courant");
        var transactions = new FakeTransactionRepository(bankAccounts);
        var categories = new FakeCategoryRepository();
        var rules = new FakeCategoryRuleRepository();
        var service = new BankStatementImportService(
            [new FakeBankStatementParser("BoursoBank", [])], bankAccounts, transactions, categories, rules);

        var row = new ImportRow(new DateOnly(2026, 8, 5), "PRLV SEPA MSF", "Msf", -10m, null, null);
        await service.CommitAsync(1, account.Id, [row]);

        var secondSummary = await service.CommitAsync(1, account.Id, [row]);

        Assert.Equal(0, secondSummary.NewTransactionsImported);
        Assert.Equal(1, secondSummary.DuplicatesSkipped);
        Assert.Single(transactions.All); // not duplicated in storage either
    }

    [Fact]
    public async Task CommitAsync_AccountBelongsToAnotherUser_ThrowsBankAccountNotFound()
    {
        var bankAccounts = new FakeBankAccountRepository();
        var account = bankAccounts.Add(userId: 1, bankName: "BoursoBank", label: "Compte courant");
        var transactions = new FakeTransactionRepository(bankAccounts);
        var categories = new FakeCategoryRepository();
        var rules = new FakeCategoryRuleRepository();
        var service = new BankStatementImportService(
            [new FakeBankStatementParser("BoursoBank", [])], bankAccounts, transactions, categories, rules);

        await Assert.ThrowsAsync<BankAccountNotFoundException>(
            () => service.CommitAsync(userId: 999, account.Id, []));
    }

    [Fact]
    public async Task CommitAsync_MatchingTransferInAnotherOwnAccount_FlagsBothAsInternalTransfer()
    {
        var bankAccounts = new FakeBankAccountRepository();
        var checking = bankAccounts.Add(1, "BoursoBank", "Compte courant");
        var savings = bankAccounts.Add(1, "BoursoBank", "Livret");
        var transactions = new FakeTransactionRepository(bankAccounts);
        var categories = new FakeCategoryRepository();
        var rules = new FakeCategoryRuleRepository();
        var service = new BankStatementImportService(
            [new FakeBankStatementParser("BoursoBank", [])], bankAccounts, transactions, categories, rules);

        var outgoing = new ImportRow(new DateOnly(2026, 8, 10), "VIR VERS LIVRET", null, -200m, null, null);
        await service.CommitAsync(1, checking.Id, [outgoing]);

        var incoming = new ImportRow(new DateOnly(2026, 8, 11), "VIR DU COURANT", null, 200m, null, null);
        var summary = await service.CommitAsync(1, savings.Id, [incoming]);

        Assert.Equal(1, summary.InternalTransfersDetected);
        Assert.All(transactions.All, t => Assert.True(t.IsInternalTransfer));
    }

    [Fact]
    public async Task CommitAsync_MatchingAmountInAnotherUsersAccount_IsNotFlagged()
    {
        var bankAccounts = new FakeBankAccountRepository();
        var mine = bankAccounts.Add(userId: 1, bankName: "BoursoBank", label: "Compte courant");
        var someoneElses = bankAccounts.Add(userId: 2, bankName: "BoursoBank", label: "Compte courant");
        var transactions = new FakeTransactionRepository(bankAccounts);
        var categories = new FakeCategoryRepository();
        var rules = new FakeCategoryRuleRepository();
        var service = new BankStatementImportService(
            [new FakeBankStatementParser("BoursoBank", [])], bankAccounts, transactions, categories, rules);

        var theirDeposit = new ImportRow(new DateOnly(2026, 8, 10), "VIR", null, 200m, null, null);
        await service.CommitAsync(2, someoneElses.Id, [theirDeposit]);

        var myWithdrawal = new ImportRow(new DateOnly(2026, 8, 10), "VIR", null, -200m, null, null);
        var summary = await service.CommitAsync(1, mine.Id, [myWithdrawal]);

        Assert.Equal(0, summary.InternalTransfersDetected);
    }
}
