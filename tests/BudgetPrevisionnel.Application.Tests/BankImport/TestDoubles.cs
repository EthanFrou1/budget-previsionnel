using BudgetPrevisionnel.Application.BankImport;
using BudgetPrevisionnel.Domain.Entities;

namespace BudgetPrevisionnel.Application.Tests.BankImport;

internal sealed class FakeBankStatementParser(string bankName, IReadOnlyList<ParsedBankTransaction> transactions) : IBankStatementParser
{
    public string BankName => bankName;

    public Task<IReadOnlyList<ParsedBankTransaction>> ParseAsync(Stream fileStream, CancellationToken cancellationToken = default) =>
        Task.FromResult(transactions);
}

internal sealed class FakeImportBatchRepository : IImportBatchRepository
{
    private readonly List<ImportBatch> _batches = [];
    private int _nextId = 1;

    public IReadOnlyList<ImportBatch> All => _batches;

    public Task<IReadOnlyList<ImportBatch>> GetForAccountAsync(int bankAccountId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ImportBatch>>(
            _batches.Where(b => b.BankAccountId == bankAccountId).OrderByDescending(b => b.ImportedAtUtc).ToList());

    public Task<ImportBatch?> GetByIdForAccountAsync(int bankAccountId, int batchId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_batches.SingleOrDefault(b => b.BankAccountId == bankAccountId && b.Id == batchId));

    public Task AddAsync(ImportBatch importBatch, CancellationToken cancellationToken = default)
    {
        importBatch.Id = _nextId++;
        _batches.Add(importBatch);
        return Task.CompletedTask;
    }
}
