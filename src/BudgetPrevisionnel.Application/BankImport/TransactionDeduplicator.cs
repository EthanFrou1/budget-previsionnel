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
    /// <summary>Generic over T so the same counting logic runs both at preview time
    /// (against freshly-parsed ParsedBankTransaction rows) and again at commit time
    /// (against the ImportRow rows the client sends back) - the two must agree on what
    /// counts as "already imported" or a row could slip through one check and not the
    /// other.</summary>
    public static IReadOnlyList<T> RemoveAlreadyImported<T>(
        IReadOnlyList<T> candidates,
        IReadOnlyDictionary<TransactionFingerprint, int> existingCounts,
        Func<T, TransactionFingerprint> fingerprintSelector)
    {
        var remainingAllowance = new Dictionary<TransactionFingerprint, int>(existingCounts);
        var result = new List<T>();

        foreach (var candidate in candidates)
        {
            var fingerprint = fingerprintSelector(candidate);
            remainingAllowance.TryGetValue(fingerprint, out var alreadyImported);

            if (alreadyImported > 0)
            {
                remainingAllowance[fingerprint] = alreadyImported - 1;
                continue;
            }

            result.Add(candidate);
        }

        return result;
    }
}
