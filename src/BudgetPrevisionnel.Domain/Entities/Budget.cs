namespace BudgetPrevisionnel.Domain.Entities;

/// <summary>
/// Planned spend for one category in one month. ActualAmount from the brief's
/// model sketch is deliberately not stored here: it's derived by summing that
/// month's transactions in the category, computed by the Application layer
/// rather than persisted (persisting it would let it drift out of sync).
/// </summary>
public class Budget
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    // Stored as the first day of the month (e.g. 2026-09-01) by convention.
    public DateOnly Month { get; set; }

    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    public decimal PlannedAmount { get; set; }
}
