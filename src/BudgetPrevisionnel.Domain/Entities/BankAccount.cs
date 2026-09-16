namespace BudgetPrevisionnel.Domain.Entities;

public class BankAccount
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    // Drives which IBankStatementParser (Application layer) handles this account's statement imports.
    public string BankName { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? Iban { get; set; }

    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    public ICollection<ImportBatch> ImportBatches { get; set; } = new List<ImportBatch>();
}
