using BudgetPrevisionnel.Domain.Entities;

namespace BudgetPrevisionnel.Domain.Tests.Entities;

public class UserTests
{
    [Fact]
    public void NewUser_HasEmptyCollections_NotNull()
    {
        var user = new User();

        Assert.Empty(user.BankAccounts);
        Assert.Empty(user.CustomCategories);
        Assert.Empty(user.CategoryRules);
        Assert.Empty(user.RecurringExpenses);
        Assert.Empty(user.SavingsGoals);
        Assert.Empty(user.Loans);
        Assert.Empty(user.Budgets);
    }
}
