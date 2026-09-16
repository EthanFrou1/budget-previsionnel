namespace BudgetPrevisionnel.Domain.Entities;

public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }

    public ICollection<BankAccount> BankAccounts { get; set; } = new List<BankAccount>();
    public ICollection<Category> CustomCategories { get; set; } = new List<Category>();
    public ICollection<CategoryRule> CategoryRules { get; set; } = new List<CategoryRule>();
    public ICollection<RecurringExpense> RecurringExpenses { get; set; } = new List<RecurringExpense>();
    public ICollection<RecurringIncome> RecurringIncomes { get; set; } = new List<RecurringIncome>();
    public ICollection<SavingsGoal> SavingsGoals { get; set; } = new List<SavingsGoal>();
    public ICollection<Loan> Loans { get; set; } = new List<Loan>();
    public ICollection<Budget> Budgets { get; set; } = new List<Budget>();
}
