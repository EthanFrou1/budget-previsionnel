using BudgetPrevisionnel.Domain.Enums;

namespace BudgetPrevisionnel.Domain.Entities;

public class RecurringIncome
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public string Label { get; set; } = string.Empty;
    public decimal Amount { get; set; }

    public int? CategoryId { get; set; }
    public Category? Category { get; set; }

    public RecurrenceFrequency Frequency { get; set; }
    public DateOnly StartDate { get; set; }

    // Null for income with no known end (e.g. an ongoing salary).
    public DateOnly? EndDate { get; set; }
}
