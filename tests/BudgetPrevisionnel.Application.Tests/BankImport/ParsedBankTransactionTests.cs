using BudgetPrevisionnel.Application.BankImport;

namespace BudgetPrevisionnel.Application.Tests.BankImport;

public class ParsedBankTransactionTests
{
    [Fact]
    public void TwoTransactions_WithSameValues_AreEqual()
    {
        var a = new ParsedBankTransaction(new DateOnly(2026, 9, 1), "RIOT* CB*2856", "Riot Games", -10.99m, "Abonnements & téléphonie");
        var b = new ParsedBankTransaction(new DateOnly(2026, 9, 1), "RIOT* CB*2856", "Riot Games", -10.99m, "Abonnements & téléphonie");

        Assert.Equal(a, b);
    }

    [Fact]
    public void CleanedLabelAndSuggestedCategory_CanBeNull_ForBanksThatDontProvideThem()
    {
        var transaction = new ParsedBankTransaction(new DateOnly(2026, 9, 1), "VIR SEPA SALAIRE", null, 1500m, null);

        Assert.Null(transaction.CleanedLabel);
        Assert.Null(transaction.SuggestedCategory);
    }
}
