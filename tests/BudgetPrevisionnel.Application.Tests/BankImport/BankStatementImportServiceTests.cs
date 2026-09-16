using BudgetPrevisionnel.Application.BankAccounts;
using BudgetPrevisionnel.Application.BankImport;
using BudgetPrevisionnel.Application.Tests.BankAccounts;
using BudgetPrevisionnel.Application.Tests.Categories;
using BudgetPrevisionnel.Application.Tests.Transactions;
using BudgetPrevisionnel.Domain.Entities;

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
        var importBatches = new FakeImportBatchRepository();

        var parsed = new ParsedBankTransaction[]
        {
            new(new DateOnly(2026, 8, 5), "PRLV SEPA MSF", "Msf", -10m, "Logement"),
            new(new DateOnly(2026, 8, 6), "CARTE UNKNOWN", "Unknown", -5m, null)
        };
        var service = new BankStatementImportService(
            [new FakeBankStatementParser("BoursoBank", parsed)], bankAccounts, transactions, categories, rules, importBatches);

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
        var importBatches = new FakeImportBatchRepository();

        var parsed = new ParsedBankTransaction[]
        {
            new(new DateOnly(2026, 8, 3), "CARTE 01/08/26 TOTAL 4 CB*2856", "TotalEnergies", -80m, "Auto & Moto")
        };
        var service = new BankStatementImportService(
            [new FakeBankStatementParser("BoursoBank", parsed)], bankAccounts, transactions, categories, rules, importBatches);

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
        var importBatches = new FakeImportBatchRepository();

        var parsed = new ParsedBankTransaction[]
        {
            new(new DateOnly(2026, 8, 5), "PRLV SEPA MSF", "Msf", -10m, null)
        };
        var service = new BankStatementImportService(
            [new FakeBankStatementParser("BoursoBank", parsed)], bankAccounts, transactions, categories, rules, importBatches);

        var firstPreview = await service.PreviewAsync(1, account.Id, Stream.Null);
        await service.CommitAsync(1, account.Id, "releve.csv", firstPreview);
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
        var importBatches = new FakeImportBatchRepository();
        var service = new BankStatementImportService(
            [new FakeBankStatementParser("BoursoBank", [])], bankAccounts, transactions, categories, rules, importBatches);

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
        var importBatches = new FakeImportBatchRepository();
        var service = new BankStatementImportService(
            [new FakeBankStatementParser("BoursoBank", [])], bankAccounts, transactions, categories, rules, importBatches);

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
        var importBatches = new FakeImportBatchRepository();
        var service = new BankStatementImportService(
            [new FakeBankStatementParser("BoursoBank", [])], bankAccounts, transactions, categories, rules, importBatches);

        var rows = new[]
        {
            new ImportRow(new DateOnly(2026, 8, 5), "PRLV SEPA MSF", "Msf", -10m, CategoryId: 2, CategoryName: "Logement"),
            new ImportRow(new DateOnly(2026, 8, 6), "CARTE UNKNOWN", "Unknown", -5m, CategoryId: null, CategoryName: null)
        };

        var summary = await service.CommitAsync(1, account.Id, "releve.csv", rows);

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
        var importBatches = new FakeImportBatchRepository();
        var service = new BankStatementImportService(
            [new FakeBankStatementParser("BoursoBank", [])], bankAccounts, transactions, categories, rules, importBatches);

        // The client only ever sends the rows it wants kept - an excluded row is simply
        // never in this list, no separate "excluded" flag needed.
        var keptRow = new ImportRow(new DateOnly(2026, 8, 5), "PRLV SEPA MSF", "Msf", -10m, null, null);

        await service.CommitAsync(1, account.Id, "releve.csv", [keptRow]);

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
        var importBatches = new FakeImportBatchRepository();
        var service = new BankStatementImportService(
            [new FakeBankStatementParser("BoursoBank", [])], bankAccounts, transactions, categories, rules, importBatches);

        // Preview suggested CategoryId 2, but the user picked a different one before commit.
        var editedRow = new ImportRow(new DateOnly(2026, 8, 5), "PRLV SEPA MSF", "Msf", -10m, CategoryId: 7, CategoryName: null);

        await service.CommitAsync(1, account.Id, "releve.csv", [editedRow]);

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
        var importBatches = new FakeImportBatchRepository();
        var service = new BankStatementImportService(
            [new FakeBankStatementParser("BoursoBank", [])], bankAccounts, transactions, categories, rules, importBatches);

        var row = new ImportRow(new DateOnly(2026, 8, 5), "PRLV SEPA MSF", "Msf", -10m, null, null);
        await service.CommitAsync(1, account.Id, "releve.csv", [row]);

        var secondSummary = await service.CommitAsync(1, account.Id, "releve.csv", [row]);

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
        var importBatches = new FakeImportBatchRepository();
        var service = new BankStatementImportService(
            [new FakeBankStatementParser("BoursoBank", [])], bankAccounts, transactions, categories, rules, importBatches);

        await Assert.ThrowsAsync<BankAccountNotFoundException>(
            () => service.CommitAsync(userId: 999, account.Id, "releve.csv", []));
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
        var importBatches = new FakeImportBatchRepository();
        var service = new BankStatementImportService(
            [new FakeBankStatementParser("BoursoBank", [])], bankAccounts, transactions, categories, rules, importBatches);

        var outgoing = new ImportRow(new DateOnly(2026, 8, 10), "VIR VERS LIVRET", null, -200m, null, null);
        await service.CommitAsync(1, checking.Id, "releve.csv", [outgoing]);

        var incoming = new ImportRow(new DateOnly(2026, 8, 11), "VIR DU COURANT", null, 200m, null, null);
        var summary = await service.CommitAsync(1, savings.Id, "releve.csv", [incoming]);

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
        var importBatches = new FakeImportBatchRepository();
        var service = new BankStatementImportService(
            [new FakeBankStatementParser("BoursoBank", [])], bankAccounts, transactions, categories, rules, importBatches);

        var theirDeposit = new ImportRow(new DateOnly(2026, 8, 10), "VIR", null, 200m, null, null);
        await service.CommitAsync(2, someoneElses.Id, "releve.csv", [theirDeposit]);

        var myWithdrawal = new ImportRow(new DateOnly(2026, 8, 10), "VIR", null, -200m, null, null);
        var summary = await service.CommitAsync(1, mine.Id, "releve.csv", [myWithdrawal]);

        Assert.Equal(0, summary.InternalTransfersDetected);
    }

    [Fact]
    public async Task CommitAsync_RecordsAnImportBatchWithFileNameAndCounts()
    {
        var bankAccounts = new FakeBankAccountRepository();
        var account = bankAccounts.Add(1, "BoursoBank", "Compte courant");
        var transactions = new FakeTransactionRepository(bankAccounts);
        var categories = new FakeCategoryRepository();
        var rules = new FakeCategoryRuleRepository();
        var importBatches = new FakeImportBatchRepository();
        var service = new BankStatementImportService(
            [new FakeBankStatementParser("BoursoBank", [])], bankAccounts, transactions, categories, rules, importBatches);
        var row = new ImportRow(new DateOnly(2026, 8, 5), "PRLV SEPA MSF", "Msf", -10m, null, null);

        await service.CommitAsync(1, account.Id, "aout-2026.csv", [row]);

        var batch = Assert.Single(importBatches.All);
        Assert.Equal(account.Id, batch.BankAccountId);
        Assert.Equal("aout-2026.csv", batch.FileName);
        Assert.Equal(1, batch.TotalRowsParsed);
        Assert.Equal(1, batch.NewTransactionsImported);
    }

    [Fact]
    public async Task CommitAsync_AllRowsAlreadyDuplicates_StillRecordsTheBatch()
    {
        var bankAccounts = new FakeBankAccountRepository();
        var account = bankAccounts.Add(1, "BoursoBank", "Compte courant");
        var transactions = new FakeTransactionRepository(bankAccounts);
        var categories = new FakeCategoryRepository();
        var rules = new FakeCategoryRuleRepository();
        var importBatches = new FakeImportBatchRepository();
        var service = new BankStatementImportService(
            [new FakeBankStatementParser("BoursoBank", [])], bankAccounts, transactions, categories, rules, importBatches);
        var row = new ImportRow(new DateOnly(2026, 8, 5), "PRLV SEPA MSF", "Msf", -10m, null, null);
        await service.CommitAsync(1, account.Id, "premier-import.csv", [row]);

        await service.CommitAsync(1, account.Id, "re-import-par-erreur.csv", [row]);

        Assert.Equal(2, importBatches.All.Count);
        var secondBatch = importBatches.All.Single(b => b.FileName == "re-import-par-erreur.csv");
        Assert.Equal(0, secondBatch.NewTransactionsImported);
        Assert.Equal(1, secondBatch.DuplicatesSkipped);
    }

    [Fact]
    public async Task CommitAsync_WithFileContent_StoresItOnTheBatch()
    {
        var bankAccounts = new FakeBankAccountRepository();
        var account = bankAccounts.Add(1, "BoursoBank", "Compte courant");
        var transactions = new FakeTransactionRepository(bankAccounts);
        var categories = new FakeCategoryRepository();
        var rules = new FakeCategoryRuleRepository();
        var importBatches = new FakeImportBatchRepository();
        var service = new BankStatementImportService(
            [new FakeBankStatementParser("BoursoBank", [])], bankAccounts, transactions, categories, rules, importBatches);
        var row = new ImportRow(new DateOnly(2026, 8, 5), "PRLV SEPA MSF", "Msf", -10m, null, null);
        var csvBytes = "Date;Libelle\n2026-08-05;PRLV SEPA MSF"u8.ToArray();

        await service.CommitAsync(1, account.Id, "aout-2026.csv", [row], csvBytes);

        var batch = Assert.Single(importBatches.All);
        Assert.Equal(csvBytes, batch.FileContent);
    }

    [Fact]
    public async Task CommitAsync_WithoutFileContent_LeavesItNull()
    {
        var bankAccounts = new FakeBankAccountRepository();
        var account = bankAccounts.Add(1, "BoursoBank", "Compte courant");
        var transactions = new FakeTransactionRepository(bankAccounts);
        var categories = new FakeCategoryRepository();
        var rules = new FakeCategoryRuleRepository();
        var importBatches = new FakeImportBatchRepository();
        var service = new BankStatementImportService(
            [new FakeBankStatementParser("BoursoBank", [])], bankAccounts, transactions, categories, rules, importBatches);
        var row = new ImportRow(new DateOnly(2026, 8, 5), "PRLV SEPA MSF", "Msf", -10m, null, null);

        await service.CommitAsync(1, account.Id, "aout-2026.csv", [row]);

        Assert.Null(Assert.Single(importBatches.All).FileContent);
    }

    [Fact]
    public async Task GetFileAsync_BatchHasStoredContent_ReturnsFileNameAndBytes()
    {
        var bankAccounts = new FakeBankAccountRepository();
        var account = bankAccounts.Add(1, "BoursoBank", "Compte courant");
        var transactions = new FakeTransactionRepository(bankAccounts);
        var categories = new FakeCategoryRepository();
        var rules = new FakeCategoryRuleRepository();
        var importBatches = new FakeImportBatchRepository();
        var service = new BankStatementImportService(
            [new FakeBankStatementParser("BoursoBank", [])], bankAccounts, transactions, categories, rules, importBatches);
        var row = new ImportRow(new DateOnly(2026, 8, 5), "PRLV SEPA MSF", "Msf", -10m, null, null);
        var csvBytes = "Date;Libelle\n2026-08-05;PRLV SEPA MSF"u8.ToArray();
        await service.CommitAsync(1, account.Id, "aout-2026.csv", [row], csvBytes);
        var batchId = Assert.Single(importBatches.All).Id;

        var (fileName, content) = await service.GetFileAsync(1, account.Id, batchId);

        Assert.Equal("aout-2026.csv", fileName);
        Assert.Equal(csvBytes, content);
    }

    [Fact]
    public async Task GetFileAsync_BatchHasNoStoredContent_ThrowsImportBatchNotFound()
    {
        var bankAccounts = new FakeBankAccountRepository();
        var account = bankAccounts.Add(1, "BoursoBank", "Compte courant");
        var transactions = new FakeTransactionRepository(bankAccounts);
        var categories = new FakeCategoryRepository();
        var rules = new FakeCategoryRuleRepository();
        var importBatches = new FakeImportBatchRepository();
        var service = new BankStatementImportService(
            [new FakeBankStatementParser("BoursoBank", [])], bankAccounts, transactions, categories, rules, importBatches);
        var row = new ImportRow(new DateOnly(2026, 8, 5), "PRLV SEPA MSF", "Msf", -10m, null, null);
        await service.CommitAsync(1, account.Id, "aout-2026.csv", [row]); // no fileContent, e.g. pre-feature batch
        var batchId = Assert.Single(importBatches.All).Id;

        await Assert.ThrowsAsync<ImportBatchNotFoundException>(() => service.GetFileAsync(1, account.Id, batchId));
    }

    [Fact]
    public async Task GetFileAsync_BatchDoesNotExist_ThrowsImportBatchNotFound()
    {
        var bankAccounts = new FakeBankAccountRepository();
        var account = bankAccounts.Add(1, "BoursoBank", "Compte courant");
        var transactions = new FakeTransactionRepository(bankAccounts);
        var categories = new FakeCategoryRepository();
        var rules = new FakeCategoryRuleRepository();
        var importBatches = new FakeImportBatchRepository();
        var service = new BankStatementImportService(
            [new FakeBankStatementParser("BoursoBank", [])], bankAccounts, transactions, categories, rules, importBatches);

        await Assert.ThrowsAsync<ImportBatchNotFoundException>(() => service.GetFileAsync(1, account.Id, batchId: 999));
    }

    [Fact]
    public async Task GetFileAsync_AccountBelongsToAnotherUser_ThrowsBankAccountNotFound()
    {
        var bankAccounts = new FakeBankAccountRepository();
        var account = bankAccounts.Add(userId: 1, bankName: "BoursoBank", label: "Compte courant");
        var transactions = new FakeTransactionRepository(bankAccounts);
        var categories = new FakeCategoryRepository();
        var rules = new FakeCategoryRuleRepository();
        var importBatches = new FakeImportBatchRepository();
        var service = new BankStatementImportService(
            [new FakeBankStatementParser("BoursoBank", [])], bankAccounts, transactions, categories, rules, importBatches);

        await Assert.ThrowsAsync<BankAccountNotFoundException>(
            () => service.GetFileAsync(userId: 999, account.Id, batchId: 1));
    }

    [Fact]
    public async Task GetHistoryAsync_ReturnsBatchesNewestFirst()
    {
        var bankAccounts = new FakeBankAccountRepository();
        var account = bankAccounts.Add(1, "BoursoBank", "Compte courant");
        var transactions = new FakeTransactionRepository(bankAccounts);
        var categories = new FakeCategoryRepository();
        var rules = new FakeCategoryRuleRepository();
        var importBatches = new FakeImportBatchRepository();
        var service = new BankStatementImportService(
            [new FakeBankStatementParser("BoursoBank", [])], bankAccounts, transactions, categories, rules, importBatches);
        await importBatches.AddAsync(new ImportBatch
        {
            BankAccountId = account.Id, FileName = "ancien.csv", ImportedAtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
        await importBatches.AddAsync(new ImportBatch
        {
            BankAccountId = account.Id, FileName = "recent.csv", ImportedAtUtc = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc)
        });

        var history = await service.GetHistoryAsync(1, account.Id);

        Assert.Equal(["recent.csv", "ancien.csv"], history.Select(b => b.FileName));
    }

    [Fact]
    public async Task GetHistoryAsync_AccountBelongsToAnotherUser_ThrowsBankAccountNotFound()
    {
        var bankAccounts = new FakeBankAccountRepository();
        var account = bankAccounts.Add(userId: 1, bankName: "BoursoBank", label: "Compte courant");
        var transactions = new FakeTransactionRepository(bankAccounts);
        var categories = new FakeCategoryRepository();
        var rules = new FakeCategoryRuleRepository();
        var importBatches = new FakeImportBatchRepository();
        var service = new BankStatementImportService(
            [new FakeBankStatementParser("BoursoBank", [])], bankAccounts, transactions, categories, rules, importBatches);

        await Assert.ThrowsAsync<BankAccountNotFoundException>(
            () => service.GetHistoryAsync(userId: 999, account.Id));
    }
}
