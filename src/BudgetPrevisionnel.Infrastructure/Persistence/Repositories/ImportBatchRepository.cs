using BudgetPrevisionnel.Application.BankImport;
using BudgetPrevisionnel.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BudgetPrevisionnel.Infrastructure.Persistence.Repositories;

public sealed class ImportBatchRepository(BudgetDbContext dbContext) : IImportBatchRepository
{
    public async Task<IReadOnlyList<ImportBatch>> GetForAccountAsync(int bankAccountId, CancellationToken cancellationToken = default) =>
        // Projected rather than a plain ToListAsync: the history list is rendered on
        // every /accounts page load, and dragging every stored CSV's bytes into memory
        // just to show counts would only get worse as imports pile up over the years.
        // FileContent itself is left null here; ImportBatchResponse only needs to know
        // whether one is stored, which HasStoredFile below captures without the bytes.
        await dbContext.ImportBatches
            .Where(b => b.BankAccountId == bankAccountId)
            .OrderByDescending(b => b.ImportedAtUtc)
            .ThenByDescending(b => b.Id)
            .Select(b => new ImportBatch
            {
                Id = b.Id,
                BankAccountId = b.BankAccountId,
                FileName = b.FileName,
                ImportedAtUtc = b.ImportedAtUtc,
                HasStoredFile = b.FileContent != null,
                TotalRowsParsed = b.TotalRowsParsed,
                NewTransactionsImported = b.NewTransactionsImported,
                DuplicatesSkipped = b.DuplicatesSkipped,
                InternalTransfersDetected = b.InternalTransfersDetected
            })
            .ToListAsync(cancellationToken);

    public Task<ImportBatch?> GetByIdForAccountAsync(int bankAccountId, int batchId, CancellationToken cancellationToken = default) =>
        dbContext.ImportBatches
            .FirstOrDefaultAsync(b => b.BankAccountId == bankAccountId && b.Id == batchId, cancellationToken);

    public async Task AddAsync(ImportBatch importBatch, CancellationToken cancellationToken = default)
    {
        dbContext.ImportBatches.Add(importBatch);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
