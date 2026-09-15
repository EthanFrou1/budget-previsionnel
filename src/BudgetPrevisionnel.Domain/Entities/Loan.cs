namespace BudgetPrevisionnel.Domain.Entities;

/// <summary>
/// A credit with a known repayment schedule, unlike a <see cref="RecurringExpense"/>
/// which may run indefinitely. Feeds the forecast as a recurring cost until EndDate.
/// </summary>
public class Loan
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public string Label { get; set; } = string.Empty;
    public decimal PrincipalAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public decimal InterestRate { get; set; }
    public decimal MonthlyPayment { get; set; }
    public DateOnly EndDate { get; set; }
}
