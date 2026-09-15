using BudgetPrevisionnel.Domain.Entities;

namespace BudgetPrevisionnel.Api.Contracts.SavingsGoals;

public sealed record CreateSavingsGoalRequest(
    string Label, decimal TargetAmount, decimal CurrentAmount, DateOnly? TargetDate, int? LinkedAccountId);

public sealed record UpdateSavingsGoalRequest(
    string Label, decimal TargetAmount, decimal CurrentAmount, DateOnly? TargetDate, int? LinkedAccountId);

public sealed record SavingsGoalResponse(
    int Id, string Label, decimal TargetAmount, decimal CurrentAmount, DateOnly? TargetDate, int? LinkedAccountId)
{
    public static SavingsGoalResponse FromEntity(SavingsGoal goal) => new(
        goal.Id, goal.Label, goal.TargetAmount, goal.CurrentAmount, goal.TargetDate, goal.LinkedAccountId);
}
