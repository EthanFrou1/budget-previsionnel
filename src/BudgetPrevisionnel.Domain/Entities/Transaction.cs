namespace BudgetPrevisionnel.Domain.Entities;

public class Transaction
{
    public int Id { get; set; }

    public int BankAccountId { get; set; }
    public BankAccount BankAccount { get; set; } = null!;

    public DateOnly Date { get; set; }

    public string RawLabel { get; set; } = string.Empty;
    public string? CleanedLabel { get; set; }

    public decimal Amount { get; set; }

    public int? CategoryId { get; set; }
    public Category? Category { get; set; }

    // True for transfers between the same user's own accounts, so consolidated
    // views can exclude them and avoid double-counting income/expense.
    public bool IsInternalTransfer { get; set; }

    // Free-form personal note the user attaches from the transactions screen - never
    // read or written by import/categorization, purely for their own reference.
    public string? Notes { get; set; }
}
