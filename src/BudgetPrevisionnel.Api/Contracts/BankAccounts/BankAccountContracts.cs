using BudgetPrevisionnel.Application.BankImport;
using BudgetPrevisionnel.Domain.Entities;

namespace BudgetPrevisionnel.Api.Contracts.BankAccounts;

public sealed record CreateBankAccountRequest(string BankName, string Label, string? Iban);

public sealed record UpdateBankAccountRequest(string BankName, string Label, string? Iban);

public sealed record BankAccountResponse(int Id, string BankName, string Label, string? Iban)
{
    public static BankAccountResponse FromEntity(BankAccount account) =>
        new(account.Id, account.BankName, account.Label, account.Iban);
}

public sealed record ImportSummaryResponse(
    int TotalRowsParsed, int NewTransactionsImported, int DuplicatesSkipped, int InternalTransfersDetected)
{
    public static ImportSummaryResponse FromResult(ImportSummary summary) =>
        new(summary.TotalRowsParsed, summary.NewTransactionsImported, summary.DuplicatesSkipped, summary.InternalTransfersDetected);
}

/// <summary>One not-yet-persisted row, as returned by the preview endpoint and echoed
/// back (possibly with an edited CategoryId, or simply omitted if the user excludes it)
/// to the commit endpoint.</summary>
public sealed record ImportRowResponse(
    DateOnly Date, string RawLabel, string? CleanedLabel, decimal Amount, int? CategoryId, string? CategoryName)
{
    public static ImportRowResponse FromRow(ImportRow row) =>
        new(row.Date, row.RawLabel, row.CleanedLabel, row.Amount, row.CategoryId, row.CategoryName);
}

public sealed record ImportCommitRowRequest(DateOnly Date, string RawLabel, string? CleanedLabel, decimal Amount, int? CategoryId)
{
    public ImportRow ToImportRow() => new(Date, RawLabel, CleanedLabel, Amount, CategoryId, CategoryName: null);
}

/// <summary>FileContentBase64 is optional so older clients/tests that only cared about
/// the rows still compile and work - CommitAsync just records no re-downloadable file
/// for that batch when it's absent.</summary>
public sealed record ImportCommitRequest(
    string FileName, IReadOnlyList<ImportCommitRowRequest> Rows, string? FileContentBase64 = null);

/// <summary>One past CSV import, newest first - see ImportBatch's own doc comment.
/// HasStoredFile gates whether the frontend offers a download link: false for batches
/// committed before file capture existed, or if the client never sent one.</summary>
public sealed record ImportBatchResponse(
    int Id, string FileName, DateTime ImportedAtUtc, int TotalRowsParsed, int NewTransactionsImported,
    int DuplicatesSkipped, int InternalTransfersDetected, bool HasStoredFile)
{
    public static ImportBatchResponse FromEntity(ImportBatch batch) => new(
        batch.Id, batch.FileName, batch.ImportedAtUtc, batch.TotalRowsParsed, batch.NewTransactionsImported,
        batch.DuplicatesSkipped, batch.InternalTransfersDetected, batch.HasStoredFile);
}
