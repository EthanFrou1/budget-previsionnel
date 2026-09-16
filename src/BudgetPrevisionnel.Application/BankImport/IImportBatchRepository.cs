using BudgetPrevisionnel.Domain.Entities;

namespace BudgetPrevisionnel.Application.BankImport;

public interface IImportBatchRepository
{
    /// <summary>Newest first - ownership of bankAccountId is validated by the caller
    /// (BankStatementImportService), same pattern as TransactionRepository.GetForOtherAccountsAsync.</summary>
    Task<IReadOnlyList<ImportBatch>> GetForAccountAsync(int bankAccountId, CancellationToken cancellationToken = default);

    /// <summary>Single batch, including its FileContent - kept separate from
    /// GetForAccountAsync so the history list doesn't drag every stored CSV's bytes
    /// into memory just to render counts.</summary>
    Task<ImportBatch?> GetByIdForAccountAsync(int bankAccountId, int batchId, CancellationToken cancellationToken = default);

    Task AddAsync(ImportBatch importBatch, CancellationToken cancellationToken = default);
}
