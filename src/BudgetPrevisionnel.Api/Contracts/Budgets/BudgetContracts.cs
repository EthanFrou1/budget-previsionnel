using BudgetPrevisionnel.Application.Budgets;

namespace BudgetPrevisionnel.Api.Contracts.Budgets;

public sealed record CreateBudgetRequest(DateOnly Month, int CategoryId, decimal PlannedAmount);

public sealed record UpdateBudgetRequest(decimal PlannedAmount);

public sealed record BudgetLineResponse(
    int Id, DateOnly Month, int CategoryId, string CategoryName, decimal PlannedAmount, decimal ActualAmount)
{
    public static BudgetLineResponse FromLine(BudgetLine line) => new(
        line.Budget.Id, line.Budget.Month, line.Budget.CategoryId, line.Budget.Category.Name,
        line.Budget.PlannedAmount, line.ActualAmount);
}
