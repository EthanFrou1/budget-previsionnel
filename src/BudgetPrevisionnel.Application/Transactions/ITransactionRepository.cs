using BudgetPrevisionnel.Domain.Entities;

namespace BudgetPrevisionnel.Application.Transactions;

/// <summary>
/// Identifies a transaction by content rather than a bank-provided id (CSV exports don't
/// carry one). Two transactions can legitimately share a fingerprint - see
/// BankImport.TransactionDeduplicator, which counts occurrences rather than treating any
/// repeat as automatically already-imported.
/// </summary>
public readonly record struct TransactionFingerprint(DateOnly Date, string RawLabel, decimal Amount);

public interface ITransactionRepository
{
    /// <summary>How many existing transactions on this account already match each fingerprint.</summary>
    Task<IReadOnlyDictionary<TransactionFingerprint, int>> GetFingerprintCountsAsync(
        int bankAccountId, CancellationToken cancellationToken = default);

    Task AddRangeAsync(IEnumerable<Transaction> transactions, CancellationToken cancellationToken = default);

    /// <summary>
    /// Candidate pool for internal-transfer matching: this user's OTHER accounts, within
    /// a date window, excluding transactions already flagged as a transfer.
    /// </summary>
    Task<IReadOnlyList<Transaction>> GetForOtherAccountsAsync(
        int userId, int excludeBankAccountId, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default);

    Task<TransactionPage> SearchAsync(TransactionQuery query, CancellationToken cancellationToken = default);

    /// <summary>Scoped by the owning account's UserId, same pattern as
    /// IBankAccountRepository.GetByIdForUserAsync - never returns another user's transaction.</summary>
    Task<Transaction?> GetByIdForUserAsync(int userId, int transactionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Raw signed sum of Amount for this user/category/date-range, excluding internal
    /// transfers (they're not real spend/income - same reasoning as
    /// TransactionQuery.ExcludeInternalTransfers). Used by BudgetService to compute
    /// "actual" for a category/month: a negative sum here means net spend, so the
    /// caller negates it to get a positive "amount spent" figure.
    /// </summary>
    Task<decimal> GetNetAmountAsync(
        int userId, int categoryId, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
