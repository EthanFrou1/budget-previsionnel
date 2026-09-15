using BudgetPrevisionnel.Application.BankImport;

namespace BudgetPrevisionnel.Application.Tests.BankImport;

internal sealed class FakeBankStatementParser(string bankName, IReadOnlyList<ParsedBankTransaction> transactions) : IBankStatementParser
{
    public string BankName => bankName;

    public Task<IReadOnlyList<ParsedBankTransaction>> ParseAsync(Stream fileStream, CancellationToken cancellationToken = default) =>
        Task.FromResult(transactions);
}
