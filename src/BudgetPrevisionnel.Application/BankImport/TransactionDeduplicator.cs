using BudgetPrevisionnel.Application.Transactions;

namespace BudgetPrevisionnel.Application.BankImport;

/// <summary>
/// Bank CSV exports carry no stable transaction id, so detecting "this statement was
/// already imported" needs a synthetic key: (Date, RawLabel, Amount). A same-day,
/// same-label, same-amount transaction CAN legitimately repeat - e.g. two identical
/// purchases on the same card the same day - so this doesn't drop every fingerprint
/// already seen. It compares how many times each fingerprint appears in the new file
/// against how many already exist on the account, and keeps only the excess.
/// </summary>
public static class TransactionDeduplicator
{
    public static IReadOnlyList<ParsedBankTransaction> RemoveAlreadyImported(
        IReadOnlyList<ParsedBankTransaction> parsedTransactions,
        IReadOnlyDictionary<TransactionFingerprint, int> existingCounts)
    {
        var remainingAllowance = new Dictionary<TransactionFingerprint, int>(existingCounts);
        var result = new List<ParsedBankTransaction>();

        foreach (var transaction in parsedTransactions)
        {
            var fingerprint = new TransactionFingerprint(transaction.Date, transaction.RawLabel, transaction.Amount);
            remainingAllowance.TryGetValue(fingerprint, out var alreadyImported);

            if (alreadyImported > 0)
            {
                remainingAllowance[fingerprint] = alreadyImported - 1;
                continue;
            }

            result.Add(transaction);
        }

        return result;
    }
}
