using BudgetPrevisionnel.Application.BankAccounts;
using BudgetPrevisionnel.Application.BankImport;
using BudgetPrevisionnel.Application.Tests.BankAccounts;
using BudgetPrevisionnel.Application.Tests.Categories;
using BudgetPrevisionnel.Application.Tests.Transactions;

namespace BudgetPrevisionnel.Application.Tests.BankImport;

public class BankStatementImportServiceTests
{
    [Fact]
    public async Task ImportAsync_NewTransactions_ResolvesSystemCategoryAndPersists()
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

        var summary = await service.ImportAsync(userId: 1, bankAccountId: account.Id, Stream.Null);

        Assert.Equal(2, summary.NewTransactionsImported);
        Assert.Equal(0, summary.DuplicatesSkipped);
        Assert.Equal(2, transactions.All.Single(t => t.RawLabel == "PRLV SEPA MSF").CategoryId);
        Assert.Null(transactions.All.Single(t => t.RawLabel == "CARTE UNKNOWN").CategoryId);
    }

    [Fact]
    public async Task ImportAsync_NoExactCategoryMatch_FallsBackToUserCategoryRule()
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

        await service.ImportAsync(1, account.Id, Stream.Null);

        Assert.Equal(3, Assert.Single(transactions.All).CategoryId);
    }

    [Fact]
    public async Task ImportAsync_SameFileImportedTwice_SecondImportSkipsDuplicates()
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

        await service.ImportAsync(1, account.Id, Stream.Null);
        var secondSummary = await service.ImportAsync(1, account.Id, Stream.Null);

        Assert.Equal(0, secondSummary.NewTransactionsImported);
        Assert.Equal(1, secondSummary.DuplicatesSkipped);
        Assert.Single(transactions.All); // not duplicated in storage either
    }

    [Fact]
    public async Task ImportAsync_UnknownBank_ThrowsNoParserAvailable()
    {
        var bankAccounts = new FakeBankAccountRepository();
        var account = bankAccounts.Add(1, "UnknownBank", "Compte courant");
        var transactions = new FakeTransactionRepository(bankAccounts);
        var categories = new FakeCategoryRepository();
        var rules = new FakeCategoryRuleRepository();
        var service = new BankStatementImportService(
            [new FakeBankStatementParser("BoursoBank", [])], bankAccounts, transactions, categories, rules);

        await Assert.ThrowsAsync<NoParserAvailableException>(
            () => service.ImportAsync(1, account.Id, Stream.Null));
    }

    [Fact]
    public async Task ImportAsync_AccountBelongsToAnotherUser_ThrowsBankAccountNotFound()
    {
        var bankAccounts = new FakeBankAccountRepository();
        var account = bankAccounts.Add(userId: 1, bankName: "BoursoBank", label: "Compte courant");
        var transactions = new FakeTransactionRepository(bankAccounts);
        var categories = new FakeCategoryRepository();
        var rules = new FakeCategoryRuleRepository();
        var service = new BankStatementImportService(
            [new FakeBankStatementParser("BoursoBank", [])], bankAccounts, transactions, categories, rules);

        await Assert.ThrowsAsync<BankAccountNotFoundException>(
            () => service.ImportAsync(userId: 999, account.Id, Stream.Null));
    }

    [Fact]
    public async Task ImportAsync_MatchingTransferInAnotherOwnAccount_FlagsBothAsInternalTransfer()
    {
        var bankAccounts = new FakeBankAccountRepository();
        var checking = bankAccounts.Add(1, "BoursoBank", "Compte courant");
        var savings = bankAccounts.Add(1, "BoursoBank", "Livret");
        var transactions = new FakeTransactionRepository(bankAccounts);
        var categories = new FakeCategoryRepository();
        var rules = new FakeCategoryRuleRepository();

        var outgoing = new ParsedBankTransaction[] { new(new DateOnly(2026, 8, 10), "VIR VERS LIVRET", null, -200m, null) };
        var serviceForChecking = new BankStatementImportService(
            [new FakeBankStatementParser("BoursoBank", outgoing)], bankAccounts, transactions, categories, rules);
        await serviceForChecking.ImportAsync(1, checking.Id, Stream.Null);

        var incoming = new ParsedBankTransaction[] { new(new DateOnly(2026, 8, 11), "VIR DU COURANT", null, 200m, null) };
        var serviceForSavings = new BankStatementImportService(
            [new FakeBankStatementParser("BoursoBank", incoming)], bankAccounts, transactions, categories, rules);
        var summary = await serviceForSavings.ImportAsync(1, savings.Id, Stream.Null);

        Assert.Equal(1, summary.InternalTransfersDetected);
        Assert.All(transactions.All, t => Assert.True(t.IsInternalTransfer));
    }

    [Fact]
    public async Task ImportAsync_MatchingAmountInAnotherUsersAccount_IsNotFlagged()
    {
        var bankAccounts = new FakeBankAccountRepository();
        var mine = bankAccounts.Add(userId: 1, bankName: "BoursoBank", label: "Compte courant");
        var someoneElses = bankAccounts.Add(userId: 2, bankName: "BoursoBank", label: "Compte courant");
        var transactions = new FakeTransactionRepository(bankAccounts);
        var categories = new FakeCategoryRepository();
        var rules = new FakeCategoryRuleRepository();

        var theirDeposit = new ParsedBankTransaction[] { new(new DateOnly(2026, 8, 10), "VIR", null, 200m, null) };
        var serviceForThem = new BankStatementImportService(
            [new FakeBankStatementParser("BoursoBank", theirDeposit)], bankAccounts, transactions, categories, rules);
        await serviceForThem.ImportAsync(2, someoneElses.Id, Stream.Null);

        var myWithdrawal = new ParsedBankTransaction[] { new(new DateOnly(2026, 8, 10), "VIR", null, -200m, null) };
        var serviceForMe = new BankStatementImportService(
            [new FakeBankStatementParser("BoursoBank", myWithdrawal)], bankAccounts, transactions, categories, rules);
        var summary = await serviceForMe.ImportAsync(1, mine.Id, Stream.Null);

        Assert.Equal(0, summary.InternalTransfersDetected);
    }
}
