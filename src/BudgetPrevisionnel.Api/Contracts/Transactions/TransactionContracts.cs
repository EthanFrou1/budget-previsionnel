using BudgetPrevisionnel.Application.Transactions;
using BudgetPrevisionnel.Domain.Entities;

namespace BudgetPrevisionnel.Api.Contracts.Transactions;

public sealed record TransactionResponse(
    int Id,
    int BankAccountId,
    string BankAccountLabel,
    DateOnly Date,
    string RawLabel,
    string? CleanedLabel,
    decimal Amount,
    int? CategoryId,
    string? CategoryName,
    bool IsInternalTransfer,
    string? Notes)
{
    public static TransactionResponse FromEntity(Transaction transaction) => new(
        transaction.Id,
        transaction.BankAccountId,
        transaction.BankAccount.Label,
        transaction.Date,
        transaction.RawLabel,
        transaction.CleanedLabel,
        transaction.Amount,
        transaction.CategoryId,
        transaction.Category?.Name,
        transaction.IsInternalTransfer,
        transaction.Notes);
}

public sealed record TransactionPageResponse(
    IReadOnlyList<TransactionResponse> Items, int TotalCount, int Page, int PageSize)
{
    public static TransactionPageResponse FromResult(TransactionPage page) => new(
        page.Items.Select(TransactionResponse.FromEntity).ToList(), page.TotalCount, page.Page, page.PageSize);
}

public sealed record UpdateTransactionCategoryRequest(int? CategoryId);

public sealed record UpdateTransactionNotesRequest(string? Notes);
