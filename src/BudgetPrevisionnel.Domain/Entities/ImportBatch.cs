using System.ComponentModel.DataAnnotations.Schema;

namespace BudgetPrevisionnel.Domain.Entities;

/// <summary>
/// One CSV statement import (preview reviewed by the user, then committed) - a record of
/// what happened, not something re-editable afterward. Counts mirror
/// BankImport.ImportSummary at the moment the import was committed.
/// </summary>
public class ImportBatch
{
    public int Id { get; set; }

    public int BankAccountId { get; set; }
    public BankAccount BankAccount { get; set; } = null!;

    public string FileName { get; set; } = string.Empty;
    public DateTime ImportedAtUtc { get; set; }

    // Null for batches committed before this was captured, or if the client never sent
    // it - GetFileAsync treats those the same as "nothing to re-download".
    public byte[]? FileContent { get; set; }

    // Not a real column - set explicitly by ImportBatchRepository.GetForAccountAsync's
    // projection, which deliberately leaves FileContent itself unloaded there (see its
    // comment). Left at its default (false) on any entity loaded outside that path.
    [NotMapped]
    public bool HasStoredFile { get; set; }

    public int TotalRowsParsed { get; set; }
    public int NewTransactionsImported { get; set; }
    public int DuplicatesSkipped { get; set; }
    public int InternalTransfersDetected { get; set; }
}
