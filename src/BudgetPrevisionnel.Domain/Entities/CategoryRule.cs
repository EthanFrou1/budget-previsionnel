namespace BudgetPrevisionnel.Domain.Entities;

/// <summary>
/// User-defined auto-categorization rule ("if label contains X, use category Y"),
/// applied to future imports to reduce manual re-tagging.
/// </summary>
public class CategoryRule
{
    public int Id { get; set; }

    public int OwnerId { get; set; }
    public User Owner { get; set; } = null!;

    public string MatchPattern { get; set; } = string.Empty;

    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    // Lower value = evaluated first, when multiple rules could match the same transaction.
    public int Priority { get; set; }
}
