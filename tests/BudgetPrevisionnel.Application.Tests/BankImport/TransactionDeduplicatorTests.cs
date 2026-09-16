using BudgetPrevisionnel.Application.BankImport;
using BudgetPrevisionnel.Application.Transactions;

namespace BudgetPrevisionnel.Application.Tests.BankImport;

public class TransactionDeduplicatorTests
{
    private static ParsedBankTransaction Transaction(DateOnly date, string label, decimal amount) =>
        new(date, label, CleanedLabel: null, amount, SuggestedCategory: null);

    private static TransactionFingerprint FingerprintOf(ParsedBankTransaction t) =>
        new(t.Date, t.RawLabel, t.Amount);

    [Fact]
    public void RemoveAlreadyImported_NoExistingTransactions_KeepsEverything()
    {
        var parsed = new[] { Transaction(new DateOnly(2026, 8, 31), "Riot Games", -10.99m) };

        var result = TransactionDeduplicator.RemoveAlreadyImported(
            parsed, new Dictionary<TransactionFingerprint, int>(), FingerprintOf);

        Assert.Single(result);
    }

    [Fact]
    public void RemoveAlreadyImported_ExactlyOneExistingMatch_DropsIt()
    {
        var fingerprint = new TransactionFingerprint(new DateOnly(2026, 8, 31), "Riot Games", -10.99m);
        var parsed = new[] { Transaction(fingerprint.Date, fingerprint.RawLabel, fingerprint.Amount) };
        var existing = new Dictionary<TransactionFingerprint, int> { [fingerprint] = 1 };

        var result = TransactionDeduplicator.RemoveAlreadyImported(parsed, existing, FingerprintOf);

        Assert.Empty(result);
    }

    [Fact]
    public void RemoveAlreadyImported_SameFingerprintTwiceInFileButOnlyOneExisting_KeepsTheSecondAsNew()
    {
        // Two genuinely separate same-day, same-amount purchases (e.g. the real Riot
        // Games CB row appearing twice with -10,99 in the sample file) must not collapse
        // into one just because one of them was already imported.
        var date = new DateOnly(2026, 8, 31);
        var parsed = new[]
        {
            Transaction(date, "Riot Games", -10.99m),
            Transaction(date, "Riot Games", -10.99m)
        };
        var existing = new Dictionary<TransactionFingerprint, int>
        {
            [new TransactionFingerprint(date, "Riot Games", -10.99m)] = 1
        };

        var result = TransactionDeduplicator.RemoveAlreadyImported(parsed, existing, FingerprintOf);

        Assert.Single(result);
    }

    [Fact]
    public void RemoveAlreadyImported_DifferentAmountSameDayAndLabel_BothKept()
    {
        var date = new DateOnly(2026, 8, 31);
        var parsed = new[]
        {
            Transaction(date, "Riot Games", -10.99m),
            Transaction(date, "Riot Games", -4.99m)
        };

        var result = TransactionDeduplicator.RemoveAlreadyImported(
            parsed, new Dictionary<TransactionFingerprint, int>(), FingerprintOf);

        Assert.Equal(2, result.Count);
    }
}
