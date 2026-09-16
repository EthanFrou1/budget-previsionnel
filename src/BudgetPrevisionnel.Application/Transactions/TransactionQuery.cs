using BudgetPrevisionnel.Domain.Entities;

namespace BudgetPrevisionnel.Application.Transactions;

public enum TransactionSortColumn
{
    Date,
    BankAccount,
    Label,
    Amount,
    Category,
}

/// <summary>
/// BankAccountId absent = the consolidated view (all of the user's accounts); present =
/// the per-account view. ExcludeInternalTransfers lets a caller avoid double-counting a
/// transfer between two of the user's own accounts as both an expense and an income -
/// see the brief's note on this. Page/PageSize are assumed already validated/clamped by
/// the caller (TransactionService), same as SortBy/SortDescending (an unrecognized sortBy
/// query string is normalized to the Date/descending default there, not here).
/// </summary>
public sealed record TransactionQuery(
    int UserId,
    int? BankAccountId,
    int? CategoryId,
    DateOnly? FromDate,
    DateOnly? ToDate,
    string? Search,
    bool ExcludeInternalTransfers,
    int Page,
    int PageSize,
    TransactionSortColumn SortBy = TransactionSortColumn.Date,
    bool SortDescending = true);

public sealed record TransactionPage(IReadOnlyList<Transaction> Items, int TotalCount, int Page, int PageSize);
