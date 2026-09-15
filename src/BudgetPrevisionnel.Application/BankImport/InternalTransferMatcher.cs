using BudgetPrevisionnel.Domain.Entities;

namespace BudgetPrevisionnel.Application.BankImport;

/// <summary>
/// Heuristic transfer-between-own-accounts detection: our pivot model has no counterparty
/// account/IBAN to match on, only date/label/amount, so this looks for a same-user
/// transaction of the exact opposite amount in another account within a few days (bank
/// transfers can take a day or two to settle on the other side).
///
/// Ambiguous cases (more than one same-amount candidate in the window) are deliberately
/// left unflagged rather than guessed at - a wrong auto-flag silently excludes a real
/// expense/income from the consolidated dashboard, which is worse than asking the user
/// to flag it manually (a Lot 12 UI affordance, not built yet).
/// </summary>
public static class InternalTransferMatcher
{
    public const int MaxDateDifferenceInDays = 3;

    public static IReadOnlyList<(Transaction NewTransaction, Transaction Match)> FindMatches(
        IReadOnlyList<Transaction> newTransactions,
        IReadOnlyList<Transaction> otherAccountsTransactions)
    {
        var matches = new List<(Transaction, Transaction)>();
        var claimed = new HashSet<int>();

        foreach (var transaction in newTransactions)
        {
            var candidates = otherAccountsTransactions
                .Where(other =>
                    !claimed.Contains(other.Id) &&
                    other.Amount == -transaction.Amount &&
                    Math.Abs(other.Date.DayNumber - transaction.Date.DayNumber) <= MaxDateDifferenceInDays)
                .ToList();

            if (candidates.Count == 1)
            {
                matches.Add((transaction, candidates[0]));
                claimed.Add(candidates[0].Id);
            }
        }

        return matches;
    }
}
