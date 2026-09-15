namespace BudgetPrevisionnel.Domain.Entities;

public class SavingsGoal
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public string Label { get; set; } = string.Empty;
    public decimal TargetAmount { get; set; }
    public decimal CurrentAmount { get; set; }
    public DateOnly? TargetDate { get; set; }

    public int? LinkedAccountId { get; set; }
    public BankAccount? LinkedAccount { get; set; }
}
