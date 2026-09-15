using BudgetPrevisionnel.Application.BankImport;
using BudgetPrevisionnel.Domain.Entities;

namespace BudgetPrevisionnel.Application.Tests.BankImport;

public class InternalTransferMatcherTests
{
    private static Transaction Tx(int id, DateOnly date, decimal amount) =>
        new() { Id = id, Date = date, Amount = amount, RawLabel = "test" };

    [Fact]
    public void FindMatches_OppositeAmountWithinWindow_Matches()
    {
        var withdrawal = Tx(1, new DateOnly(2026, 8, 10), -200m);
        var deposit = Tx(2, new DateOnly(2026, 8, 11), 200m);

        var matches = InternalTransferMatcher.FindMatches([withdrawal], [deposit]);

        var match = Assert.Single(matches);
        Assert.Equal(withdrawal, match.NewTransaction);
        Assert.Equal(deposit, match.Match);
    }

    [Fact]
    public void FindMatches_OutsideDateWindow_DoesNotMatch()
    {
        var withdrawal = Tx(1, new DateOnly(2026, 8, 1), -200m);
        var deposit = Tx(2, new DateOnly(2026, 8, 10), 200m); // 9 days later, window is 3

        var matches = InternalTransferMatcher.FindMatches([withdrawal], [deposit]);

        Assert.Empty(matches);
    }

    [Fact]
    public void FindMatches_SameSignAmount_DoesNotMatch()
    {
        var withdrawal = Tx(1, new DateOnly(2026, 8, 10), -200m);
        var anotherWithdrawal = Tx(2, new DateOnly(2026, 8, 10), -200m);

        var matches = InternalTransferMatcher.FindMatches([withdrawal], [anotherWithdrawal]);

        Assert.Empty(matches);
    }

    [Fact]
    public void FindMatches_MultipleCandidatesForSameAmount_IsAmbiguousAndLeftUnflagged()
    {
        var withdrawal = Tx(1, new DateOnly(2026, 8, 10), -200m);
        var depositA = Tx(2, new DateOnly(2026, 8, 10), 200m);
        var depositB = Tx(3, new DateOnly(2026, 8, 11), 200m);

        var matches = InternalTransferMatcher.FindMatches([withdrawal], [depositA, depositB]);

        Assert.Empty(matches);
    }

    [Fact]
    public void FindMatches_CandidateAlreadyClaimedByAnotherTransaction_IsNotReused()
    {
        var withdrawalA = Tx(1, new DateOnly(2026, 8, 10), -200m);
        var withdrawalB = Tx(2, new DateOnly(2026, 8, 10), -200m);
        var onlyDeposit = Tx(3, new DateOnly(2026, 8, 10), 200m);

        var matches = InternalTransferMatcher.FindMatches([withdrawalA, withdrawalB], [onlyDeposit]);

        var match = Assert.Single(matches);
        Assert.Equal(withdrawalA, match.NewTransaction);
    }
}
