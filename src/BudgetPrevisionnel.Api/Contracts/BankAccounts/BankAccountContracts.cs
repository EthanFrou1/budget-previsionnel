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
